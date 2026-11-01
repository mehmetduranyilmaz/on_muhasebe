using OnMuhasebe.Shared.Contracts.Moduller;

namespace OnMuhasebe.Web;

// Modül ekranlarının lazy yüklenen derlemeleri ve ana sayfa / ayarlar ekranına koydukları paneller.
// Yeni modül eklendiğinde buraya ve csproj'daki BlazorWebAssemblyLazyLoad listesine eklenir.
public static class ModulPaketleri
{
    public static IReadOnlyDictionary<string, string[]> Derlemeler { get; } = new Dictionary<string, string[]>
    {
        [ModulKodlari.Cari] =
        [
            "OnMuhasebe.Web.Cari.wasm",
            "OnMuhasebe.Modules.Cari.Contracts.wasm",
            "OnMuhasebe.Modules.Doviz.Contracts.wasm"
        ],
        [ModulKodlari.Stok] =
        [
            "OnMuhasebe.Web.Stok.wasm",
            "OnMuhasebe.Modules.Stok.Contracts.wasm",
            "OnMuhasebe.Modules.Fatura.Contracts.wasm"
        ],
        [ModulKodlari.Fatura] =
        [
            "OnMuhasebe.Web.Fatura.wasm",
            "OnMuhasebe.Modules.Fatura.Contracts.wasm",
            "OnMuhasebe.Modules.Cari.Contracts.wasm",
            "OnMuhasebe.Modules.Stok.Contracts.wasm",
            "OnMuhasebe.Modules.Doviz.Contracts.wasm",
            "OnMuhasebe.Modules.KasaBanka.Contracts.wasm"
        ],
        [ModulKodlari.KasaBanka] =
        [
            "OnMuhasebe.Web.KasaBanka.wasm",
            "OnMuhasebe.Modules.KasaBanka.Contracts.wasm",
            "OnMuhasebe.Modules.Cari.Contracts.wasm",
            "OnMuhasebe.Modules.Doviz.Contracts.wasm"
        ],
        [ModulKodlari.Muhasebe] =
        [
            "OnMuhasebe.Web.Muhasebe.wasm",
            "OnMuhasebe.Modules.Muhasebe.Contracts.wasm"
        ],
        [ModulKodlari.EvrakTakip] =
        [
            "OnMuhasebe.Web.EvrakTakip.wasm",
            "OnMuhasebe.Modules.EvrakTakip.Contracts.wasm"
        ],
        [ModulKodlari.Arac] =
        [
            "OnMuhasebe.Web.Arac.wasm",
            "OnMuhasebe.Modules.Arac.Contracts.wasm"
        ],
        [ModulKodlari.Bakim] =
        [
            "OnMuhasebe.Web.Bakim.wasm",
            "OnMuhasebe.Modules.Bakim.Contracts.wasm"
        ],
        [ModulKodlari.Satinalma] =
        [
            "OnMuhasebe.Web.Satinalma.wasm",
            "OnMuhasebe.Modules.Satinalma.Contracts.wasm"
        ],
        [ModulKodlari.Personel] =
        [
            "OnMuhasebe.Web.Personel.wasm",
            "OnMuhasebe.Modules.Personel.Contracts.wasm",
            "OnMuhasebe.Modules.KasaBanka.Contracts.wasm"
        ]
    };

    // Tip adları string: ana uygulama lazy derlemelere derleme zamanında bağımlı olmasın.
    public static IReadOnlyDictionary<string, string> OzetPanelleri { get; } = new Dictionary<string, string>
    {
        [ModulKodlari.Cari] = "OnMuhasebe.Web.Cari.Bilesenler.CariOzetPaneli",
        [ModulKodlari.Stok] = "OnMuhasebe.Web.Stok.Bilesenler.StokOzetPaneli",
        [ModulKodlari.Fatura] = "OnMuhasebe.Web.Fatura.Bilesenler.FaturaOzetPaneli",
        [ModulKodlari.KasaBanka] = "OnMuhasebe.Web.KasaBanka.Bilesenler.KasaBankaOzetPaneli"
    };

    public static IReadOnlyDictionary<string, string> AyarPanelleri { get; } = new Dictionary<string, string>
    {
        [ModulKodlari.Stok] = "OnMuhasebe.Web.Stok.Bilesenler.StokAyarPaneli",
        [ModulKodlari.Fatura] = "OnMuhasebe.Web.Fatura.Bilesenler.FaturaAyarPaneli",
        [ModulKodlari.KasaBanka] = "OnMuhasebe.Web.KasaBanka.Bilesenler.KasaBankaAyarPaneli",
        [ModulKodlari.Satinalma] = "OnMuhasebe.Web.Satinalma.Bilesenler.SatinalmaAyarPaneli"
    };

    // Adresin ilk parçası modülün yoludur (ör. "cari/123/ekstre" -> cari).
    public static ModulMenuOgesi? AdrestekiModul(string adres)
    {
        var ilkParca = adres.TrimStart('/').Split('/', '?', '#')[0];
        return ModulMenusu.MenuOgeleri.FirstOrDefault(m => m.Yol == ilkParca);
    }
}
