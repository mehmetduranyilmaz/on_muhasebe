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

    // Menüde modül altındaki ek bölümler; aynı lisans ve aynı lazy paketle açılır (adres -> modül eşlemesi de bunu kullanır).
    public static IReadOnlyList<ModulMenuOgesi> MenuOgeleri { get; } =
    [
        .. Tumu,
        new(ModulKodlari.Fatura, "İrsaliyeler", "irsaliye", Icons.Material.Filled.LocalShipping, "Sevk irsaliyeleri, depolar arası sevk ve faturalanmamış irsaliye takibi"),
        new(ModulKodlari.KasaBanka, "Nakit Akış", "nakit-akis", Icons.Material.Filled.Timeline, "13 haftalık nakit planı: beklenen tahsilat ve ödemeler, nakit açığı uyarısı"),
        new(ModulKodlari.KasaBanka, "Krediler", "krediler", Icons.Material.Filled.AccountBalance, "Nakdi banka kredileri, ödeme planları ve taksit ödemeleri"),
        new(ModulKodlari.KasaBanka, "Çek / Senet", "cek-senet", Icons.Material.Filled.RequestQuote, "Alınan ve verilen çek/senetler, portföy ve vade takvimi"),
        new(ModulKodlari.KasaBanka, "Teminatlar", "teminatlar", Icons.Material.Filled.Shield, "Alınan ve verilen teminatlar: teminat mektubu, ipotek, rehin, kefalet")
    ];

    public static string Ad(string kod) => Tumu.FirstOrDefault(m => m.Kod == kod)?.Ad ?? kod;
}
