using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnMuhasebe.Modules.Satinalma.Contracts;
using OnMuhasebe.Modules.Satinalma.Features;
using OnMuhasebe.Modules.Satinalma.Infrastructure;
using OnMuhasebe.Shared.Contracts.Dosyalar;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Infrastructure.Bildirimler;
using OnMuhasebe.Shared.Infrastructure.Dosyalar;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Modules;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Satinalma;

// Kiralanan modül: satın alma talebi ve tutara bağlı onay akışı. Onaylanan talep Fatura modülünde taslak satınalma siparişine dönüşür.
public sealed class SatinalmaModulu : IModul
{
    public string Ad => "Satın Alma";

    public IReadOnlyList<YetkiKaynagi> YetkiKaynaklari => SatinalmaYetkileri.Tumu;

    public void ServisleriEkle(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModulDbContext<SatinalmaDbContext>(configuration);
        services.AddValidatorsFromAssemblyContaining<SatinalmaModulu>(includeInternalTypes: true);
        services.AddScoped<OnaycilarServisi>();
        services.AddScoped<IBildirimKaynagi, SatinalmaBildirimKaynagi>();
        services.AddDosyaKaynagi<SatinalmaDbContext>(DosyaKayitTurleri.SatinalmaTalebi, ModulKodlari.Satinalma, SatinalmaYetkileri.Talep,
            (db, id, ct) => db.Talepler.AnyAsync(t => t.Id == id, ct));
    }

    public void EndpointleriEkle(IEndpointRouteBuilder endpoints)
    {
        var grup = endpoints.MapGroup("/api/satinalma").WithTags("Satın Alma").RequireModul(ModulKodlari.Satinalma);
        grup.MapDegisiklikGecmisi<SatinalmaDbContext>();
        TalepIslemleri.Map(grup);
        OnayKuraliIslemleri.Map(grup);
    }
}
