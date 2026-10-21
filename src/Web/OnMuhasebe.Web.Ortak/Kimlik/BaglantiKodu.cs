using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace OnMuhasebe.Web.Kimlik;

// E-posta bağlantılarındaki tek kullanımlık kod adresin # kısmında gelir (sunucuya ve Referer'a gitmez).
// Okunduktan sonra adresten silinir (tarayıcı geçmişinde ve ekran paylaşımında görünmesin). Silme Blazor gezinmesiyle değil
// history.replaceState ile yapılır: gezinme sayfayı yeniden kurar ve okunan kod kaybolurdu.
public static class BaglantiKodu
{
    public static async Task<string?> AlAsync(NavigationManager navigasyon, IJSRuntime js)
    {
        var adres = new Uri(navigasyon.Uri);
        if (string.IsNullOrEmpty(adres.Fragment))
        {
            return null;
        }

        var kod = adres.Fragment.TrimStart('#').Split('&')
            .Select(p => p.Split('=', 2))
            .FirstOrDefault(p => p.Length == 2 && p[0] == "kod")?[1];
        await js.InvokeVoidAsync("history.replaceState", null, "", adres.GetLeftPart(UriPartial.Path));
        return string.IsNullOrWhiteSpace(kod) ? null : Uri.UnescapeDataString(kod);
    }
}
