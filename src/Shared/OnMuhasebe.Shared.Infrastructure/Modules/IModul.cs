using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OnMuhasebe.Shared.Infrastructure.Modules;

public interface IModul
{
    string Ad { get; }

    void ServisleriEkle(IServiceCollection services, IConfiguration configuration);

    void EndpointleriEkle(IEndpointRouteBuilder endpoints);
}
