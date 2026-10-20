using OnMuhasebe.Shared.Contracts.Moduller;

namespace OnMuhasebe.Web;

// Modül ekranlarının lazy yüklenen derlemeleri. Yeni modül eklendiğinde buraya ve csproj'daki
// BlazorWebAssemblyLazyLoad listesine eklenir.
public static class ModulPaketleri
{
    public static IReadOnlyDictionary<string, string[]> Derlemeler { get; } = new Dictionary<string, string[]>
    {
        [ModulKodlari.Cari] =
        [
            "OnMuhasebe.Web.Cari.wasm",
            "OnMuhasebe.Modules.Cari.Contracts.wasm",
            "OnMuhasebe.Modules.Doviz.Contracts.wasm"
        ]
    };

    // Adresin ilk parçası modülün yoludur (ör. "cari/123/ekstre" -> cari).
    public static ModulMenuOgesi? AdrestekiModul(string adres)
    {
        var ilkParca = adres.TrimStart('/').Split('/', '?', '#')[0];
        return ModulMenusu.Tumu.FirstOrDefault(m => m.Yol == ilkParca);
    }
}
