using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OnMuhasebe.Modules.Yonetim.Domain;
using OnMuhasebe.Shared.Infrastructure;
using OnMuhasebe.Shared.Infrastructure.Bildirimler;

namespace OnMuhasebe.Modules.Yonetim.Infrastructure.Kimlik;

internal static class TekKullanimlikKod
{
    // 256 bit rastgele kod; veritabanına yalnızca özeti yazılır.
    public static (string Kod, string Ozet) Uret()
    {
        var kod = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        return (kod, TokenServisi.Ozetle(kod));
    }
}

// Hesapla ilgili e-postalar. Kodlar bağlantının # kısmında taşınır: tarayıcı bu kısmı sunucuya göndermez, böylece kod
// sunucu günlüklerine ve Referer başlığına düşmez. Gönderilemeyen e-posta isteği bozmaz, günlüğe yazılır.
internal sealed partial class KimlikPostasi(IEpostaServisi epostaServisi, IConfiguration configuration, ILogger<KimlikPostasi> logger)
{
    public Task DogrulamaAsync(string alici, string adSoyad, string kod, CancellationToken cancellationToken) =>
        GonderAsync(alici, adSoyad, "Ön Muhasebe: e-posta adresinizi doğrulayın", "Hoş geldiniz",
            ["Hesabınızı etkinleştirmek için e-posta adresinizi doğrulayın. Bağlantı <b>24 saat</b> geçerlidir."],
            ("E-postamı doğrula", $"eposta-dogrula#kod={kod}"),
            "Bu kaydı siz yapmadıysanız bu e-postayı yok sayabilirsiniz; hesap etkinleşmez.", cancellationToken);

    public Task ZatenKayitliAsync(string alici, string adSoyad, CancellationToken cancellationToken) =>
        GonderAsync(alici, adSoyad, "Ön Muhasebe: bu adresle zaten hesabınız var", "Zaten bir hesabınız var",
            ["Bu e-posta adresiyle yeni kayıt denendi, ancak adresinize ait bir hesap zaten bulunuyor. Giriş yapabilir ya da şifrenizi unuttuysanız sıfırlayabilirsiniz."],
            ("Giriş yap", "giris"),
            "Bu denemeyi siz yapmadıysanız bir şey yapmanız gerekmez; hesabınızda değişiklik olmadı.", cancellationToken);

    public Task SifreSifirlamaAsync(string alici, string adSoyad, string kod, CancellationToken cancellationToken) =>
        GonderAsync(alici, adSoyad, "Ön Muhasebe şifre sıfırlama", "Şifre sıfırlama",
            ["Hesabınız için şifre sıfırlama istendi. Yeni şifrenizi belirlemek için aşağıdaki düğmeye tıklayın. Bağlantı <b>1 saat</b> geçerlidir ve yalnızca bir kez kullanılabilir."],
            ("Yeni şifre belirle", $"sifre-sifirla#kod={kod}"),
            "Bu isteği siz yapmadıysanız bu e-postayı yok sayabilirsiniz; şifreniz değişmez.", cancellationToken);

    public Task DavetAsync(string alici, string firmaUnvani, string davetEden, string kod, CancellationToken cancellationToken) =>
        GonderAsync(alici, null, $"Ön Muhasebe: {firmaUnvani} sizi davet etti", "Firmaya davet",
            [$"<b>{WebUtility.HtmlEncode(davetEden)}</b>, sizi Ön Muhasebe'de <b>{WebUtility.HtmlEncode(firmaUnvani)}</b> firmasına davet etti. Daveti kabul etmek için aşağıdaki düğmeye tıklayın. Bağlantı <b>7 gün</b> geçerlidir."],
            ("Daveti görüntüle", $"davet#kod={kod}"),
            "Bu daveti beklemiyorsanız e-postayı yok sayabilirsiniz; siz kabul etmedikçe firmaya eklenmezsiniz.", cancellationToken);

    // paragraflar güvenilir HTML'dir (kullanıcı verisi önceden kodlanır); ad ve bağlantı burada kodlanır.
    private async Task GonderAsync(string alici, string? adSoyad, string konu, string baslik, string[] paragraflar,
        (string Metin, string Yol) dugme, string dipnot, CancellationToken cancellationToken)
    {
        var adres = WebUtility.HtmlEncode(UygulamaAdresi.Oku(configuration) + dugme.Yol);
        var selam = adSoyad is null ? "Merhaba," : $"Merhaba {WebUtility.HtmlEncode(adSoyad)},";
        var html = $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;max-width:560px;margin:auto;color:#1f2937">
              <h2 style="color:#1E3A5F">{WebUtility.HtmlEncode(baslik)}</h2>
              <p>{selam}</p>
              {string.Concat(paragraflar.Select(p => $"<p>{p}</p>"))}
              <p style="margin:28px 0"><a href="{adres}" style="background:#1E3A5F;color:#fff;padding:12px 22px;border-radius:8px;text-decoration:none;font-weight:600">{WebUtility.HtmlEncode(dugme.Metin)}</a></p>
              <p style="font-size:13px;color:#6b7280">{WebUtility.HtmlEncode(dipnot)}</p>
            </div>
            """;
        try
        {
            if (!await epostaServisi.GonderAsync(alici, adSoyad ?? alici, konu, html, cancellationToken))
            {
                SmtpYok(logger, konu);
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Gonderilemedi(logger, konu, e);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Hesap e-postası gönderilemedi ({Konu}): SMTP ayarı yok ya da kapalı")]
    private static partial void SmtpYok(ILogger logger, string konu);

    [LoggerMessage(Level = LogLevel.Error, Message = "Hesap e-postası gönderilemedi ({Konu})")]
    private static partial void Gonderilemedi(ILogger logger, string konu, Exception exception);
}

internal static class KullaniciKodlari
{
    // Aynı amaçla son iki dakikada kod gönderildiyse yenisi gönderilmez.
    public static async Task<bool> GonderilebilirMiAsync(
        YonetimDbContext db, Guid kullaniciId, KodAmaci amac, DateTime simdi, CancellationToken cancellationToken) =>
        !await db.KullaniciKodlari.AnyAsync(
            k => k.KullaniciId == kullaniciId && k.Amac == amac && k.OlusturmaTarihi > simdi - KullaniciKodu.YenidenGondermeAraligi,
            cancellationToken);
}
