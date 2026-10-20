using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.KasaBanka.Features;
using OnMuhasebe.Modules.KasaBanka.Infrastructure;
using OnMuhasebe.Modules.KasaBanka.Infrastructure.CekSenet;
using OnMuhasebe.Modules.KasaBanka.Infrastructure.Ekstre;
using OnMuhasebe.Shared.Contracts.Ayarlar;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Modules;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.KasaBanka;

// Cari'ye yalnızca sözleşmeleri (ICariSorgusu, ICariHareketYazici) üzerinden bağlıdır; Fatura peşin tahsilat için IKasaBankaYazici'yi kullanır.
public sealed class KasaBankaModulu : IModul
{
    public string Ad => "KasaBanka";

    public IReadOnlyList<ZorunluAlanTanimi> ZorunluAlanlar => KasaBankaZorunluAlanlari.Tumu;

    public void ServisleriEkle(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModulDbContext<KasaBankaDbContext>(configuration);
        services.AddValidatorsFromAssemblyContaining<KasaBankaModulu>(includeInternalTypes: true);
        services.AddScoped<KasaKodUretici>();
        services.AddScoped<KategoriServisi>();
        services.AddScoped<CariDovizServisi>();
        services.AddScoped<KasaIslemServisi>();
        services.AddScoped<CekSenetServisi>();
        services.AddScoped<EvrakRiskServisi>();
        services.AddScoped<EkstreServisi>();
        services.AddScoped<IKasaBankaSorgusu, KasaBankaSorgusu>();
        services.AddScoped<IKasaBankaYazici, KasaBankaYazici>();
    }

    public void EndpointleriEkle(IEndpointRouteBuilder endpoints)
    {
        var grup = endpoints.MapGroup("/api/kasabanka").WithTags("KasaBanka").RequireModul(ModulKodlari.KasaBanka);

        HesapIslemleri.Map(grup);
        IslemIslemleri.Map(grup);
        KategoriIslemleri.Map(grup);
        KasaBankaRaporlari.Map(grup);
        EkstreIslemleri.Map(grup);
        CekSenetIslemleri.Map(grup);
    }
}
