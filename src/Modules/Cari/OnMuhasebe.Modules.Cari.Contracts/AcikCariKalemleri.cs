namespace OnMuhasebe.Modules.Cari.Contracts;

public static class CariHareketTurleri
{
    // Fatura modülündeki bir belgeden (fatura, gider pusulası, serbest meslek makbuzu) doğan hareket.
    public static bool FaturaHareketiMi(CariHareketTuru tur) => tur is CariHareketTuru.SatisFaturasi or CariHareketTuru.AlisFaturasi
        or CariHareketTuru.SatistanIadeFaturasi or CariHareketTuru.AlistanIadeFaturasi or CariHareketTuru.GiderPusulasi
        or CariHareketTuru.SerbestMeslekMakbuzu or CariHareketTuru.VerilenSerbestMeslekMakbuzu;

    // Açık kalan avans kalemleri: carinin borcundan fazla tahsilat (alınan avans) ya da alacağından fazla ödeme (verilen avans).
    // Bunlar vadesi gelen alacak/borç değildir; vade analizine ve nakit akışına katılmaz.
    public static bool AvansMi(CariHareketTuru tur, HareketYonu yon) => yon == HareketYonu.Alacak
        ? tur is CariHareketTuru.Tahsilat or CariHareketTuru.CekSenetGirisi
        : tur is CariHareketTuru.Odeme or CariHareketTuru.CekSenetCikisi;
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
