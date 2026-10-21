using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OnMuhasebe.Shared.Infrastructure.Guvenlik;

public static class GuvenlikExtensions
{
    // Ekstre dosyası gibi yüklemeler için genel sınırdan geniş gövde izni (uçta RequestSizeLimit ile verilir).
    public const long AzamiYuklemeBoyutu = 6 * 1024 * 1024;

    // Genel istek sınırları:
    // - Oturum açmış kullanıcı başına ve oturumsuz istekte IP başına dakikalık hız sınırı (giriş/kayıt ayrıca daha sıkı sınırlıdır).
    // - İstek gövdesi varsayılan 1 MB; dosya yükleyen uçlar kendi sınırını açar.
    public static IServiceCollection AddGuvenlik(this IServiceCollection services, IConfiguration configuration)
    {
        var kullaniciLimiti = configuration.GetValue("Guvenlik:KullaniciDakikalikIstek", 600);
        var ipLimiti = configuration.GetValue("Guvenlik:OturumsuzDakikalikIstek", 120);
        var azamiGovde = configuration.GetValue<long>("Guvenlik:AzamiGovdeBayt", 1024 * 1024);

        services.Configure<RateLimiterOptions>(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (baglam, _) =>
            {
                if (baglam.Lease.TryGetMetadata(MetadataName.RetryAfter, out var sure))
                {
                    baglam.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(sure.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                return ValueTask.CompletedTask;
            };
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
            {
                if (http.Request.Path.StartsWithSegments("/health"))
                {
                    return RateLimitPartition.GetNoLimiter("saglik");
                }

                var kullanici = http.User.FindFirst("sub")?.Value;
                return kullanici is not null
                    ? RateLimitPartition.GetFixedWindowLimiter($"k:{kullanici}",
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = kullaniciLimiti, Window = TimeSpan.FromMinutes(1) })
                    : RateLimitPartition.GetFixedWindowLimiter($"ip:{http.Connection.RemoteIpAddress}",
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = ipLimiti, Window = TimeSpan.FromMinutes(1) });
            });
        });

        // Ters vekil (Caddy) arkasında istemci IP'si (hız sınırı) ve https bilgisi X-Forwarded-* başlıklarından okunur.
        // Yalnızca API'ye vekil dışında erişilemiyorsa açılmalı (deploy/docker-compose.yml'de API portu dışarı açılmaz).
        if (configuration.GetValue("Guvenlik:VekilArkasinda", false))
        {
            services.Configure<ForwardedHeadersOptions>(o =>
            {
                o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                o.ForwardLimit = 1;
                o.KnownIPNetworks.Clear();
                o.KnownProxies.Clear();
            });
        }

        services.Configure<KestrelServerOptions>(o => o.Limits.MaxRequestBodySize = azamiGovde);
        services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = AzamiYuklemeBoyutu);
        return services;
    }

    // Uç meta verisindeki gövde sınırını (ör. ekstre yükleme) gövde okunmadan önce uygular; Minimal API bağlama işleyiciden önce okur.
    public static IApplicationBuilder UseUcGovdeSiniri(this IApplicationBuilder app) =>
        app.Use(async (http, sonraki) =>
        {
            if (http.GetEndpoint()?.Metadata.GetMetadata<IRequestSizeLimitMetadata>() is { MaxRequestBodySize: var sinir }
                && http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } ozellik)
            {
                ozellik.MaxRequestBodySize = sinir;
            }

            await sonraki(http);
        });

    // API cevapları JSON'dur; tarayıcıya hiçbir içerik çalıştırma, çerçeveye gömme ya da önbellekleme izni verilmez.
    // HSTS burada verilmez: TLS'i ters vekil (Caddy) sonlandırır ve tüm yanıtlara tek, uzun süreli HSTS başlığını o ekler.
    public static IApplicationBuilder UseGuvenlikBasliklari(this IApplicationBuilder app) =>
        app.Use(async (http, sonraki) =>
        {
            http.Response.OnStarting(() =>
            {
                var b = http.Response.Headers;
                b.XContentTypeOptions = "nosniff";
                b.XFrameOptions = "DENY";
                b.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
                b["Referrer-Policy"] = "no-referrer";
                b["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
                b["Cross-Origin-Opener-Policy"] = "same-origin";

                // Cari, fatura gibi kişisel/finansal veriler tarayıcı ya da ara sunucu önbelleğinde kalmasın.
                if (http.Request.Path.StartsWithSegments("/api"))
                {
                    b.CacheControl = "no-store";
                    b.Pragma = "no-cache";
                }

                return Task.CompletedTask;
            });
            await sonraki(http);
        });
}

// Dosya yükleyen uca genel gövde sınırından geniş izin: .WithMetadata(new YuklemeBoyutu(...)).
public sealed class YuklemeBoyutu(long boyut) : IRequestSizeLimitMetadata
{
    public long? MaxRequestBodySize => boyut;
}
