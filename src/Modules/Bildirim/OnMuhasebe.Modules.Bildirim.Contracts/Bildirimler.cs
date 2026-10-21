using System.Text.Json.Serialization;
using OnMuhasebe.Shared.Contracts.Bildirimler;

namespace OnMuhasebe.Modules.Bildirim.Contracts;

public sealed record BildirimOzeti(Guid Id, BildirimTuru Tur, string Baslik, string Metin, string? Baglanti, BildirimOnemi Onem, bool OkunduMu, DateTime Tarih);

public sealed record BildirimSayaci(int Okunmamis);

[JsonConverter(typeof(JsonStringEnumConverter<EpostaSikligi>))]
public enum EpostaSikligi
{
    Kapali,
    Aninda,
    GunlukOzet
}

// Kullanıcının kendi tercihi. Varsayılan: yönetici her türü alır (e-posta günlük özet), kullanıcı rolü almaz.
public sealed record BildirimTercihleri(bool BildirimAlir, IReadOnlyList<BildirimTuru> KapaliTurler, EpostaSikligi Eposta);

// Firma ayarı (yönetici): vade kaç gün önce hatırlatılsın ve firmada hiç üretilmeyecek türler.
public sealed record BildirimAyarlari(int VadeOncesiGun, IReadOnlyList<BildirimTuru> KapaliTurler);

[JsonConverter(typeof(JsonStringEnumConverter<SmtpGuvenligi>))]
public enum SmtpGuvenligi
{
    Yok,
    StartTls,
    Ssl
}

// Sistem geneli SMTP ayarı (sistem yöneticisi). Şifre yalnızca yazılırken gönderilir, okunurken dönmez; boş gönderilirse
// kayıtlı şifre korunur.
public sealed record EpostaAyarlari(
    bool AktifMi,
    string? Sunucu,
    int Port,
    SmtpGuvenligi Guvenlik,
    string? KullaniciAdi,
    string? Sifre,
    bool SifreKayitliMi,
    string? GonderenAdres,
    string? GonderenAd);

public sealed record EpostaTestIstegi(string Alici);

public sealed record BildirimCalismaSonucu(int Firma, int Uretilen, int Eposta);
