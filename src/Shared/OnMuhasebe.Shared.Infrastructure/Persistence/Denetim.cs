using System.Globalization;
using System.Text.Json;
using OnMuhasebe.Shared.Contracts.Ayarlar;
using OnMuhasebe.Shared.Domain.Entities;

namespace OnMuhasebe.Shared.Infrastructure.Persistence;

// Bir kaydın güncellenmesi ya da silinmesinin izi: kim, ne zaman, hangi alan neyken ne oldu. Her modül şemasında ayrı tablodadır
// (aynı transaction'da yazılır, satır güvenliği kapsamındadır). Kaydın eklenmesi yazılmaz: oluşturan ve tarih zaten kaydın üzerindedir,
// hareketler de değiştirilemediğinden geçmişi şişirmekten başka işe yaramaz.
public sealed class DegisiklikKaydi : FirmaEntity
{
    private DegisiklikKaydi() { }

    private DegisiklikKaydi(Guid firmaId) : base(firmaId) { }

    public Guid? KullaniciId { get; private set; }
    public string? KullaniciAdi { get; private set; }
    public DateTime Zaman { get; private set; }
    public string Tablo { get; private set; } = null!;
    public Guid KayitId { get; private set; }

    // Alt satırın (fatura satırı, stok birimi, evrak hareketi) bağlı olduğu kayıt; geçmiş üst kayıtta birlikte listelenir.
    public Guid? UstKayitId { get; private set; }
    public DegisiklikIslemi Islem { get; private set; }

    // [{ "Alan": ..., "Eski": ..., "Yeni": ... }] JSON'u.
    public string Alanlar { get; private set; } = null!;

    public static DegisiklikKaydi Olustur(
        Guid firmaId, Guid? kullaniciId, string? kullaniciAdi, DateTime zaman, string tablo, Guid kayitId, Guid? ustKayitId, DegisiklikIslemi islem,
        IReadOnlyList<AlanDegisikligi> alanlar) => new(firmaId)
    {
        KullaniciId = kullaniciId,
        KullaniciAdi = kullaniciAdi,
        Zaman = zaman,
        Tablo = tablo,
        KayitId = kayitId,
        UstKayitId = ustKayitId,
        Islem = islem,
        Alanlar = JsonSerializer.Serialize(alanlar)
    };
}

public static class DegisiklikHesaplayici
{
    private const int AzamiMetin = 500;

    // Değeri değişen alanlar; teknik alanlar (sürüm, güncelleme izi) dışarıda kalır.
    public static IReadOnlyList<AlanDegisikligi> Farklar(IEnumerable<(string Alan, object? Eski, object? Yeni)> alanlar, ISet<string> haricler) =>
        alanlar
            .Where(a => !haricler.Contains(a.Alan))
            .Select(a => new AlanDegisikligi(a.Alan, Metin(a.Eski), Metin(a.Yeni)))
            .Where(a => a.Eski != a.Yeni)
            .ToList();

    public static string? Metin(object? deger) => deger switch
    {
        null => null,
        string s => s.Length > AzamiMetin ? s[..AzamiMetin] + "…" : s,
        byte[] => "[ikili veri]",
        // Ölçek farkı (10,50 ile 10,5) değişiklik sayılmasın.
        decimal m => m.ToString("0.##########", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime t => t.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        _ => Kisalt(JsonSerializer.Serialize(deger))
    };

    private static string Kisalt(string s) => s.Length > AzamiMetin ? s[..AzamiMetin] + "…" : s;
}

// Firmanın kilitli dönemi: bu tarih ve öncesindeki hareketler değiştirilemez (Yönetim modülü sağlar, önbellekli).
public interface IDonemKilidiSorgusu
{
    Task<DateOnly?> KilitTarihiAsync(Guid firmaId, CancellationToken cancellationToken = default);
}
