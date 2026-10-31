using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Modules.Stok.Features;
using OnMuhasebe.Modules.Stok.Infrastructure;
using OnMuhasebe.Shared.Contracts.Ayarlar;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Modules;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Stok;

public sealed class StokModulu : IModul
{
    public string Ad => "Stok";

    public IReadOnlyList<ZorunluAlanTanimi> ZorunluAlanlar => StokZorunluAlanlari.Tumu;

    public IReadOnlyList<YetkiKaynagi> YetkiKaynaklari => StokYetkileri.Tumu;

    public void ServisleriEkle(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModulDbContext<StokDbContext>(configuration);
        services.AddValidatorsFromAssemblyContaining<StokModulu>(includeInternalTypes: true);
        services.AddScoped<StokKodUretici>();
        services.AddScoped<DepoServisi>();
        services.AddScoped<StokAyarServisi>();
        services.AddScoped<StokHesaplayici>();
        services.AddScoped<StokCikisKontrolu>();
        services.AddScoped<IzlenebilirlikServisi>();
        services.AddScoped<IStokSorgusu, StokSorgusu>();
        services.AddScoped<IStokHareketYazici, StokHareketYazici>();
    }

    public void EndpointleriEkle(IEndpointRouteBuilder endpoints)
    {
        var grup = endpoints.MapGroup("/api/stok").WithTags("Stok").RequireModul(ModulKodlari.Stok);

        grup.MapDegisiklikGecmisi<StokDbContext>();
        StokKartiIslemleri.Map(grup);
        StokHareketIslemleri.Map(grup);
        StokTanimIslemleri.Map(grup);
        StokRaporlari.Map(grup);
        FiyatListesiIslemleri.Map(grup);
        IzlenebilirlikIslemleri.Map(grup);
    }
}
