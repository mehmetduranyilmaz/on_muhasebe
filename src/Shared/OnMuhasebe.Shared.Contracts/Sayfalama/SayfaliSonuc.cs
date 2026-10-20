namespace OnMuhasebe.Shared.Contracts.Sayfalama;

public sealed record SayfaliSonuc<T>(IReadOnlyList<T> Kayitlar, int Sayfa, int Boyut, int ToplamKayit)
{
    public int ToplamSayfa => (int)Math.Ceiling(ToplamKayit / (double)Boyut);
}
