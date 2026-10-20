using System.Reflection;
using Microsoft.AspNetCore.Components.WebAssembly.Services;
using OnMuhasebe.Web.Kimlik;

namespace OnMuhasebe.Web;

// Modül ekran derlemelerini lisans varsa bir kez indirir. Router ve ana sayfa panelleri aynı listeyi kullanır.
public sealed class ModulYukleyici(LazyAssemblyLoader derlemeYukleyici, OturumYoneticisi oturum)
{
    private readonly Dictionary<string, IReadOnlyList<Assembly>> _yuklenenler = [];
    private readonly SemaphoreSlim _kilit = new(1, 1);

    // Router'ın AdditionalAssemblies listesi; yüklenen derlemeler buraya eklenir.
    public List<Assembly> Derlemeler { get; } = [];

    public async Task<IReadOnlyList<Assembly>> YukleAsync(string modulKodu)
    {
        if (!oturum.ModulAcikMi(modulKodu) || !ModulPaketleri.Derlemeler.TryGetValue(modulKodu, out var adlar))
        {
            return [];
        }

        await _kilit.WaitAsync();
        try
        {
            if (!_yuklenenler.TryGetValue(modulKodu, out var derlemeler))
            {
                derlemeler = (await derlemeYukleyici.LoadAssembliesAsync(adlar)).ToList();
                _yuklenenler[modulKodu] = derlemeler;
                Derlemeler.AddRange(derlemeler);
            }

            return derlemeler;
        }
        finally
        {
            _kilit.Release();
        }
    }

    public Task<Type?> OzetPaneliAsync(string modulKodu) => PanelAsync(ModulPaketleri.OzetPanelleri, modulKodu);

    public Task<Type?> AyarPaneliAsync(string modulKodu) => PanelAsync(ModulPaketleri.AyarPanelleri, modulKodu);

    private async Task<Type?> PanelAsync(IReadOnlyDictionary<string, string> paneller, string modulKodu) =>
        paneller.TryGetValue(modulKodu, out var tipAdi)
            ? (await YukleAsync(modulKodu)).Select(d => d.GetType(tipAdi)).FirstOrDefault(t => t is not null)
            : null;
}
