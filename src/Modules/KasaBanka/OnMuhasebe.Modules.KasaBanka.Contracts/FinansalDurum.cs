namespace OnMuhasebe.Modules.KasaBanka.Contracts;

// Ana sayfanın finansal durum özeti. Bakiyeler güncel TCMB döviz alış kuruyla TL'dir (nakit akışıyla aynı kural);
// kuru bulunamayan döviz tutarları toplamlara girmez ve KuruBulunamayanlar'da listelenir.

// Varlıklar: kasa ve banka (KMH eksisi düşülmüş), POS'ta bankaya geçmeyi bekleyen, cari alacaklar, portföy/tahsildeki alınan çek-senet.
// Yükümlülükler: cari borçlar, ödenmemiş verilen çek-senet, kredi kartı borcu, açık kredilerin ödenmemiş anaparası.
public sealed record MiniBilanco(
    decimal KasaBankaTl,
    decimal PosBekleyenTl,
    decimal CariAlacakTl,
    decimal AlinanCekSenetTl,
    decimal ToplamVarlikTl,
    decimal CariBorcTl,
    decimal VerilenCekSenetTl,
    decimal KrediKartiBorcuTl,
    decimal KrediAnaparaTl,
    decimal ToplamYukumlulukTl,
    decimal NetTl);

// Kova: vadesi geçmiş, 7 gün içinde, 8-30 gün, 31-90 gün, 90 günden sonra. Cari açık kalemleri ve çek/senetler birlikte.
public sealed record VadeKovasi(string Ad, int? GunBaslangic, int? GunBitis, decimal TahsilEdilecekTl, decimal OdenecekTl);

// Gerçekleşen nakit: iç virman ve açılış kayıtları sayılmaz. AySonuNakitTl kasa+banka+POS bakiyesidir (işlem kurlarıyla TL).
public sealed record AylikNakitHareketi(int Yil, int Ay, decimal GirisTl, decimal CikisTl, decimal NetTl, decimal AySonuNakitTl);

public sealed record FinansalDurumRaporu(
    DateOnly Tarih,
    MiniBilanco Bilanco,
    IReadOnlyList<VadeKovasi> VadeKovalari,
    IReadOnlyList<AylikNakitHareketi> Aylar,
    IReadOnlyList<string> KuruBulunamayanlar);

public sealed record VadeliKalem(DateOnly Vade, bool TahsilMi, decimal TutarTl);

public static class FinansalDurumHesaplayici
{
    private static readonly (string Ad, int? Bas, int? Bit)[] _kovalar =
    [
        ("Vadesi geçmiş", null, -1),
        ("7 gün içinde", 0, 7),
        ("8-30 gün", 8, 30),
        ("31-90 gün", 31, 90),
        ("90 günden sonra", 91, null)
    ];

    public static IReadOnlyList<VadeKovasi> VadeKovalari(DateOnly bugun, IEnumerable<VadeliKalem> kalemler)
    {
        var liste = kalemler.ToList();
        return _kovalar.Select(k =>
        {
            bool Icinde(VadeliKalem x)
            {
                var gun = x.Vade.DayNumber - bugun.DayNumber;
                return (k.Bas is null || gun >= k.Bas) && (k.Bit is null || gun <= k.Bit);
            }

            return new VadeKovasi(k.Ad, k.Bas, k.Bit, liste.Where(x => x.TahsilMi && Icinde(x)).Sum(x => x.TutarTl),
                liste.Where(x => !x.TahsilMi && Icinde(x)).Sum(x => x.TutarTl));
        }).ToList();
    }

    public static MiniBilanco Bilanco(decimal kasaBanka, decimal posBekleyen, decimal cariAlacak, decimal alinanEvrak,
        decimal cariBorc, decimal verilenEvrak, decimal krediKarti, decimal krediAnapara)
    {
        var varlik = kasaBanka + posBekleyen + cariAlacak + alinanEvrak;
        var yukumluluk = cariBorc + verilenEvrak + krediKarti + krediAnapara;
        return new MiniBilanco(kasaBanka, posBekleyen, cariAlacak, alinanEvrak, varlik, cariBorc, verilenEvrak, krediKarti, krediAnapara,
            yukumluluk, varlik - yukumluluk);
    }

    // Son 12 ay (içinde bulunulan ay dahil), boş aylar sıfırla; ay sonu nakit, pencere öncesi bakiyeden birikimli.
    public static IReadOnlyList<AylikNakitHareketi> Aylar(
        DateOnly bugun, int aySayisi, decimal oncekiNakit, IReadOnlyDictionary<(int Yil, int Ay), (decimal Giris, decimal Cikis)> akis,
        IReadOnlyDictionary<(int Yil, int Ay), decimal> bakiyeDegisimi)
    {
        var ilk = new DateOnly(bugun.Year, bugun.Month, 1).AddMonths(-(aySayisi - 1));
        var nakit = oncekiNakit;
        var sonuc = new List<AylikNakitHareketi>(aySayisi);
        for (var i = 0; i < aySayisi; i++)
        {
            var ay = ilk.AddMonths(i);
            var (giris, cikis) = akis.GetValueOrDefault((ay.Year, ay.Month));
            nakit += bakiyeDegisimi.GetValueOrDefault((ay.Year, ay.Month));
            sonuc.Add(new AylikNakitHareketi(ay.Year, ay.Month, giris, cikis, giris - cikis, nakit));
        }

        return sonuc;
    }
}
