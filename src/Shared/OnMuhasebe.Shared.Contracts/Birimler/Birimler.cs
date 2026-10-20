namespace OnMuhasebe.Shared.Contracts.Birimler;

// UneceKodu: e-Fatura'da (UBL-TR) kullanılan UN/ECE Rec. 20 birim kodu.
public sealed record Birim(string Kod, string Ad, string UneceKodu, bool KesirliMi);

public static class Birimler
{
    public const string Adet = "ADET";

    public static IReadOnlyList<Birim> Tumu { get; } =
    [
        new(Adet, "Adet", "C62", false),
        new("KG", "Kilogram", "KGM", true),
        new("GR", "Gram", "GRM", true),
        new("TON", "Ton", "TNE", true),
        new("LT", "Litre", "LTR", true),
        new("M", "Metre", "MTR", true),
        new("M2", "Metrekare", "MTK", true),
        new("M3", "Metreküp", "MTQ", true),
        new("PAKET", "Paket", "PA", false),
        new("KUTU", "Kutu", "BX", false),
        new("KOLI", "Koli", "CT", false),
        new("DUZINE", "Düzine", "DZN", false),
        new("TAKIM", "Takım", "SET", false),
        new("CIFT", "Çift", "PR", false),
        new("SAAT", "Saat", "HUR", true),
        new("GUN", "Gün", "DAY", true),
        new("AY", "Ay", "MON", false)
    ];

    public static bool GecerliMi(string? kod) => kod is not null && Tumu.Any(b => b.Kod == kod);

    public static string Ad(string kod) => Tumu.FirstOrDefault(b => b.Kod == kod)?.Ad ?? kod;
}
