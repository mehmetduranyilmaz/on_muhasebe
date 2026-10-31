using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Modules.Fatura.Features;
using OnMuhasebe.Modules.Fatura.Infrastructure;
using OnMuhasebe.Shared.Contracts.Ayarlar;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Modules;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Fatura;

// Cari ve Stok'a yalnızca sözleşmeleri (ICariHareketYazici, IStokHareketYazici...) üzerinden bağlıdır.
public sealed class FaturaModulu : IModul
{
    public string Ad => "Fatura";

    public IReadOnlyList<ZorunluAlanTanimi> ZorunluAlanlar => FaturaZorunluAlanlari.Tumu;

    public void ServisleriEkle(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModulDbContext<FaturaDbContext>(configuration);
        services.AddValidatorsFromAssemblyContaining<FaturaModulu>(includeInternalTypes: true);
        services.AddScoped<FaturaAyarServisi>();
        services.AddScoped<FaturaNumaraUretici>();
        services.AddScoped<FaturaHazirlayici>();
        services.AddScoped<FaturaKayitServisi>();
        services.AddScoped<IrsaliyeHazirlayici>();
        services.AddScoped<IrsaliyeKayitServisi>();
        services.AddScoped<TeklifSiparisServisi>();
    }

    public void EndpointleriEkle(IEndpointRouteBuilder endpoints)
    {
        var grup = endpoints.MapGroup("/api/fatura").WithTags("Fatura").RequireModul(ModulKodlari.Fatura);

        grup.MapDegisiklikGecmisi<FaturaDbContext>();
        FaturaIslemleri.Map(grup);
        IrsaliyeIslemleri.Map(grup);
        TeklifSiparisIslemleri.Map(grup);
        FaturaAyarIslemleri.Map(grup);
        FaturaRaporlari.Map(grup);
    }
}
