using MudBlazor;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Web.Kimlik;

namespace OnMuhasebe.Web;

// Ayarlar merkezinin bölümleri. Panel ya ana uygulamadadır (PanelTipi null, sayfa kendisi çizer) ya da modülün lazy paketindedir
// (PanelTipi tip adı; ana uygulama lazy derlemeye derleme zamanında bağımlı olmasın). Href'li bölüm kendi sayfasına gider.
public sealed record AyarBolumu(
    string Yol,
    string Grup,
    string Ad,
    string Ikon,
    string Aciklama,
    string? ModulKodu,
    string? Yetki,
    string? PanelTipi = null,
    IReadOnlyDictionary<string, object>? PanelParametreleri = null,
    string? Href = null)
{
    public string Adres => Href ?? $"ayarlar/{Yol}";

    // Modül ayarları da firma ayarıdır: lisans + yetki birlikte aranır.
    public bool ErisilebilirMi(OturumYoneticisi oturum) =>
        (ModulKodu is null || oturum.ModulAcikMi(ModulKodu)) && (Yetki is null || oturum.YetkiVarMi(Yetki));
}

public static class AyarBolumleri
{
    private static readonly string FirmaDuzenle = YonetimYetkileri.Firma.Yetki(YetkiIslemleri.Duzenle);
    private const string FaturaPaneli = "OnMuhasebe.Web.Fatura.Bilesenler.FaturaAyarPaneli";

    private static Dictionary<string, object> Bolum(string ad) => new() { ["Bolum"] = ad };

    public static IReadOnlyList<AyarBolumu> Tumu { get; } =
    [
        new("firma", "Firma", "Firma bilgileri", Icons.Material.Filled.Business, "Ünvan, vergi bilgileri ve adres; faturanın üst bilgisi", null, FirmaDuzenle),
        new("isyerleri", "Firma", "İşyerleri", Icons.Material.Filled.Store, "Merkez ve şubeler, işyerine özel seriler", null, FirmaDuzenle),
        new("abonelik", "Firma", "Abonelik", Icons.Material.Filled.WorkspacePremium, "Paket, süre ve ödeme bildirimleri", null,
            YonetimYetkileri.Abonelik.Yetki(YetkiIslemleri.Goruntule), Href: "abonelik"),

        new("kullanicilar", "Kullanıcılar", "Kullanıcılar", Icons.Material.Filled.ManageAccounts, "Kullanıcılar ve davetler", null,
            YonetimYetkileri.Kullanici.Yetki(YetkiIslemleri.Goruntule), Href: "kullanicilar"),
        new("roller", "Kullanıcılar", "Roller ve yetkiler", Icons.Material.Filled.AdminPanelSettings, "Rol bazında ekran ve işlem yetkileri", null,
            YonetimYetkileri.Kullanici.Yetki(YetkiIslemleri.Goruntule), Href: "roller"),

        new("fatura-seriler", "Belgeler", "Kayıt akışı ve seriler", Icons.Material.Filled.Numbers, "Doğrudan kayıt ya da taslak-onay; belge numara serileri",
            ModulKodlari.Fatura, FirmaDuzenle, FaturaPaneli, Bolum("Seriler")),
        new("fatura-varsayilanlar", "Belgeler", "Fatura varsayılanları", Icons.Material.Filled.ReceiptLong, "KDV dahil fiyat, vade ve ön tanımlı açıklama",
            ModulKodlari.Fatura, FirmaDuzenle, FaturaPaneli, Bolum("Varsayilanlar")),
        new("kagit-cikti", "Belgeler", "Kağıt çıktı", Icons.Material.Filled.Print, "Yazdırma şablonu, banka bilgileri ve imza alanı",
            ModulKodlari.Fatura, FirmaDuzenle, FaturaPaneli, Bolum("Kagit")),

        new("stok", "Modüller", "Stok", Icons.Material.Filled.Inventory2, "Maliyet yöntemi ve eksi stok davranışı",
            ModulKodlari.Stok, FirmaDuzenle, "OnMuhasebe.Web.Stok.Bilesenler.StokAyarPaneli"),
        new("gelir-gider", "Modüller", "Gelir / gider kategorileri", Icons.Material.Filled.Category, "Kasa/banka gelir ve gider kayıtlarının kategorileri",
            ModulKodlari.KasaBanka, FirmaDuzenle, "OnMuhasebe.Web.KasaBanka.Bilesenler.KasaBankaAyarPaneli"),
        new("ekstre-kurallari", "Modüller", "Ekstre eşleştirme kuralları", Icons.Material.Filled.AutoAwesome, "Banka ekstresi aktarımında satırlara önerilecek işlemler",
            ModulKodlari.KasaBanka, FirmaDuzenle, "OnMuhasebe.Web.KasaBanka.Bilesenler.EkstreKurallariPaneli"),
        new("cek-senet", "Modüller", "Çek / senet", Icons.Material.Filled.RequestQuote, "Keşideci kimliği, karne ve riskli keşideci kuralları",
            ModulKodlari.KasaBanka, FirmaDuzenle, "OnMuhasebe.Web.KasaBanka.Bilesenler.CekSenetAyarPaneli"),
        new("satinalma", "Modüller", "Satın alma onayı", Icons.Material.Filled.Rule, "Tutara göre sıralı onay basamakları",
            ModulKodlari.Satinalma, "satinalma.kural.goruntule", "OnMuhasebe.Web.Satinalma.Bilesenler.SatinalmaAyarPaneli"),

        new("zorunlu-alanlar", "Kurallar", "Zorunlu alanlar", Icons.Material.Filled.PlaylistAddCheck, "Boş bırakılamayacak form alanları", null, FirmaDuzenle),
        new("donem-kilidi", "Kurallar", "Dönem kilidi", Icons.Material.Filled.Lock, "Kapatılan dönemin kayıtları değiştirilemesin", null, FirmaDuzenle),
        new("bildirimler", "Kurallar", "Bildirimler", Icons.Material.Filled.NotificationsActive, "Firmada üretilecek bildirim türleri ve gün sayıları", null, FirmaDuzenle)
    ];

    public static IEnumerable<AyarBolumu> Erisilebilir(OturumYoneticisi oturum) => Tumu.Where(b => b.ErisilebilirMi(oturum));

    public static AyarBolumu? Bul(string? yol) => Tumu.FirstOrDefault(b => b.Yol == yol);
}
