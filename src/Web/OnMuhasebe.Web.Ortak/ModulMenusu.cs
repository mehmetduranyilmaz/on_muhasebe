using MudBlazor;
using OnMuhasebe.Shared.Contracts.Moduller;

namespace OnMuhasebe.Web;

// Kaynak: ekranın yetki kaynağı; menüde ve adres girişinde "kaynak.goruntule" yetkisi aranır.
public sealed record ModulMenuOgesi(string Kod, string Ad, string Yol, string Ikon, string Aciklama, string Kaynak)
{
    public string GorunumYetkisi => $"{Kaynak}.goruntule";
}

public static class ModulMenusu
{
    public static IReadOnlyList<ModulMenuOgesi> Tumu { get; } =
    [
        new(ModulKodlari.Cari, "Cari Hesaplar", "cari", Icons.Material.Filled.People, "Müşteri ve tedarikçi hesapları, ekstreler", "cari.hesap"),
        new(ModulKodlari.Stok, "Stok", "stok", Icons.Material.Filled.Inventory2, "Ürünler ve stok hareketleri", "stok.kart"),
        new(ModulKodlari.Fatura, "Fatura", "fatura", Icons.Material.Filled.ReceiptLong, "Alış ve satış faturaları", "fatura.belge"),
        new(ModulKodlari.KasaBanka, "Kasa / Banka", "kasabanka", Icons.Material.Filled.AccountBalance, "Tahsilat, ödeme, kasa ve banka hareketleri", "kasabanka.hesap"),
        new(ModulKodlari.Muhasebe, "Muhasebe Aktarımı", "muhasebe", Icons.Material.Filled.SyncAlt, "Yevmiye fişleri, mali müşavire Excel/CSV aktarımı", "muhasebe.aktarim"),
        new(ModulKodlari.EvrakTakip, "Evrak Takibi", "evrak-takibi", Icons.Material.Filled.EventNote, "Ruhsat, poliçe, sözleşme gibi süreli evrakların bitiş takibi ve hatırlatması", "evraktakip.kayit"),
        new(ModulKodlari.Arac, "Araç Takibi", "arac", Icons.Material.Filled.DirectionsCar, "Sigorta, muayene ve MTV süreleri; yakıt, masraf ve hasar takibi", "arac.kart"),
        new(ModulKodlari.Bakim, "Bakım Takibi", "bakim", Icons.Material.Filled.Build, "Ekipman bakım planları, yasal kontroller, arıza iş emirleri ve duruş takibi", "bakim.ekipman"),
        new(ModulKodlari.Satinalma, "Satın Alma", "satinalma", Icons.Material.Filled.ShoppingCart, "Satın alma talepleri, tutara göre onay akışı ve siparişe dönüşüm", "satinalma.talep"),
        new(ModulKodlari.Personel, "Personel", "personel", Icons.Material.Filled.Badge, "Personel kartı, net maaş, avans, masraf ve zimmet takibi", "personel.kart")
    ];

    // Menüde modül altındaki ek bölümler; aynı lisans ve aynı lazy paketle açılır (adres -> modül eşlemesi de bunu kullanır).
    public static IReadOnlyList<ModulMenuOgesi> MenuOgeleri { get; } =
    [
        .. Tumu,
        new(ModulKodlari.Stok, "Fiyat Listeleri", "fiyat-listesi", Icons.Material.Filled.PriceChange, "Cariye ve cari grubuna özel, tarihli ve kademeli satış/alış fiyatları", "stok.fiyat"),
        new(ModulKodlari.Fatura, "Tekrarlayan Faturalar", "tekrar-fatura", Icons.Material.Filled.EventRepeat, "Kira, abonelik ve bakım sözleşmesi gibi düzenli kesilen faturalar", "fatura.tekrar"),
        new(ModulKodlari.Fatura, "Teklifler", "teklif", Icons.Material.Filled.RequestPage, "Satış teklifleri: geçerlilik, kabul/red, siparişe dönüşüm", "fatura.teklif"),
        new(ModulKodlari.Fatura, "Siparişler", "siparis", Icons.Material.Filled.ShoppingBag, "Müşteri ve satınalma siparişleri, termin ve karşılama takibi", "fatura.teklif"),
        new(ModulKodlari.Fatura, "İrsaliyeler", "irsaliye", Icons.Material.Filled.LocalShipping, "Sevk irsaliyeleri, depolar arası sevk ve faturalanmamış irsaliye takibi", "fatura.irsaliye"),
        new(ModulKodlari.KasaBanka, "Nakit Akış", "nakit-akis", Icons.Material.Filled.Timeline, "13 haftalık nakit planı: beklenen tahsilat ve ödemeler, nakit açığı uyarısı", "kasabanka.rapor"),
        new(ModulKodlari.KasaBanka, "Krediler", "krediler", Icons.Material.Filled.AccountBalance, "Nakdi banka kredileri, ödeme planları ve taksit ödemeleri", "kasabanka.kredi"),
        new(ModulKodlari.KasaBanka, "Çek / Senet", "cek-senet", Icons.Material.Filled.RequestQuote, "Alınan ve verilen çek/senetler, portföy ve vade takvimi", "kasabanka.cek"),
        new(ModulKodlari.KasaBanka, "Demirbaşlar", "demirbaslar", Icons.Material.Filled.Chair, "Sabit kıymetler, amortisman planı ve yıllık amortisman listesi", "kasabanka.demirbas"),
        new(ModulKodlari.KasaBanka, "Teminatlar", "teminatlar", Icons.Material.Filled.Shield, "Alınan ve verilen teminatlar: teminat mektubu, ipotek, rehin, kefalet", "kasabanka.teminat"),
        new(ModulKodlari.Personel, "Masraflarım", "masraflarim", Icons.Material.Filled.ReceiptLong, "Kendi masraf beyanlarınız", "personel.kendi"),
        new(ModulKodlari.Muhasebe, "Hesap Planı", "hesap-plani", Icons.Material.Filled.AccountTree, "Muhasebe hesap kodları ve kart eşleştirmesi", "muhasebe.plan")
    ];

    public static string Ad(string kod) => Tumu.FirstOrDefault(m => m.Kod == kod)?.Ad ?? kod;
}
