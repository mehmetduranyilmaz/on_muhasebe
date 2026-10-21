using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OnMuhasebe.Modules.Yonetim.Contracts;
using OnMuhasebe.Modules.Yonetim.Domain;
using OnMuhasebe.Modules.Yonetim.Infrastructure;
using OnMuhasebe.Modules.Yonetim.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure;
using OnMuhasebe.Shared.Infrastructure.Bildirimler;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Yonetim.Features;

// Şifre değiştirme (oturum açıkken) ve "şifremi unuttum" (e-postayla tek kullanımlık bağlantı).
// Şifre değişince kullanıcının tüm açık oturumları (refresh token) kapanır.
internal static partial class SifreIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        grup.MapPost("/sifre-degistir", DegistirAsync)
            .RequireAuthorization()
            .RequireRateLimiting(YonetimModulu.KimlikLimitPolitikasi)
            .Dogrula<SifreDegistirIstegi>();
        grup.MapPost("/sifre-sifirlama-istegi", SifirlamaIstegiAsync)
            .AllowAnonymous()
            .RequireRateLimiting(YonetimModulu.KimlikLimitPolitikasi)
            .Dogrula<SifreSifirlamaIstegi>();
        grup.MapPost("/sifre-sifirla", SifirlaAsync)
            .AllowAnonymous()
            .RequireRateLimiting(YonetimModulu.KimlikLimitPolitikasi)
            .Dogrula<SifreSifirlaIstegi>();
    }

    internal sealed class DegistirDogrulayici : AbstractValidator<SifreDegistirIstegi>
    {
        public DegistirDogrulayici()
        {
            RuleFor(x => x.MevcutSifre).NotEmpty();
            RuleFor(x => x.YeniSifre).Sifre()
                .NotEqual(x => x.MevcutSifre).WithMessage("Yeni şifre mevcut şifreden farklı olmalıdır.");
        }
    }

    internal sealed class SifirlamaIstegiDogrulayici : AbstractValidator<SifreSifirlamaIstegi>
    {
        public SifirlamaIstegiDogrulayici() => RuleFor(x => x.Eposta).Eposta();
    }

    internal sealed class SifirlaDogrulayici : AbstractValidator<SifreSifirlaIstegi>
    {
        public SifirlaDogrulayici()
        {
            RuleFor(x => x.Kod).NotEmpty().MaximumLength(100);
            RuleFor(x => x.YeniSifre).Sifre();
        }
    }

    private static async Task<IResult> DegistirAsync(
        SifreDegistirIstegi istek,
        IMevcutKullanici mevcutKullanici,
        YonetimDbContext db,
        IPasswordHasher<Kullanici> hasher,
        OturumServisi oturumServisi,
        TimeProvider zaman,
        CancellationToken cancellationToken)
    {
        var simdi = zaman.GetUtcNow().UtcDateTime;
        var kullanici = await db.Kullanicilar.SingleOrDefaultAsync(k => k.Id == mevcutKullanici.KullaniciId && k.AktifMi, cancellationToken);
        if (kullanici is null)
        {
            return YonetimHatalari.OturumGecersiz.Problem();
        }

        if (kullanici.KilitliMi(simdi))
        {
            return YonetimHatalari.HesapKilitli.Problem();
        }

        // Yanlış mevcut şifre girişteki gibi sayılır: ele geçirilmiş bir oturumla şifre denenemesin.
        if (hasher.VerifyHashedPassword(kullanici, kullanici.SifreOzeti, istek.MevcutSifre) == PasswordVerificationResult.Failed)
        {
            kullanici.HataliGirisYapildi(simdi);
            await db.SaveChangesAsync(cancellationToken);
            return YonetimHatalari.MevcutSifreHatali.Problem();
        }

        kullanici.SifreDegistir(hasher.HashPassword(kullanici, istek.YeniSifre));
        await OturumlariKapatAsync(db, kullanici.Id, simdi, cancellationToken);

        // Bu cihazda oturum yeni token'la sürer; diğer cihazlar yeniden giriş yapmalıdır.
        var sonuc = await oturumServisi.AcAsync(kullanici, mevcutKullanici.FirmaId, eskiToken: null, cancellationToken);
        return sonuc.HttpSonucu();
    }

    // Hesap olsun olmasın aynı yanıt döner: e-postanın kayıtlı olup olmadığı buradan öğrenilemez.
    private static async Task<IResult> SifirlamaIstegiAsync(
        SifreSifirlamaIstegi istek,
        YonetimDbContext db,
        IEpostaServisi epostaServisi,
        IConfiguration configuration,
        TimeProvider zaman,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var simdi = zaman.GetUtcNow().UtcDateTime;
        var eposta = Kullanici.EpostaNormalize(istek.Eposta);
        var kullanici = await db.Kullanicilar.AsNoTracking().SingleOrDefaultAsync(k => k.Eposta == eposta && k.AktifMi, cancellationToken);
        var yakinZamandaGonderildi = kullanici is not null && await db.SifreSifirlamaKodlari.AnyAsync(
            s => s.KullaniciId == kullanici.Id && s.OlusturmaTarihi > simdi - SifreSifirlamaKodu.YenidenGondermeAraligi, cancellationToken);
        if (kullanici is null || yakinZamandaGonderildi)
        {
            return TypedResults.Accepted((string?)null);
        }

        var kod = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        db.SifreSifirlamaKodlari.Add(SifreSifirlamaKodu.Olustur(kullanici.Id, TokenServisi.Ozetle(kod), simdi));
        await db.SaveChangesAsync(cancellationToken);

        var baglanti = $"{UygulamaAdresi.Oku(configuration)}sifre-sifirla?kod={kod}";
        var adSoyad = $"{kullanici.Ad} {kullanici.Soyad}";
        if (!await epostaServisi.GonderAsync(kullanici.Eposta, adSoyad, "Ön Muhasebe şifre sıfırlama", Html(adSoyad, baglanti), cancellationToken))
        {
            EpostaAyariYok(loggerFactory.CreateLogger(typeof(SifreIslemleri)), kullanici.Id);
        }

        return TypedResults.Accepted((string?)null);
    }

    private static async Task<IResult> SifirlaAsync(
        SifreSifirlaIstegi istek,
        YonetimDbContext db,
        IPasswordHasher<Kullanici> hasher,
        TimeProvider zaman,
        CancellationToken cancellationToken)
    {
        var simdi = zaman.GetUtcNow().UtcDateTime;
        var ozet = TokenServisi.Ozetle(istek.Kod.Trim());
        var kod = await db.SifreSifirlamaKodlari.SingleOrDefaultAsync(s => s.KodOzeti == ozet, cancellationToken);
        var kullanici = kod is null
            ? null
            : await db.Kullanicilar.SingleOrDefaultAsync(k => k.Id == kod.KullaniciId && k.AktifMi, cancellationToken);
        if (kod is null || kullanici is null || !kod.GecerliMi(simdi))
        {
            return YonetimHatalari.SifirlamaKoduGecersiz.Problem();
        }

        kullanici.SifreDegistir(hasher.HashPassword(kullanici, istek.YeniSifre));

        // Bu kod ve kullanıcının bekleyen diğer kodları da artık kullanılamaz.
        var bekleyenler = await db.SifreSifirlamaKodlari
            .Where(s => s.KullaniciId == kullanici.Id && s.KullanimTarihi == null)
            .ToListAsync(cancellationToken);
        bekleyenler.ForEach(s => s.Kullan(simdi));

        await OturumlariKapatAsync(db, kullanici.Id, simdi, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task OturumlariKapatAsync(YonetimDbContext db, Guid kullaniciId, DateTime simdi, CancellationToken cancellationToken)
    {
        var tokenlar = await db.RefreshTokenlar
            .Where(t => t.KullaniciId == kullaniciId && t.IptalTarihi == null && t.BitisTarihi > simdi)
            .ToListAsync(cancellationToken);
        tokenlar.ForEach(t => t.IptalEt(simdi));
        await db.SaveChangesAsync(cancellationToken);
    }

    internal static string Html(string adSoyad, string baglanti)
    {
        var ad = WebUtility.HtmlEncode(adSoyad);
        var adres = WebUtility.HtmlEncode(baglanti);
        return $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;max-width:560px;margin:auto;color:#1f2937">
              <h2 style="color:#1E3A5F">Şifre sıfırlama</h2>
              <p>Merhaba {ad},</p>
              <p>Hesabınız için şifre sıfırlama istendi. Yeni şifrenizi belirlemek için aşağıdaki düğmeye tıklayın.
                 Bağlantı <b>1 saat</b> geçerlidir ve yalnızca bir kez kullanılabilir.</p>
              <p style="margin:28px 0"><a href="{adres}" style="background:#1E3A5F;color:#fff;padding:12px 22px;border-radius:8px;text-decoration:none;font-weight:600">Yeni şifre belirle</a></p>
              <p style="font-size:13px;color:#6b7280">Bu isteği siz yapmadıysanız bu e-postayı yok sayabilirsiniz; şifreniz değişmez.</p>
            </div>
            """;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{KullaniciId} için şifre sıfırlama e-postası gönderilemedi: SMTP ayarı yok ya da kapalı")]
    private static partial void EpostaAyariYok(ILogger logger, Guid kullaniciId);
}
