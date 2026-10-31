using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnMuhasebe.Shared.Contracts.Ayarlar;
using OnMuhasebe.Shared.Contracts.Kimlik;

namespace OnMuhasebe.Shared.Infrastructure.Modules;

public interface IModul
{
    string Ad { get; }

    void ServisleriEkle(IServiceCollection services, IConfiguration configuration);

    void EndpointleriEkle(IEndpointRouteBuilder endpoints);

    // Firmanın zorunlu yapabileceği isteğe bağlı alanlar (Ayarlar > Zorunlu alanlar).
    IReadOnlyList<ZorunluAlanTanimi> ZorunluAlanlar => [];

    // Rol ekranında verilebilen yetkiler.
    IReadOnlyList<YetkiKaynagi> YetkiKaynaklari => [];
}
