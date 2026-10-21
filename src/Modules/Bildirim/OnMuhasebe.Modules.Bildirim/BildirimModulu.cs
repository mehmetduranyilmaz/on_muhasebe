using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnMuhasebe.Modules.Bildirim.Features;
using OnMuhasebe.Modules.Bildirim.Infrastructure;
using OnMuhasebe.Shared.Infrastructure.Bildirimler;
using OnMuhasebe.Shared.Infrastructure.Modules;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Bildirim;

// Lisanssız çekirdek modül: uygulama içi bildirim ve e-posta. Bildirimleri diğer modüllerin IBildirimKaynagi uygulamaları üretir.
public sealed class BildirimModulu : IModul
{
    public string Ad => "Bildirim";

    public void ServisleriEkle(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModulDbContext<BildirimDbContext>(configuration);
        services.AddValidatorsFromAssemblyContaining<BildirimModulu>(includeInternalTypes: true);
        services.AddSingleton<SifreKoruyucu>();
        services.AddScoped<IEpostaGonderici, SmtpEpostaGonderici>();
        services.AddScoped<EpostaPostacisi>();
        services.AddScoped<IEpostaServisi, SistemEpostaServisi>();
        services.AddScoped<BildirimMotoru>();
        services.AddSingleton<BildirimCalistirici>();
        services.AddSingleton<IBildirimTetikleyici>(sp => sp.GetRequiredService<BildirimCalistirici>());
        services.AddHostedService<BildirimZamanlayici>();
    }

    public void EndpointleriEkle(IEndpointRouteBuilder endpoints) =>
        BildirimIslemleri.Map(endpoints.MapGroup("/api/bildirim").WithTags("Bildirim"));
}
