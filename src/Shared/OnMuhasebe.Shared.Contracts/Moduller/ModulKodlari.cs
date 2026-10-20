namespace OnMuhasebe.Shared.Contracts.Moduller;

public static class ModulKodlari
{
    public const string Cari = "cari";
    public const string Stok = "stok";
    public const string Fatura = "fatura";
    public const string KasaBanka = "kasabanka";

    public static IReadOnlyList<string> Tumu { get; } = [Cari, Stok, Fatura, KasaBanka];
}
