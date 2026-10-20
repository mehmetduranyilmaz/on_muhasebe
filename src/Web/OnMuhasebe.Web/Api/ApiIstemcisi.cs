using System.Net.Http.Json;

namespace OnMuhasebe.Web.Api;

// API çağrılarının tek giriş noktası: token ekleme ve yenileme ApiYetkiHandler'da, hata çevirimi burada yapılır.
public sealed class ApiIstemcisi(IHttpClientFactory httpClientFactory)
{
    public const string Ad = "api";

    private HttpClient Istemci => httpClientFactory.CreateClient(Ad);

    public Task<ApiSonucu<T>> GetAsync<T>(string adres, CancellationToken cancellationToken = default) =>
        GonderAsync<T>(() => Istemci.GetAsync(adres, cancellationToken), cancellationToken);

    public Task<ApiSonucu<T>> PostAsync<T>(string adres, object govde, CancellationToken cancellationToken = default) =>
        GonderAsync<T>(() => Istemci.PostAsJsonAsync(adres, govde, cancellationToken), cancellationToken);

    public Task<ApiSonucu> PostAsync(string adres, object? govde = null, CancellationToken cancellationToken = default) =>
        GonderAsync(() => Istemci.PostAsJsonAsync(adres, govde, cancellationToken), cancellationToken);

    public Task<ApiSonucu> PutAsync(string adres, object govde, CancellationToken cancellationToken = default) =>
        GonderAsync(() => Istemci.PutAsJsonAsync(adres, govde, cancellationToken), cancellationToken);

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
