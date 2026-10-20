using Microsoft.Extensions.DependencyInjection;
using OnMuhasebe.Shared.Infrastructure.Errors;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Shared.Infrastructure;

public static class SharedInfrastructureExtensions
{
    public static IServiceCollection AddSharedInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddScoped<IMevcutKullanici, HttpMevcutKullanici>();
        services.AddScoped<DenetimInterceptor>();

        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
        services.AddExceptionHandler<GlobalHataYakalayici>();

        return services;
    }
}
