using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OnMuhasebe.Web.Api;
using OnMuhasebe.Web.Bilesenler;
using OnMuhasebe.Web.Kimlik;
using OnMuhasebe.Web.Tema;

namespace OnMuhasebe.Web;

public static class OrtakServisler
{
    public static IServiceCollection AddOrtakServisler(this IServiceCollection services, Uri apiAdresi)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddTransient<CerezHandler>();
        services.AddTransient<ApiYetkiHandler>();
        services.AddHttpClient(OturumYoneticisi.KimlikIstemcisi, c => c.BaseAddress = apiAdresi)
            .AddHttpMessageHandler<CerezHandler>();
        services.AddHttpClient(ApiIstemcisi.Ad, c => c.BaseAddress = apiAdresi)
            .AddHttpMessageHandler<ApiYetkiHandler>()
            .AddHttpMessageHandler<CerezHandler>();

        // Handler'lar ayrı DI kapsamında oluşturulduğu için oturum singleton olmalı; aksi halde farklı örnekler görürler.
        services.AddSingleton<OturumYoneticisi>();
        services.AddScoped<ApiIstemcisi>();
        services.AddScoped<OnayServisi>();
        services.AddScoped<ZorunluAlanServisi>();
        services.AddSingleton<TemaYoneticisi>();
        services.AddAuthorizationCore();
        services.AddCascadingAuthenticationState();
        services.AddScoped<AuthenticationStateProvider, OturumAuthStateProvider>();
        return services;
    }
}
