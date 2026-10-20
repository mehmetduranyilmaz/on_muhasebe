using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OnMuhasebe.Shared.Infrastructure.Modules;

public static class ModulExtensions
{
    public static IServiceCollection AddModuller(this IServiceCollection services, IConfiguration configuration, params IModul[] moduller)
    {
        foreach (var modul in moduller)
        {
            modul.ServisleriEkle(services, configuration);
            services.AddSingleton(modul);
        }

        return services;
    }

    public static IEndpointRouteBuilder MapModuller(this IEndpointRouteBuilder endpoints)
    {
        foreach (var modul in endpoints.ServiceProvider.GetServices<IModul>())
        {
            modul.EndpointleriEkle(endpoints);
        }

        return endpoints;
    }
}
