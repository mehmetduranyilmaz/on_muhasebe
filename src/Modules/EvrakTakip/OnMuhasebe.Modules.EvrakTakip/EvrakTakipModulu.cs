using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnMuhasebe.Modules.EvrakTakip.Contracts;
using OnMuhasebe.Modules.EvrakTakip.Features;
using OnMuhasebe.Modules.EvrakTakip.Infrastructure;
using OnMuhasebe.Shared.Contracts.Dosyalar;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Infrastructure.Bildirimler;
using OnMuhasebe.Shared.Infrastructure.Dosyalar;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Modules;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.EvrakTakip;

// Kiralanan modül: ruhsat, poliçe, sözleşme gibi süreli evrakların bitiş takibi ve hatırlatması. Dosya eki temel lisanstaki Dosya modülündedir.
public sealed class EvrakTakipModulu : IModul
{
    public string Ad => "Evrak Takibi";

    public IReadOnlyList<YetkiKaynagi> YetkiKaynaklari => EvrakTakipYetkileri.Tumu;

    public void ServisleriEkle(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModulDbContext<EvrakTakipDbContext>(configuration);
        services.AddValidatorsFromAssemblyContaining<EvrakTakipModulu>(includeInternalTypes: true);
        services.AddScoped<IBildirimKaynagi, EvrakTakipBildirimKaynagi>();
        services.AddDosyaKaynagi<EvrakTakipDbContext>(DosyaKayitTurleri.TakipEvraki, ModulKodlari.EvrakTakip, EvrakTakipYetkileri.Kayit,
            (db, id, ct) => db.TakipEvraklari.AnyAsync(e => e.Id == id, ct));
    }

    public void EndpointleriEkle(IEndpointRouteBuilder endpoints)
    {
        var grup = endpoints.MapGroup("/api/evrak-takibi").WithTags("Evrak Takibi").RequireModul(ModulKodlari.EvrakTakip);
        grup.MapDegisiklikGecmisi<EvrakTakipDbContext>();
        TakipEvrakiIslemleri.Map(grup);
    }
}
