using System.Text.Json.Serialization;

namespace OnMuhasebe.Modules.Cari.Contracts;

// Yaşlandırma dilimleri piyasadaki standart rapor dilimleridir; gecikme vadeden (vade yoksa işlem tarihinden) sayılır.
[JsonConverter(typeof(JsonStringEnumConverter<YasDilimi>))]
public enum YasDilimi
{
    VadesiGelmemis,
    Gun1_30,
    Gun31_60,
    Gun61_90,
    Gun90Ustu
}

// Alacak: carinin bize borcu (açık borç kalemleri). Borç: bizim cariye borcumuz (açık alacak kalemleri).
[JsonConverter(typeof(JsonStringEnumConverter<YaslandirmaYonu>))]
public enum YaslandirmaYonu
{
    Alacak,
    Borc
}

// Bir carinin yaşlandırma satırı. Dilim tutarları carinin para birimindedir; ToplamTl güncel kurla (TCMB döviz alış) bulunur.
// OrtalamaGecikme: vadesi geçmiş kısmın tutar ağırlıklı ortalama gecikme günü (vadesi geçmiş yoksa 0).
public sealed record YaslandirmaSatiri(
    Guid CariHesapId,
    string CariKodu,
    string Unvan,
    string ParaBirimi,
    decimal VadesiGelmemis,
    decimal Gun1_30,
    decimal Gun31_60,
    decimal Gun61_90,
    decimal Gun90Ustu,
    decimal Toplam,
    decimal ToplamTl,
    decimal VadesiGecmisTl,
    int OrtalamaGecikme,
    DateOnly EnEskiVade);

// Dilim toplamları TL'dir. AvansTl, yaşlandırmaya katılmayan (fazla tahsilat/ödeme) açık kalemlerin TL toplamıdır.
public sealed record YaslandirmaRaporu(
    DateOnly Tarih,
    YaslandirmaYonu Yon,
    decimal VadesiGelmemisTl,
    decimal Gun1_30Tl,
    decimal Gun31_60Tl,
    decimal Gun61_90Tl,
    decimal Gun90UstuTl,
    decimal ToplamTl,
    int OrtalamaGecikme,
    decimal AvansTl,
    IReadOnlyList<YaslandirmaSatiri> Satirlar);

public static class CariHareketTurleri
{
    // Açık kalan avans kalemleri: carinin borcundan fazla tahsilat (alınan avans) ya da alacağından fazla ödeme (verilen avans).
    // Bunlar vadesi gelen alacak/borç değildir; yaşlandırmaya ve nakit akışına katılmaz.
    public static bool AvansMi(CariHareketTuru tur, HareketYonu yon) => yon == HareketYonu.Alacak
        ? tur is CariHareketTuru.Tahsilat or CariHareketTuru.CekSenetGirisi
        : tur is CariHareketTuru.Odeme or CariHareketTuru.CekSenetCikisi;
}

public static class YaslandirmaHesaplayici
{
    public static YasDilimi Dilim(int gecikmeGun) => gecikmeGun switch
    {
        <= 0 => YasDilimi.VadesiGelmemis,
        <= 30 => YasDilimi.Gun1_30,
        <= 60 => YasDilimi.Gun31_60,
        <= 90 => YasDilimi.Gun61_90,
        _ => YasDilimi.Gun90Ustu
    };

    // Vadesi geçmiş kısmın tutar ağırlıklı ortalama gecikmesi; tam güne yuvarlanır.
    public static int OrtalamaGecikme(IEnumerable<(decimal Tutar, int GecikmeGun)> kalemler)
    {
        var gecmis = kalemler.Where(k => k.GecikmeGun > 0 && k.Tutar > 0).ToList();
        var toplam = gecmis.Sum(k => k.Tutar);
        return toplam == 0m ? 0 : (int)Math.Round(gecmis.Sum(k => k.Tutar * k.GecikmeGun) / toplam, MidpointRounding.AwayFromZero);
    }

    // Kalemlerin dilimlere göre toplamı (dilim sırasıyla 5 eleman).
    public static decimal[] Dilimle(IEnumerable<(decimal Tutar, int GecikmeGun)> kalemler)
    {
        var sonuc = new decimal[5];
        foreach (var (tutar, gecikme) in kalemler)
        {
            sonuc[(int)Dilim(gecikme)] += tutar;
        }

        return sonuc;
    }
}

// Nakit akış raporu için firma geneli açık cari kalemi (avanslar hariç). Tutar carinin para birimindedir.
public sealed record AcikCariKalemi(
    Guid CariHesapId,
    string Unvan,
    HareketYonu Yon,
    DateOnly Vade,
    decimal Kalan,
    string ParaBirimi,
    CariHareketTuru Tur,
    string? BelgeNo,
    Guid? KaynakBelgeId);
