using System.Net.Http.Json;

namespace OnMuhasebe.Web.Api;

// API çağrılarının tek giriş noktası: token ekleme ve yenileme ApiYetkiHandler'da, hata çevirimi burada yapılır.
// Yazma isteklerine işlem anahtarı (Idempotency-Key) eklenir. Form kendi anahtarını verirse (IslemAnahtari) aynı formun
// yeniden gönderimi sunucuda tek işlem sayılır; vermezse her çağrı yeni anahtar alır (otomatik yeniden denemeleri korur).
public sealed class ApiIstemcisi(IHttpClientFactory httpClientFactory)
{
    public const string Ad = "api";
    private const string AnahtarBasligi = "Idempotency-Key";

    private HttpClient Istemci => httpClientFactory.CreateClient(Ad);

    public Task<ApiSonucu<T>> GetAsync<T>(string adres, CancellationToken cancellationToken = default) =>
        GonderAsync<T>(() => Istemci.GetAsync(adres, cancellationToken), cancellationToken);

    public Task<ApiSonucu<T>> PostAsync<T>(string adres, object govde, CancellationToken cancellationToken = default, string? islemAnahtari = null) =>
        GonderAsync<T>(() => YazAsync(HttpMethod.Post, adres, JsonContent.Create(govde), islemAnahtari, cancellationToken), cancellationToken);

    public Task<ApiSonucu> PostAsync(string adres, object? govde = null, CancellationToken cancellationToken = default, string? islemAnahtari = null) =>
        GonderAsync(() => YazAsync(HttpMethod.Post, adres, JsonContent.Create(govde), islemAnahtari, cancellationToken), cancellationToken);

    // Tek dosyalı multipart yükleme; alan adı API'deki IFormFile parametresiyle aynı olmalı.
    public Task<ApiSonucu<T>> DosyaYukleAsync<T>(
        string adres, string alanAdi, Stream icerik, string dosyaAdi, CancellationToken cancellationToken = default, string? islemAnahtari = null) =>
        GonderAsync<T>(() => YazAsync(HttpMethod.Post, adres, new MultipartFormDataContent { { new StreamContent(icerik), alanAdi, dosyaAdi } },
            islemAnahtari, cancellationToken), cancellationToken);

    public Task<ApiSonucu> PutAsync(string adres, object govde, CancellationToken cancellationToken = default, string? islemAnahtari = null) =>
        GonderAsync(() => YazAsync(HttpMethod.Put, adres, JsonContent.Create(govde), islemAnahtari, cancellationToken), cancellationToken);

    public Task<ApiSonucu<T>> PutAsync<T>(string adres, object govde, CancellationToken cancellationToken = default, string? islemAnahtari = null) =>
        GonderAsync<T>(() => YazAsync(HttpMethod.Put, adres, JsonContent.Create(govde), islemAnahtari, cancellationToken), cancellationToken);

    public Task<ApiSonucu> DeleteAsync(string adres, CancellationToken cancellationToken = default, string? islemAnahtari = null) =>
        GonderAsync(() => YazAsync(HttpMethod.Delete, adres, null, islemAnahtari, cancellationToken), cancellationToken);

    public Task<ApiSonucu<T>> DeleteAsync<T>(string adres, CancellationToken cancellationToken = default, string? islemAnahtari = null) =>
        GonderAsync<T>(() => YazAsync(HttpMethod.Delete, adres, null, islemAnahtari, cancellationToken), cancellationToken);

    private Task<HttpResponseMessage> YazAsync(HttpMethod metot, string adres, HttpContent? icerik, string? islemAnahtari, CancellationToken cancellationToken)
    {
        var istek = new HttpRequestMessage(metot, adres) { Content = icerik };
        istek.Headers.TryAddWithoutValidation(AnahtarBasligi, islemAnahtari ?? Guid.NewGuid().ToString("N"));
        return Istemci.SendAsync(istek, cancellationToken);
    }

    private static async Task<ApiSonucu<T>> GonderAsync<T>(Func<Task<HttpResponseMessage>> istek, CancellationToken cancellationToken)
    {
        try
        {
            using var cevap = await istek();
            if (!cevap.IsSuccessStatusCode)
            {
                return ApiSonucu<T>.Basarisiz(await ApiHatasi.OkuAsync(cevap, cancellationToken));
            }

            var deger = await cevap.Content.ReadFromJsonAsync<T>(cancellationToken);
            return ApiSonucu<T>.Basarili(deger!);
        }
        catch (HttpRequestException)
        {
            return ApiSonucu<T>.Basarisiz(ApiHatasi.Baglanti);
        }
    }

    private static async Task<ApiSonucu> GonderAsync(Func<Task<HttpResponseMessage>> istek, CancellationToken cancellationToken)
    {
        try
        {
            using var cevap = await istek();
            return cevap.IsSuccessStatusCode
                ? ApiSonucu.Basarili()
                : ApiSonucu.Basarisiz(await ApiHatasi.OkuAsync(cevap, cancellationToken));
        }
        catch (HttpRequestException)
        {
            return ApiSonucu.Basarisiz(ApiHatasi.Baglanti);
        }
    }
}
