using System.Globalization;
using System.Text.Json.Serialization;

namespace OnMuhasebe.Modules.Fatura.Contracts;

// Satış/alış özeti, ürün bazlı alış-satış toplamları ve sipariş durum raporu. Tutarlar TL'dir (dövizli belgede belge kuruyla),
// yalnızca onaylı belgeler sayılır; iadeler eksi yazılarak düşülür.

[JsonConverter(typeof(JsonStringEnumConverter<TicariTaraf>))]
public enum TicariTaraf
{
    Satis,
    Alis
}

[JsonConverter(typeof(JsonStringEnumConverter<FaturaOzetKirilimi>))]
public enum FaturaOzetKirilimi
{
    Cari,
    Ay,
    Fatura
}

// Bir faturanın özete giren tutarları; iade faturasında tutarlar eksidir.
public sealed record FaturaOzetKalemi(
    Guid FaturaId,
    string? Numara,
    DateOnly Tarih,
    Guid? CariHesapId,
    string CariUnvan,
    bool IadeMi,
    decimal BrutTl,
    decimal IskontoTl,
    decimal MatrahTl,
    decimal KdvTl,
    decimal TevkifatTl,
    decimal GenelToplamTl);

// Anahtar: cari ünvanı, "Ekim 2026" ya da fatura numarası. Id cari ya da fatura kimliğidir (ay kırılımında boş).
public sealed record FaturaOzetSatiri(
    string Anahtar,
    string? Ek,
    Guid? Id,
    int FaturaSayisi,
    int IadeSayisi,
    decimal BrutTl,
    decimal IskontoTl,
    decimal MatrahTl,
    decimal KdvTl,
    decimal TevkifatTl,
    decimal GenelToplamTl);

public sealed record SatisAlisOzetRaporu(
    DateOnly Baslangic,
    DateOnly Bitis,
    TicariTaraf Taraf,
    FaturaOzetKirilimi Kirilim,
    FaturaOzetSatiri Toplam,
    IReadOnlyList<FaturaOzetSatiri> Satirlar);

// Fatura satırının ürün toplamına giren kısmı; TemelMiktar kartın temel birimindedir. İadede miktar ve tutarlar eksidir.
public sealed record UrunToplamKalemi(
    Guid? StokKartiId,
    string? StokKodu,
    string Ad,
    string Birim,
    TicariTaraf Taraf,
    decimal TemelMiktar,
    decimal IskontoTl,
    decimal MatrahTl,
    decimal KdvTl);

// Birim boşsa kartsız satırlar farklı birimlerle girilmiştir; miktar toplamı anlamlı değildir.
public sealed record UrunToplamSatiri(
    Guid? StokKartiId,
    string? StokKodu,
    string Ad,
    string? Birim,
    decimal SatisMiktari,
    decimal SatisIskontoTl,
    decimal SatisMatrahTl,
    decimal SatisKdvTl,
    decimal AlisMiktari,
    decimal AlisIskontoTl,
    decimal AlisMatrahTl,
    decimal AlisKdvTl);

public sealed record UrunToplamlariRaporu(
    DateOnly Baslangic,
    DateOnly Bitis,
    decimal SatisMatrahTl,
    decimal AlisMatrahTl,
    IReadOnlyList<UrunToplamSatiri> Satirlar);

[JsonConverter(typeof(JsonStringEnumConverter<SiparisOdemeDurumu>))]
public enum SiparisOdemeDurumu
{
    Faturalanmadi,
    Odenmedi,
    KismenOdendi,
    Odendi
}

// Faturalanan ve kalan tutarlar siparişe bağlı fatura satırlarından gelir (irsaliyeli ya da doğrudan). Bir fatura birden çok
// siparişi karşılıyorsa kalan bakiyesi satır tutarı oranında paylaştırılır.
public sealed record SiparisDurumSatiri(
    Guid Id,
    TeklifSiparisTuru Tur,
    string? Numara,
    DateOnly Tarih,
    DateOnly? TerminTarihi,
    Guid? CariHesapId,
    string CariUnvan,
    TeklifSiparisDurumu Durum,
    KarsilamaDurumu? Karsilama,
    decimal KarsilamaYuzde,
    string ParaBirimi,
    decimal GenelToplam,
    decimal FaturalananTl,
    decimal KalanTl,
    SiparisOdemeDurumu OdemeDurumu,
    int? GecikmeGun);

public sealed record SiparisDurumRaporu(
    DateOnly Baslangic,
    DateOnly Bitis,
    int SiparisSayisi,
    int GecikenSayisi,
    decimal FaturalananTl,
    decimal KalanTl,
    IReadOnlyList<SiparisDurumSatiri> Satirlar);

public static class TicariRaporHesaplayici
{
    private static readonly CultureInfo _tr = CultureInfo.GetCultureInfo("tr-TR");

    public static SatisAlisOzetRaporu FaturaOzeti(DateOnly bas, DateOnly son, TicariTaraf taraf, FaturaOzetKirilimi kirilim, IReadOnlyList<FaturaOzetKalemi> kalemler)
    {
        var gruplar = kirilim switch
        {
            FaturaOzetKirilimi.Cari => kalemler.GroupBy(k => (Anahtar: k.CariUnvan, Ek: (string?)null, Id: k.CariHesapId, Sira: k.CariUnvan))
                .OrderByDescending(g => g.Sum(k => k.MatrahTl)),
            FaturaOzetKirilimi.Ay => kalemler.GroupBy(k => (Anahtar: k.Tarih.ToString("MMMM yyyy", _tr), Ek: (string?)null, Id: (Guid?)null,
                    Sira: $"{k.Tarih:yyyy-MM}"))
                .OrderBy(g => g.Key.Sira),
            _ => kalemler.GroupBy(k => (Anahtar: k.Numara ?? "Numarasız", Ek: (string?)k.CariUnvan, Id: (Guid?)k.FaturaId, Sira: $"{k.Tarih:yyyy-MM-dd}"))
                .OrderBy(g => g.Key.Sira).ThenBy(g => g.Key.Anahtar)
        };

        var satirlar = gruplar.Select(g => Topla(g.Key.Anahtar, g.Key.Ek, g.Key.Id, g)).ToList();
        return new SatisAlisOzetRaporu(bas, son, taraf, kirilim, Topla("Toplam", null, null, kalemler), satirlar);
    }

    private static FaturaOzetSatiri Topla(string anahtar, string? ek, Guid? id, IEnumerable<FaturaOzetKalemi> kalemler)
    {
        var liste = kalemler as IReadOnlyCollection<FaturaOzetKalemi> ?? kalemler.ToList();
        return new FaturaOzetSatiri(anahtar, ek, id, liste.Count(k => !k.IadeMi), liste.Count(k => k.IadeMi),
            liste.Sum(k => k.BrutTl), liste.Sum(k => k.IskontoTl), liste.Sum(k => k.MatrahTl), liste.Sum(k => k.KdvTl),
            liste.Sum(k => k.TevkifatTl), liste.Sum(k => k.GenelToplamTl));
    }

    // Kartlı satırlar karta göre, kartsız (serbest) satırlar adına göre (büyük/küçük harf ve boşluk farkı gözetilmeden) toplanır.
    public static UrunToplamlariRaporu UrunToplamlari(DateOnly bas, DateOnly son, IReadOnlyList<UrunToplamKalemi> kalemler)
    {
        var satirlar = kalemler
            .GroupBy(k => k.StokKartiId is { } id ? id.ToString() : "serbest:" + string.Join(' ', k.Ad.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLower(_tr))
            .Select(g =>
            {
                var ilk = g.First();
                var birimler = g.Select(k => k.Birim).Distinct().ToList();
                var satis = g.Where(k => k.Taraf == TicariTaraf.Satis).ToList();
                var alis = g.Where(k => k.Taraf == TicariTaraf.Alis).ToList();
                return new UrunToplamSatiri(ilk.StokKartiId, ilk.StokKodu, ilk.Ad, birimler.Count == 1 ? birimler[0] : null,
                    satis.Sum(k => k.TemelMiktar), satis.Sum(k => k.IskontoTl), satis.Sum(k => k.MatrahTl), satis.Sum(k => k.KdvTl),
                    alis.Sum(k => k.TemelMiktar), alis.Sum(k => k.IskontoTl), alis.Sum(k => k.MatrahTl), alis.Sum(k => k.KdvTl));
            })
            .OrderByDescending(s => s.SatisMatrahTl).ThenByDescending(s => s.AlisMatrahTl).ThenBy(s => s.Ad, StringComparer.Create(_tr, true))
            .ToList();

        return new UrunToplamlariRaporu(bas, son, satirlar.Sum(s => s.SatisMatrahTl), satirlar.Sum(s => s.AlisMatrahTl), satirlar);
    }

    // Karşılama oranı satır matrahıyla ağırlıklandırılır: 100 TL'lik satırın tamamı ile 10.000 TL'lik satırın yarısı aynı sayılmaz.
    public static decimal KarsilamaYuzdesi(IEnumerable<(decimal Miktar, decimal Karsilanan, decimal Matrah)> satirlar)
    {
        var liste = satirlar.ToList();
        var toplam = liste.Sum(s => s.Matrah);
        if (toplam <= 0m)
        {
            var miktar = liste.Sum(s => s.Miktar);
            return miktar <= 0m ? 0m : Math.Round(Math.Min(liste.Sum(s => Math.Min(s.Karsilanan, s.Miktar)), miktar) / miktar * 100m, 1);
        }

        var karsilanan = liste.Where(s => s.Miktar > 0m).Sum(s => s.Matrah * Math.Min(s.Karsilanan, s.Miktar) / s.Miktar);
        return Math.Round(karsilanan / toplam * 100m, 1, MidpointRounding.AwayFromZero);
    }

    // Kuruş altı kalan (yuvarlama artığı) ödenmiş sayılır.
    public static SiparisOdemeDurumu OdemeDurumu(decimal faturalananTl, decimal kalanTl) =>
        faturalananTl <= 0m ? SiparisOdemeDurumu.Faturalanmadi
        : kalanTl < 0.01m ? SiparisOdemeDurumu.Odendi
        : kalanTl >= faturalananTl - 0.01m ? SiparisOdemeDurumu.Odenmedi
        : SiparisOdemeDurumu.KismenOdendi;
}
