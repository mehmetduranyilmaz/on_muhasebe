namespace OnMuhasebe.Shared.Domain;

public static class Para
{
    // Muhasebe uygulamasındaki yerleşik kural: yarım kuruş yukarı yuvarlanır (bankacı yuvarlaması değil).
    public static decimal Yuvarla(decimal tutar) => Math.Round(tutar, 2, MidpointRounding.AwayFromZero);

    public static decimal TlKarsiligi(decimal dovizTutari, decimal kur) => Yuvarla(dovizTutari * kur);
}
