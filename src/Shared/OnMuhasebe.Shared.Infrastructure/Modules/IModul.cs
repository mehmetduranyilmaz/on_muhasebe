using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnMuhasebe.Shared.Contracts.Ayarlar;

namespace OnMuhasebe.Shared.Infrastructure.Modules;

public interface IModul
{
    string Ad { get; }

    void ServisleriEkle(IServiceCollection services, IConfiguration configuration);

    void EndpointleriEkle(IEndpointRouteBuilder endpoints);

    // Firmanın zorunlu yapabileceği isteğe bağlı alanlar (Ayarlar > Zorunlu alanlar).
    IReadOnlyList<ZorunluAlanTanimi> ZorunluAlanlar => [];
}
