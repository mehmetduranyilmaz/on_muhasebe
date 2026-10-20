using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnMuhasebe.Modules.Doviz.Contracts;
using OnMuhasebe.Modules.Doviz.Infrastructure;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Modules;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;

namespace OnMuhasebe.Modules.Doviz;

// Lisanssız çekirdek modül: tüm firmalar ve diğer modüller kur bilgisini buradan alır.
public sealed class DovizModulu : IModul
{
    public string Ad => "Doviz";

    public void ServisleriEkle(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModulDbContext<DovizDbContext>(configuration);
        services.AddHttpClient<TcmbIstemcisi>(c =>
        {
            c.BaseAddress = TcmbIstemcisi.Adres;
            c.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<IKurSorgusu, KurSorgusu>();
    }

    public void EndpointleriEkle(IEndpointRouteBuilder endpoints)
    {
        var grup = endpoints.MapGroup("/api/doviz").WithTags("Doviz").RequireAuthorization();
        grup.MapGet("/islem-kuru", IslemKuruAsync);
    }

    private static async Task<IResult> IslemKuruAsync(
        string paraBirimi, DateOnly tarih, IKurSorgusu kurSorgusu, CancellationToken cancellationToken)
    {
        if (!ParaBirimleri.GecerliMi(paraBirimi))
        {
            return Hata.Dogrulama("gecersiz_para_birimi", "Geçersiz para birimi.").Problem();
        }

        var kur = await kurSorgusu.IslemKuruAsync(paraBirimi, tarih, cancellationToken);
        return kur is null
            ? Hata.Bulunamadi("kur_bulunamadi", "Bu tarih için TCMB kuru bulunamadı; kuru elle girebilirsiniz.").Problem()
            : TypedResults.Ok(kur);
    }
}
