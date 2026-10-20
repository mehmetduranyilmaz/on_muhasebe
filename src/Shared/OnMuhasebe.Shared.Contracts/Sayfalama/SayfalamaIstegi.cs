namespace OnMuhasebe.Shared.Contracts.Sayfalama;

public sealed record SayfalamaIstegi(int Sayfa = 1, int Boyut = SayfalamaIstegi.VarsayilanBoyut)
{
    public const int VarsayilanBoyut = 20;
    public const int AzamiBoyut = 100;

    public int GecerliSayfa => Math.Max(Sayfa, 1);

    public int GecerliBoyut => Math.Clamp(Boyut, 1, AzamiBoyut);
}
