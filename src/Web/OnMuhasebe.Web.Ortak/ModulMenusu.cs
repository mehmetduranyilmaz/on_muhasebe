using MudBlazor;
using OnMuhasebe.Shared.Contracts.Moduller;

namespace OnMuhasebe.Web;

public sealed record ModulMenuOgesi(string Kod, string Ad, string Yol, string Ikon, string Aciklama);

public static class ModulMenusu
{
    public static IReadOnlyList<ModulMenuOgesi> Tumu { get; } =
    [
        new(ModulKodlari.Cari, "Cari Hesaplar", "cari", Icons.Material.Filled.People, "Müşteri ve tedarikçi hesapları, ekstreler"),
        new(ModulKodlari.Stok, "Stok", "stok", Icons.Material.Filled.Inventory2, "Ürünler ve stok hareketleri"),
        new(ModulKodlari.Fatura, "Fatura", "fatura", Icons.Material.Filled.ReceiptLong, "Alış ve satış faturaları"),
        new(ModulKodlari.KasaBanka, "Kasa / Banka", "kasabanka", Icons.Material.Filled.AccountBalance, "Tahsilat, ödeme, kasa ve banka hareketleri")
    ];

    public static string Ad(string kod) => Tumu.FirstOrDefault(m => m.Kod == kod)?.Ad ?? kod;
}
