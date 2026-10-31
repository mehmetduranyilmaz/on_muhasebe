namespace OnMuhasebe.Modules.Cari.Contracts;

public static class CariHareketTurleri
{
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
