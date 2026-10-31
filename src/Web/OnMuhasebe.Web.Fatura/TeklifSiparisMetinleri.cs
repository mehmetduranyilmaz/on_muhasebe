using MudBlazor;
using OnMuhasebe.Modules.Fatura.Contracts;

namespace OnMuhasebe.Web.Fatura;

public static class TeklifSiparisMetinleri
{
    public static string Tur(TeklifSiparisTuru tur) => tur switch
    {
        TeklifSiparisTuru.SatisTeklifi => "Satış Teklifi",
        TeklifSiparisTuru.SatisSiparisi => "Müşteri Siparişi",
        _ => "Satınalma Siparişi"
    };

    public static string Ikon(TeklifSiparisTuru tur) => tur switch
    {
        TeklifSiparisTuru.SatisTeklifi => Icons.Material.Filled.RequestPage,
        TeklifSiparisTuru.SatisSiparisi => Icons.Material.Filled.ShoppingBag,
        _ => Icons.Material.Filled.AddShoppingCart
    };

    public static string Renk(TeklifSiparisTuru tur) => tur switch
    {
        TeklifSiparisTuru.SatisTeklifi => "#8B5CF6",
        TeklifSiparisTuru.SatisSiparisi => "#14B8A6",
        _ => "#3B82F6"
    };

    // Adres: teklif/..., siparis/... (ilk parça menüdeki bölümü ve yüklenecek paketi belirler).
    public static string Kok(TeklifSiparisTuru tur) => TeklifSiparisTurleri.TeklifMi(tur) ? "teklif" : "siparis";

    public static string Yol(TeklifSiparisTuru tur) => tur switch
    {
        TeklifSiparisTuru.SatisTeklifi => "teklif/yeni",
        TeklifSiparisTuru.SatisSiparisi => "siparis/yeni/satis",
        _ => "siparis/yeni/satinalma"
    };

    public static string Durum(TeklifSiparisDurumu d, bool teklifMi, bool suresiDoldu) => d switch
    {
        _ when suresiDoldu => "Süresi doldu",
        TeklifSiparisDurumu.Taslak => "Taslak",
        TeklifSiparisDurumu.Acik => teklifMi ? "Verildi" : "Açık",
        TeklifSiparisDurumu.Kabul => "Kabul edildi",
        TeklifSiparisDurumu.Red => "Reddedildi",
        TeklifSiparisDurumu.Kapatildi => "Kapatıldı",
        _ => "İptal"
    };

    public static Color DurumRengi(TeklifSiparisDurumu d, bool suresiDoldu) => d switch
    {
        _ when suresiDoldu => Color.Error,
        TeklifSiparisDurumu.Taslak => Color.Warning,
        TeklifSiparisDurumu.Acik => Color.Info,
        TeklifSiparisDurumu.Kabul => Color.Success,
        TeklifSiparisDurumu.Kapatildi => Color.Default,
        _ => Color.Error
    };

    public static string Karsilama(KarsilamaDurumu k) => k switch
    {
        KarsilamaDurumu.Bekliyor => "Bekliyor",
        KarsilamaDurumu.Kismen => "Kısmen karşılandı",
        _ => "Tamamlandı"
    };

    public static Color KarsilamaRengi(KarsilamaDurumu k) => k switch
    {
        KarsilamaDurumu.Tamamlandi => Color.Success,
        KarsilamaDurumu.Kismen => Color.Info,
        _ => Color.Default
    };
}
