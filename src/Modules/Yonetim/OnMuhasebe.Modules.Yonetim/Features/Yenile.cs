using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OnMuhasebe.Modules.Yonetim.Domain;
using OnMuhasebe.Modules.Yonetim.Infrastructure;
using OnMuhasebe.Modules.Yonetim.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Results;

namespace OnMuhasebe.Modules.Yonetim.Features;

internal static partial class Yenile
{
    // Aynı anda iki sekmeden yenileme yapılabilir; bu süre içindeki tekrar kullanım saldırı sayılmaz.
    private static readonly TimeSpan _esZamanlilikToleransi = TimeSpan.FromSeconds(30);

    public static void Map(RouteGroupBuilder grup) =>
        grup.MapPost("/yenile", IsleAsync)
            .AllowAnonymous()
            .RequireRateLimiting(YonetimModulu.YenilemeLimitPolitikasi);

    private static async Task<IResult> IsleAsync(
        HttpContext http,
        YonetimDbContext db,
        OturumServisi oturumServisi,
        TimeProvider zaman,
        ILogger<RefreshToken> logger,
        CancellationToken cancellationToken)
    {
        var token = YenilemeCerezi.Oku(http.Request);
        if (string.IsNullOrEmpty(token))
        {
            return YonetimHatalari.OturumGecersiz.Problem();
        }

        var simdi = zaman.GetUtcNow().UtcDateTime;
        var ozet = TokenServisi.Ozetle(token);
        var kayit = await db.RefreshTokenlar.SingleOrDefaultAsync(t => t.TokenOzeti == ozet, cancellationToken);

        if (kayit is null || kayit.SuresiDolduMu(simdi))
        {
            YenilemeCerezi.Sil(http.Response);
            return YonetimHatalari.OturumGecersiz.Problem();
        }

        if (kayit.IptalTarihi is not null)
        {
            if (simdi - kayit.IptalTarihi > _esZamanlilikToleransi)
            {
                // İptal edilmiş token tekrar kullanıldı: token çalınmış olabilir, kullanıcının tüm oturumları kapatılır.
                TokenYenidenKullanildi(logger, kayit.KullaniciId);
                await db.RefreshTokenlar
                    .Where(t => t.KullaniciId == kayit.KullaniciId && t.IptalTarihi == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.IptalTarihi, simdi), cancellationToken);
                YenilemeCerezi.Sil(http.Response);
            }

            return YonetimHatalari.OturumGecersiz.Problem();
        }

        var kullanici = await db.Kullanicilar.SingleOrDefaultAsync(k => k.Id == kayit.KullaniciId, cancellationToken);
        if (kullanici is null || !kullanici.AktifMi)
        {
            YenilemeCerezi.Sil(http.Response);
            return YonetimHatalari.OturumGecersiz.Problem();
        }

        var sonuc = await oturumServisi.AcAsync(kullanici, kayit.FirmaId, kayit, cancellationToken);
        return sonuc.HttpSonucu();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "İptal edilmiş refresh token tekrar kullanıldı; kullanıcının tüm oturumları kapatıldı. KullaniciId: {KullaniciId}")]
    private static partial void TokenYenidenKullanildi(ILogger logger, Guid kullaniciId);
}
