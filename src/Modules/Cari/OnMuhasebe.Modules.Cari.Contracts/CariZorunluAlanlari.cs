using OnMuhasebe.Shared.Contracts.Ayarlar;
using OnMuhasebe.Shared.Contracts.Moduller;

namespace OnMuhasebe.Modules.Cari.Contracts;

// Firmanın cari kartında zorunlu yapabileceği alanlar; ekran ve API aynı kodları kullanır.
public static class CariZorunluAlanlari
{
    private const string Form = "Cari kartı";

    public const string VergiNo = "cari.kart.vergiNo";
    public const string VergiDairesi = "cari.kart.vergiDairesi";
    public const string Il = "cari.kart.il";
    public const string Ilce = "cari.kart.ilce";
    public const string Adres = "cari.kart.adres";
    public const string Telefon = "cari.kart.telefon";
    public const string Eposta = "cari.kart.eposta";
    public const string Iban = "cari.kart.iban";
    public const string Grup = "cari.kart.grup";
    public const string VadeGun = "cari.kart.vadeGun";
    public const string RiskLimiti = "cari.kart.riskLimiti";
    public const string YetkiliAd = "cari.kart.yetkiliAd";
    public const string YetkiliTelefon = "cari.kart.yetkiliTelefon";

    public static IReadOnlyList<ZorunluAlanTanimi> Tumu { get; } =
    [
        new(VergiNo, ModulKodlari.Cari, Form, "Vergi / TC kimlik no"),
        new(VergiDairesi, ModulKodlari.Cari, Form, "Vergi dairesi"),
        new(Il, ModulKodlari.Cari, Form, "İl"),
        new(Ilce, ModulKodlari.Cari, Form, "İlçe"),
        new(Adres, ModulKodlari.Cari, Form, "Adres"),
        new(Telefon, ModulKodlari.Cari, Form, "Telefon"),
        new(Eposta, ModulKodlari.Cari, Form, "E-posta"),
        new(Iban, ModulKodlari.Cari, Form, "IBAN"),
        new(Grup, ModulKodlari.Cari, Form, "Grup"),
        new(VadeGun, ModulKodlari.Cari, Form, "Vade günü"),
        new(RiskLimiti, ModulKodlari.Cari, Form, "Risk limiti"),
        new(YetkiliAd, ModulKodlari.Cari, Form, "Yetkili adı"),
        new(YetkiliTelefon, ModulKodlari.Cari, Form, "Yetkili telefonu")
    ];
}
