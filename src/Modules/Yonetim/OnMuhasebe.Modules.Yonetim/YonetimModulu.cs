using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OnMuhasebe.Modules.Yonetim.Domain;
using OnMuhasebe.Modules.Yonetim.Features;
using OnMuhasebe.Modules.Yonetim.Infrastructure;
using OnMuhasebe.Modules.Yonetim.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Modules;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Yonetim;

public sealed class YonetimModulu : IModul
{
    internal const string KimlikLimitPolitikasi = "kimlik";

    public string Ad => "Yonetim";

    public void ServisleriEkle(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModulDbContext<YonetimDbContext>(configuration);
        services.AddValidatorsFromAssemblyContaining<YonetimModulu>(includeInternalTypes: true);
        services.AddHybridCache();

        services.AddOptions<JwtAyarlari>()
            .BindConfiguration(JwtAyarlari.Bolum)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtAyarlari>>((options, ayarlar) =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = ayarlar.Value.Yayinci,
                    ValidAudience = ayarlar.Value.Hedef,
                    IssuerSigningKey = TokenServisi.ImzaAnahtari(ayarlar.Value),
                    NameClaimType = ClaimTipleri.Ad,
                    RoleClaimType = ClaimTipleri.Rol,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });
        services.AddAuthorization();

        // Kaba kuvvet denemelerine karşı IP başına dakikalık istek sınırı. Ters vekil arkasında ForwardedHeaders yapılandırılmalı.
        var dakikalikLimit = configuration.GetValue("Kimlik:DakikalikIstekLimiti", 10);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(KimlikLimitPolitikasi, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "bilinmiyor",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = dakikalikLimit, Window = TimeSpan.FromMinutes(1) }));
        });

        services.AddSingleton<IPasswordHasher<Kullanici>, PasswordHasher<Kullanici>>();
        services.AddSingleton<TokenServisi>();
        services.AddScoped<OturumServisi>();
        services.AddScoped<ILisansSorgusu, LisansSorgusu>();
        services.AddHostedService<SistemYoneticisiTohumlayici>();
    }

    public void EndpointleriEkle(IEndpointRouteBuilder endpoints)
    {
        var grup = endpoints.MapGroup("/api/yonetim").WithTags("Yonetim");

        Kayit.Map(grup);
        Giris.Map(grup);
        Yenile.Map(grup);
        Cikis.Map(grup);
        FirmaDegistir.Map(grup);
        Ben.Map(grup);
        KullaniciIslemleri.Map(grup);
        SistemIslemleri.Map(grup);
    }
}
