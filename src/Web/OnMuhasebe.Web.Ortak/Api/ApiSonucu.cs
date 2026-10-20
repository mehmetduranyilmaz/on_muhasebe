using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OnMuhasebe.Web.Api;

public sealed record ApiHatasi(string Baslik, string? Kod, IReadOnlyDictionary<string, string[]> Alanlar)
{
    private static readonly IReadOnlyDictionary<string, string[]> _bos = new Dictionary<string, string[]>();

    public static ApiHatasi Baglanti { get; } =
        new("Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edip tekrar deneyin.", "baglanti", _bos);

    // Bağlantı hatasında null.
    public HttpStatusCode? Durum { get; private init; }

    public IEnumerable<string> AlanMesajlari => Alanlar.Values.SelectMany(m => m);

    // API hataları ProblemDetails biçimindedir: title, kod ve doğrulama hatalarında errors alanı.
    public static async Task<ApiHatasi> OkuAsync(HttpResponseMessage cevap, CancellationToken cancellationToken = default)
    {
        try
        {
            var problem = await cevap.Content.ReadFromJsonAsync<ProblemCevabi>(cancellationToken);
            if (problem?.Title is { Length: > 0 } baslik)
            {
                return new ApiHatasi(baslik, problem.Kod, problem.Errors ?? _bos) { Durum = cevap.StatusCode };
            }
        }
        catch (JsonException)
        {
        }
        catch (NotSupportedException)
        {
        }

        return new ApiHatasi(VarsayilanMesaj(cevap.StatusCode), null, _bos) { Durum = cevap.StatusCode };
    }

    private static string VarsayilanMesaj(HttpStatusCode durum) => durum switch
    {
        HttpStatusCode.Unauthorized => "Oturumunuzun süresi doldu. Lütfen tekrar giriş yapın.",
        HttpStatusCode.Forbidden => "Bu işlem için yetkiniz yok.",
        HttpStatusCode.NotFound => "Kayıt bulunamadı.",
        HttpStatusCode.TooManyRequests => "Çok fazla deneme yapıldı. Lütfen biraz bekleyip tekrar deneyin.",
        _ => "Beklenmeyen bir hata oluştu."
    };

    private sealed record ProblemCevabi(string? Title, string? Kod, Dictionary<string, string[]>? Errors);
}

public class ApiSonucu
{
    protected ApiSonucu(ApiHatasi? hata) => Hata = hata;

    public ApiHatasi? Hata { get; }

    [MemberNotNullWhen(false, nameof(Hata))]
    public bool BasariliMi => Hata is null;

    public static ApiSonucu Basarili() => new(null);

    public static ApiSonucu Basarisiz(ApiHatasi hata) => new(hata);
}

public sealed class ApiSonucu<T> : ApiSonucu
{
    private readonly T? _deger;

    private ApiSonucu(T? deger, ApiHatasi? hata) : base(hata) => _deger = deger;

    public T Deger => BasariliMi ? _deger! : throw new InvalidOperationException("Başarısız sonucun değeri okunamaz.");

    public static ApiSonucu<T> Basarili(T deger) => new(deger, null);

    public static new ApiSonucu<T> Basarisiz(ApiHatasi hata) => new(default, hata);
}
