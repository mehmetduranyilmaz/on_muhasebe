namespace OnMuhasebe.Modules.Fatura.Contracts;

// Form Ba (mal ve hizmet alımları) / Form Bs (satışları) bildirimi — 396 Sıra No'lu VUK Genel Tebliği.
// Aynı karşı taraftan ayda KDV hariç sınır (5.000 TL) ve üzeri belgeler; tutar kuruşsuz TL, belge sayısıyla birlikte.
// İadeler ayrı belge olarak girer: alıştan iade faturası Bs'ye, satıştan iade faturası Ba'ya. Nihai tüketici satışları girmez.
// Varsayım (mali müşavirle teyit edilmeli): serbest meslek makbuzu Ba'ya girer, gider pusulası girmez.
public sealed record BaBsBelgesi(bool BaMi, string? VergiNo, Guid? CariHesapId, string Unvan, decimal MatrahTl);

public sealed record BaBsSatiri(string? VergiNo, Guid? CariHesapId, string Unvan, int BelgeSayisi, decimal Tutar, bool VergiNoEksik);

public sealed record BaBsRaporu(int Yil, int Ay, decimal Sinir, IReadOnlyList<BaBsSatiri> Ba, IReadOnlyList<BaBsSatiri> Bs);

// Bir cariyle bir aydaki BA/BS mutabakatı için (sınırdan bağımsız) belge sayısı ve tutar.
public sealed record CariBaBsOzeti(int Yil, int Ay, int BaBelgeSayisi, decimal BaTutar, int BsBelgeSayisi, decimal BsTutar);

public static class BaBsHesaplayici
{
    public const decimal VarsayilanSinir = 5_000m;

    public static bool? BaMi(FaturaTuru tur, bool nihaiTuketiciMi) => tur switch
    {
        FaturaTuru.Alis or FaturaTuru.SatistanIade or FaturaTuru.SerbestMeslekMakbuzu => true,
        FaturaTuru.Satis or FaturaTuru.AlistanIade => nihaiTuketiciMi ? null : false,
        _ => null
    };

    // Karşı taraf vergi/TC kimlik numarasıyla belirlenir (aynı VKN'li iki cari kartı tek satırdır); numarası olmayan cari kendi başına satırdır.
    public static IReadOnlyList<BaBsSatiri> Ozet(IEnumerable<BaBsBelgesi> belgeler, bool ba, decimal sinir) =>
        belgeler.Where(b => b.BaMi == ba)
            .GroupBy(b => b.VergiNo ?? $"cari:{b.CariHesapId}")
            .Select(g => new BaBsSatiri(g.First().VergiNo, g.First().CariHesapId, g.First().Unvan, g.Count(),
                Math.Truncate(g.Sum(b => b.MatrahTl)), g.First().VergiNo is null))
            .Where(s => s.Tutar >= sinir)
            .OrderByDescending(s => s.Tutar)
            .ToList();
}
