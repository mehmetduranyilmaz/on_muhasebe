namespace OnMuhasebe.Shared.Contracts.Vergi;

public static class KdvOranlari
{
    public const int Genel = 20;

    public static IReadOnlyList<int> Tumu { get; } = [0, 1, 10, 20];

    public static bool GecerliMi(int oran) => Tumu.Contains(oran);
}
