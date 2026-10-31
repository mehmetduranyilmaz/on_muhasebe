using Microsoft.JSInterop;
using OnMuhasebe.Shared.Contracts.Isyerleri;
using OnMuhasebe.Web.Api;
using OnMuhasebe.Web.Kimlik;

namespace OnMuhasebe.Web;

// Kullanıcının girebildiği işyerleri ve üst çubukta seçili işyeri. Seçili işyeri listeleri ve raporları süzer, yeni belgenin
// işyeri alanına önerilir; boşsa (Tüm işyerleri) liste süzülmez ve yeni belge kullanıcının varsayılan işyerine açılır.
// Tek işyerli firmada hiçbir ekranda işyeri sorulmaz. Seçim tarayıcıda firma başına hatırlanır.
public sealed class IsyeriYoneticisi
{
    private readonly ApiIstemcisi _api;
    private readonly OturumYoneticisi _oturum;
    private readonly IJSRuntime _js;
    private Task<IReadOnlyList<IsyeriOzeti>>? _yukleme;

    public IsyeriYoneticisi(ApiIstemcisi api, OturumYoneticisi oturum, IJSRuntime js)
    {
        _api = api;
        _oturum = oturum;
        _js = js;
        oturum.Degisti += () =>
        {
            _yukleme = null;
            Tumu = [];
            Secili = null;
            Degisti?.Invoke();
        };
    }

    public event Action? Degisti;

    // Liste uçundan gelen işyerleri: yönetici tümünü (kapalılar dahil), kısıtlı kullanıcı yalnız girebildiği aktifleri görür.
    public IReadOnlyList<IsyeriOzeti> Tumu { get; private set; } = [];

    public IReadOnlyList<IsyeriOzeti> Aktifler => Tumu.Where(i => i.AktifMi).ToList();

    public bool CokluMu => Aktifler.Count > 1;

    public Guid? Secili { get; private set; }

    public async Task<IReadOnlyList<IsyeriOzeti>> YukleAsync() => await (_yukleme ??= GetirAsync());

    public void Sec(Guid? isyeriId)
    {
        Secili = isyeriId;
        if (_oturum.Bilgi?.AktifFirma?.Id is { } firma)
        {
            _ = _js.InvokeVoidAsync("omIsyeri.yaz", firma.ToString(), isyeriId?.ToString()).AsTask();
        }

        Degisti?.Invoke();
    }

    // İşyeri eklenince/değişince (Ayarlar) liste yeniden okunur.
    public async Task YenileAsync()
    {
        _yukleme = null;
        await YukleAsync();
        Degisti?.Invoke();
    }

    public string? Ad(Guid? isyeriId) => Tumu.FirstOrDefault(i => i.Id == isyeriId)?.Ad;

    // Liste ve rapor adreslerine eklenecek sorgu parçası ("&isyeriId=..." ya da boş).
    public string Sorgu(char ayrac = '&') => Secili is { } id ? $"{ayrac}isyeriId={id}" : "";

    private async Task<IReadOnlyList<IsyeriOzeti>> GetirAsync()
    {
        var sonuc = await _api.GetAsync<List<IsyeriOzeti>>("api/yonetim/isyerleri");
        Tumu = sonuc.Deger ?? [];
        if (Secili is null && _oturum.Bilgi?.AktifFirma?.Id is { } firma
            && Guid.TryParse(await _js.InvokeAsync<string?>("omIsyeri.oku", firma.ToString()), out var kayitli))
        {
            Secili = kayitli;
        }

        if (Secili is { } s && (!CokluMu || Aktifler.All(i => i.Id != s)))
        {
            Secili = null;
        }

        Degisti?.Invoke();

        return Tumu;
    }
}
