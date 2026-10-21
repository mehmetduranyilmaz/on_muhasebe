using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace OnMuhasebe.Modules.Bildirim.Infrastructure;

// SMTP şifresini veritabanında AES-GCM ile saklar. Anahtar (32 bayt, base64) yapılandırmadan gelir: geliştirmede user-secrets,
// canlıda ortam değişkeni (Bildirim__SifrelemeAnahtari). Anahtar yoksa şifre kaydedilemez; veritabanı tek başına sızsa da şifre açılmaz.
internal sealed class SifreKoruyucu(IConfiguration configuration)
{
    public const string AyarAdi = "Bildirim:SifrelemeAnahtari";
    private const int NonceBoyu = 12;
    private const int EtiketBoyu = 16;

    public bool HazirMi => Anahtar() is not null;

    public string Sifrele(string metin)
    {
        var anahtar = Anahtar() ?? throw new InvalidOperationException($"'{AyarAdi}' tanımlı değil.");
        var acik = Encoding.UTF8.GetBytes(metin);
        var sonuc = new byte[NonceBoyu + EtiketBoyu + acik.Length];
        var nonce = sonuc.AsSpan(0, NonceBoyu);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(anahtar, EtiketBoyu);
        aes.Encrypt(nonce, acik, sonuc.AsSpan(NonceBoyu + EtiketBoyu), sonuc.AsSpan(NonceBoyu, EtiketBoyu));
        return Convert.ToBase64String(sonuc);
    }

    public string? Coz(string? sifreli)
    {
        if (sifreli is null || Anahtar() is not { } anahtar)
        {
            return null;
        }

        var veri = Convert.FromBase64String(sifreli);
        var acik = new byte[veri.Length - NonceBoyu - EtiketBoyu];
        using var aes = new AesGcm(anahtar, EtiketBoyu);
        aes.Decrypt(veri.AsSpan(0, NonceBoyu), veri.AsSpan(NonceBoyu + EtiketBoyu), veri.AsSpan(NonceBoyu, EtiketBoyu), acik);
        return Encoding.UTF8.GetString(acik);
    }

    private byte[]? Anahtar() =>
        configuration[AyarAdi] is { Length: > 0 } b64 && Convert.FromBase64String(b64) is { Length: 32 } anahtar ? anahtar : null;
}
