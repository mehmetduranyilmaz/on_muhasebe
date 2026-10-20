using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using OnMuhasebe.Modules.Yonetim.Contracts;
using OnMuhasebe.Web.Api;

namespace OnMuhasebe.Web.Kimlik;

// Uygulama boyunca tek örnek (singleton). Access token yalnızca bellekte tutulur;
// sayfa yenilendiğinde HttpOnly çerezdeki refresh token ile sessizce yeniden alınır.
public sealed class OturumYoneticisi(IHttpClientFactory httpClientFactory, TimeProvider zaman)
{
    public const string KimlikIstemcisi = "kimlik";

    private static readonly TimeSpan _yenilemePayi = TimeSpan.FromSeconds(60);

    private readonly SemaphoreSlim _yenilemeKilidi = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _yenilenecekZaman;

    public ClaimsPrincipal Kullanici { get; private set; } = TokenOkuyucu.Anonim;

    public KullaniciBilgisi? Bilgi { get; private set; }

    public event Action? Degisti;

    public bool ModulAcikMi(string modulKodu) => Kullanici.HasClaim(TokenOkuyucu.ModulClaim, modulKodu);

    // Lisans süresi dolduysa kayıt ekranları gizlenir; asıl engel API'dedir.
    public bool SaltOkunurMu => Bilgi?.Lisans?.SaltOkunur == true;

    public Task<ApiSonucu> GirisAsync(GirisIstegi istek) =>
        TokenAlAsync(istemci => istemci.PostAsJsonAsync("api/yonetim/giris", istek));

    public Task<ApiSonucu> KayitAsync(KayitIstegi istek) =>
        TokenAlAsync(istemci => istemci.PostAsJsonAsync("api/yonetim/kayit", istek));

    public Task<ApiSonucu> FirmaDegistirAsync(Guid firmaId) =>
        TokenAlAsync(istemci => istemci.SendAsync(YetkiliIstek(HttpMethod.Post, "api/yonetim/firma-degistir",
            JsonContent.Create(new FirmaDegistirIstegi(firmaId)))));

    public async Task SessizGirisAsync() => await YenileAsync(bilinenToken: null);

    public async Task CikisAsync()
    {
        try
        {
            await Istemci.PostAsync("api/yonetim/cikis", null);
        }
        catch (HttpRequestException)
        {
            // Sunucuya ulaşılamasa da yerel oturum kapatılır.
        }

        Temizle();
    }

    public async Task<string?> GecerliTokenAsync()
    {
        var token = _accessToken;
        if (token is null || zaman.GetUtcNow() < _yenilenecekZaman)
        {
            return token;
        }

        await YenileAsync(token);
        return _accessToken;
    }

    // bilinenToken: yenilemeyi tetikleyen token. Bekleme sırasında başka bir istek zaten yenilediyse tekrar yenilenmez.
    public async Task<bool> YenileAsync(string? bilinenToken)
    {
        await _yenilemeKilidi.WaitAsync();
        try
        {
            if (bilinenToken is not null && _accessToken != bilinenToken)
            {
                return _accessToken is not null;
            }

            var sonuc = await TokenAlAsync(istemci => istemci.PostAsync("api/yonetim/yenile", null));

            // Yalnızca sunucu oturumu reddederse çıkış yapılır; ağ hatası veya geçici yoğunlukta oturum korunur.
            if (sonuc.Hata?.Durum == HttpStatusCode.Unauthorized)
            {
                Temizle();
            }

            return sonuc.BasariliMi;
        }
        finally
        {
            _yenilemeKilidi.Release();
        }
    }

    private HttpClient Istemci => httpClientFactory.CreateClient(KimlikIstemcisi);

    private async Task<ApiSonucu> TokenAlAsync(Func<HttpClient, Task<HttpResponseMessage>> istek)
    {
        try
        {
            using var cevap = await istek(Istemci);
            if (!cevap.IsSuccessStatusCode)
            {
                return ApiSonucu.Basarisiz(await ApiHatasi.OkuAsync(cevap));
            }

            var tokenCevabi = await cevap.Content.ReadFromJsonAsync<TokenCevabi>();
            await TokenAyarlaAsync(tokenCevabi!.AccessToken);
            return ApiSonucu.Basarili();
        }
        catch (HttpRequestException)
        {
            return ApiSonucu.Basarisiz(ApiHatasi.Baglanti);
        }
    }

    private async Task TokenAyarlaAsync(string accessToken)
    {
        var (kullanici, omur) = TokenOkuyucu.Oku(accessToken);
        _accessToken = accessToken;
        _yenilenecekZaman = zaman.GetUtcNow() + omur - _yenilemePayi;
        Kullanici = kullanici;

        using var cevap = await Istemci.SendAsync(YetkiliIstek(HttpMethod.Get, "api/yonetim/ben"));
        Bilgi = cevap.IsSuccessStatusCode ? await cevap.Content.ReadFromJsonAsync<KullaniciBilgisi>() : null;

        Degisti?.Invoke();
    }

    private HttpRequestMessage YetkiliIstek(HttpMethod metot, string adres, HttpContent? icerik = null)
    {
        var istek = new HttpRequestMessage(metot, adres) { Content = icerik };
        istek.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        return istek;
    }

    private void Temizle()
    {
        var oncedenGirisliMi = _accessToken is not null;
        _accessToken = null;
        Kullanici = TokenOkuyucu.Anonim;
        Bilgi = null;

        if (oncedenGirisliMi)
        {
            Degisti?.Invoke();
        }
    }
}
