using OnMuhasebe.Shared.Contracts.Ayarlar;
using OnMuhasebe.Web.Api;
using OnMuhasebe.Web.Kimlik;

namespace OnMuhasebe.Web;

// Firmanın zorunlu yaptığı alanlar; formlar alanı "Required" işaretlemek için sorar. Oturum (firma) değişince ya da ayar kaydedilince
// yeniden okunur. Asıl kontrol API'dedir; bu yalnızca kullanıcıyı kaydetmeden önce uyarır.
public sealed class ZorunluAlanServisi
{
    private readonly ApiIstemcisi _api;
    private Task<IReadOnlySet<string>>? _secili;

    public ZorunluAlanServisi(ApiIstemcisi api, OturumYoneticisi oturum)
    {
        _api = api;
        oturum.Degisti += Temizle;
    }

    public Task<IReadOnlySet<string>> SeciliAsync() => _secili ??= YukleAsync();

    public void Temizle() => _secili = null;

    private async Task<IReadOnlySet<string>> YukleAsync()
    {
        var sonuc = await _api.GetAsync<ZorunluAlanlarCevabi>("api/yonetim/zorunlu-alanlar");
        if (!sonuc.BasariliMi)
        {
            _secili = null;
            return new HashSet<string>();
        }

        return sonuc.Deger.Secili.ToHashSet();
    }
}
