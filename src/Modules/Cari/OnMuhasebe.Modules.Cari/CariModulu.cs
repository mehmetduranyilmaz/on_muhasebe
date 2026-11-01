using OnMuhasebe.Shared.Infrastructure.Dosyalar;
using OnMuhasebe.Shared.Contracts.Dosyalar;
using Microsoft.EntityFrameworkCore;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.Cari.Features;
using OnMuhasebe.Modules.Cari.Infrastructure;
using OnMuhasebe.Shared.Contracts.Ayarlar;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Bildirimler;
using OnMuhasebe.Shared.Infrastructure.Modules;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Cari;

public sealed class CariModulu : IModul
{
    public string Ad => "Cari";

    public IReadOnlyList<ZorunluAlanTanimi> ZorunluAlanlar => CariZorunluAlanlari.Tumu;

    public IReadOnlyList<YetkiKaynagi> YetkiKaynaklari => CariYetkileri.Tumu;

    public void ServisleriEkle(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModulDbContext<CariDbContext>(configuration);
        services.AddValidatorsFromAssemblyContaining<CariModulu>(includeInternalTypes: true);
        services.AddScoped<CariKodUretici>();
        services.AddScoped<CariAktarimServisi>();
        services.AddDosyaKaynagi<CariDbContext>(DosyaKayitTurleri.Cari, ModulKodlari.Cari, CariYetkileri.Hesap,
            (db, id, ct) => db.CariHesaplar.AnyAsync(k => k.Id == id, ct));
        services.AddScoped<CariKapamaServisi>();
        services.AddScoped<ICariSorgusu, CariSorgusu>();
        services.AddScoped<IBildirimKaynagi, RiskBildirimKaynagi>();
        services.AddScoped<ICariHareketYazici, CariHareketYazici>();
    }

    public void EndpointleriEkle(IEndpointRouteBuilder endpoints)
    {
        var grup = endpoints.MapGroup("/api/cari").WithTags("Cari").RequireModul(ModulKodlari.Cari);

        grup.MapDegisiklikGecmisi<CariDbContext>();
        CariHesapIslemleri.Map(grup);
        CariAktarimIslemleri.Map(grup);
        CariHareketIslemleri.Map(grup);
        CariRaporlari.Map(grup);
        CariGrubuIslemleri.Map(grup);
        CariKapamaIslemleri.Map(grup);
    }
}
