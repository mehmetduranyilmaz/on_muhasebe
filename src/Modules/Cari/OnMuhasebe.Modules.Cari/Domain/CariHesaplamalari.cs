using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Shared.Domain;

namespace OnMuhasebe.Modules.Cari.Domain;

internal sealed record EkstreHesabi(
    IReadOnlyList<EkstreSatiri> Satirlar,
    decimal ToplamBorc,
    decimal ToplamAlacak,
    decimal SonBakiye,
    decimal ToplamBorcTl,
    decimal ToplamAlacakTl,
    decimal SonBakiyeTl);

internal static class CariHesaplamalari
{
    // Hareketler tarih sırasıyla gelmelidir. Bakiye = devir + Σ(borç - alacak); pozitif bakiye cari borçlu demektir.
    public static EkstreHesabi Ekstre(decimal devirBakiye, decimal devirBakiyeTl, IEnumerable<EkstreSatiri> hareketler)
    {
        var bakiye = devirBakiye;
        var bakiyeTl = devirBakiyeTl;
        decimal borc = 0, alacak = 0, borcTl = 0, alacakTl = 0;
        var satirlar = new List<EkstreSatiri>();

        foreach (var h in hareketler)
        {
            bakiye += h.Borc - h.Alacak;
            bakiyeTl += h.BorcTl - h.AlacakTl;
            borc += h.Borc;
            alacak += h.Alacak;
            borcTl += h.BorcTl;
            alacakTl += h.AlacakTl;
            satirlar.Add(h with { Bakiye = bakiye, BakiyeTl = bakiyeTl });
        }

        return new EkstreHesabi(satirlar, borc, alacak, bakiye, borcTl, alacakTl, bakiyeTl);
    }

    // Döviz bakiyenin güncel kurla TL değeri ile kayıtlı (işlem kurlarıyla oluşmuş) TL bakiye arasındaki fark.
    // Borçlu caride pozitif fark kur farkı geliri, alacaklı caride pozitif fark kur farkı gideridir.
    public static (decimal GuncelTl, decimal KurFarki) KurFarki(decimal dovizBakiye, decimal bakiyeTl, decimal guncelKur)
    {
        var guncelTl = Para.TlKarsiligi(dovizBakiye, guncelKur);
        return (guncelTl, guncelTl - bakiyeTl);
    }
}
