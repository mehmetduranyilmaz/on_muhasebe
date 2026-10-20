using OnMuhasebe.Shared.Domain.Abstractions;
using OnMuhasebe.Shared.Domain.Entities;

namespace OnMuhasebe.Modules.Yonetim.Domain;

// İşlem anahtarlı yazma isteğinin kaydı (tekrar koruması). Başarılı cevap 24 saat saklanır, sonra temizlenir.
[DenetimDisi]
internal sealed class IstekKaydi : FirmaEntity
{
    public const int SaklamaSaati = 24;

    private IstekKaydi() { }

    public Guid KullaniciId { get; private set; }
    public string Anahtar { get; private set; } = null!;

    // Metot + adres + gövdenin SHA-256 özeti; aynı anahtarın başka bir istekle kullanılmasını yakalar.
    public string Ozet { get; private set; } = null!;
    public bool Tamamlandi { get; private set; }
    public int? DurumKodu { get; private set; }
    public string? IcerikTuru { get; private set; }
    public string? Konum { get; private set; }
    public byte[]? Govde { get; private set; }

    public static IstekKaydi Olustur(Guid kullaniciId, string anahtar, string ozet) =>
        new() { KullaniciId = kullaniciId, Anahtar = anahtar, Ozet = ozet };
}
