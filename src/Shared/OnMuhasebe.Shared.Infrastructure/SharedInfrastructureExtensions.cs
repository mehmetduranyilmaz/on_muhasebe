using System.Globalization;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using OnMuhasebe.Shared.Infrastructure.Errors;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Shared.Infrastructure;

public static class SharedInfrastructureExtensions
{
    public static IServiceCollection AddSharedInfrastructure(this IServiceCollection services)
    {
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("tr-TR");

        // Zincirdeki ilk kural (ör. NotNull) başarısızsa devamı çalışmaz; eksik gönderilen liste/nesne alanında 500 yerine 400 döner.
        ValidatorOptions.Global.DefaultRuleLevelCascadeMode = CascadeMode.Stop;

        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddScoped<ArkaPlanBaglami>();
        services.AddScoped<IMevcutKullanici, HttpMevcutKullanici>();
        services.AddSingleton<FirmaKapsami>();
        services.AddSingleton<YetkiKatalogu>();
        services.AddScoped<IYetkiDenetimi, YetkiDenetimi>();
        services.AddScoped<DenetimInterceptor>();
        services.AddScoped<IslemKapsami>();
        services.AddScoped<IIslemKapsami>(sp => sp.GetRequiredService<IslemKapsami>());
        services.AddScoped<IslemKatilimInterceptor>();

        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            TurkceProblemBasliklari.Uygula(context);
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        });
        services.AddExceptionHandler<GlobalHataYakalayici>();

        return services;
    }
}
