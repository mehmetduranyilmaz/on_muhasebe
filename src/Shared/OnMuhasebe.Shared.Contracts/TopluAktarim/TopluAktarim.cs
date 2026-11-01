using System.Text.Json.Serialization;

namespace OnMuhasebe.Shared.Contracts.TopluAktarim;

[JsonConverter(typeof(JsonStringEnumConverter<AktarimSatirDurumu>))]
public enum AktarimSatirDurumu
{
    Eklenecek,
    // Kod, vergi no, barkod ya da ad aynı olan kayıt zaten var ya da dosyada daha önce geçti: atlanır, mevcut kayıt değişmez.
    Mukerrer,
    Hatali
}

// Acilis: cari için işaretli net bakiye (artı borç, eksi alacak), stok için açılış miktarı.
public sealed record AktarimSatiriOzeti(int SatirNo, string? Kod, string Ad, AktarimSatirDurumu Durum, IReadOnlyList<string> Mesajlar, decimal? Acilis = null);

// Sayılar dosyanın tamamı içindir; Satirlar en çok AzamiSatirOzeti satır taşır (önce hatalılar, sonra mükerrerler).
public sealed record TopluAktarimSonucu(
    bool KaydedildiMi, int Toplam, int Eklenecek, int Mukerrer, int Hatali, IReadOnlyList<string> TaninmayanSutunlar, IReadOnlyList<AktarimSatiriOzeti> Satirlar)
{
    public const int AzamiSatirOzeti = 500;
}
