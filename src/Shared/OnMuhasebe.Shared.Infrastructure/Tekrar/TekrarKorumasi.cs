using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using OnMuhasebe.Shared.Infrastructure.Kimlik;

namespace OnMuhasebe.Shared.Infrastructure.Tekrar;

// İşlem anahtarıyla daha önce başlatılmış istek. Tamamlanmamışsa ilk istek hâlâ işleniyordur (ya da sonucu belirsizdir).
public sealed record KayitliIstek(string Ozet, bool Tamamlandi, DateTime Baslangic, int? DurumKodu, string? IcerikTuru, string? Konum, byte[]? Govde);

public interface IIstekKaydiDeposu
{
    // Anahtar ilk kez geliyorsa kaydı açıp kimliğini, daha önce geldiyse mevcut kaydı döner (aynı anda gelen iki istekten yalnızca biri açabilir).
    Task<(Guid? YeniId, KayitliIstek? Mevcut)> BaslatAsync(string anahtar, string ozet, CancellationToken cancellationToken);

    Task TamamlaAsync(Guid id, int durumKodu, string? icerikTuru, string? konum, byte[] govde, CancellationToken cancellationToken);

    Task SilAsync(Guid id, CancellationToken cancellationToken);
}

// Yazma isteklerinin tekrarına karşı koruma (Idempotency-Key). Aynı anahtar ve aynı içerikle gelen istek yeniden işlenmez, ilk başarılı
// cevap aynen döner. Böylece cevabı ağda kaybolan bir tahsilatın tekrar gönderilmesi ikinci kayıt oluşturmaz.
// Yalnızca başarılı cevaplar saklanır: doğrulama hatası alan form düzeltilip aynı anahtarla yeniden gönderilebilir.
// Oturum açmamış istekler (giriş, kayıt) ve çerez yazan cevaplar (token) saklanmaz.
internal sealed class TekrarKorumasi(RequestDelegate sonraki)
{
    public const string Baslik = "Idempotency-Key";
    public const string TekrarBasligi = "Idempotent-Replayed";
    private const int AzamiSaklanacakGovde = 1024 * 1024;
    private static readonly TimeSpan _belirsizlikSuresi = TimeSpan.FromMinutes(2);

    public async Task InvokeAsync(HttpContext http, IMevcutKullanici kullanici, IIstekKaydiDeposu depo, TimeProvider zaman)
    {
        var anahtar = http.Request.Headers[Baslik].ToString();
        if (anahtar.Length == 0 || !YazmaMi(http.Request.Method) || kullanici.FirmaId is null || kullanici.KullaniciId is null)
        {
            await sonraki(http);
            return;
        }

        if (anahtar.Length is < 8 or > 100 || !anahtar.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
        {
            await Problem(http, StatusCodes.Status400BadRequest, "tekrar_anahtari_gecersiz", "İşlem anahtarı geçersiz.");
            return;
        }

        var ct = http.RequestAborted;
        var ozet = await OzetAsync(http.Request, ct);
        var (yeniId, mevcut) = await depo.BaslatAsync(anahtar, ozet, ct);
        if (mevcut is not null)
        {
            await MevcutIcinCevapAsync(http, mevcut, ozet, zaman);
            return;
        }

        var asilGovde = http.Response.Body;
        await using var tampon = new MemoryStream();
        http.Response.Body = tampon;
        try
        {
            await sonraki(http);
        }
        catch
        {
            http.Response.Body = asilGovde;
            await depo.SilAsync(yeniId!.Value, CancellationToken.None);
            throw;
        }

        http.Response.Body = asilGovde;
        var cevap = http.Response;
        if (cevap.StatusCode is >= 200 and < 300 && !cevap.Headers.ContainsKey("Set-Cookie") && tampon.Length <= AzamiSaklanacakGovde)
        {
            await depo.TamamlaAsync(yeniId!.Value, cevap.StatusCode, cevap.ContentType, cevap.Headers.Location.ToString() is { Length: > 0 } k ? k : null,
                tampon.ToArray(), CancellationToken.None);
        }
        else
        {
            await depo.SilAsync(yeniId!.Value, CancellationToken.None);
        }

        tampon.Position = 0;
        await tampon.CopyToAsync(asilGovde, ct);
    }

    private static async Task MevcutIcinCevapAsync(HttpContext http, KayitliIstek mevcut, string ozet, TimeProvider zaman)
    {
        if (mevcut.Ozet != ozet)
        {
            await Problem(http, StatusCodes.Status422UnprocessableEntity, "tekrar_anahtari_uyusmuyor",
                "Bu işlem anahtarı farklı bir istekle kullanılmış. Önceki kaydınız işlenmiş olabilir; sayfayı yenileyip kontrol edin.");
            return;
        }

        if (!mevcut.Tamamlandi)
        {
            var belirsiz = zaman.GetUtcNow().UtcDateTime - mevcut.Baslangic > _belirsizlikSuresi;
            await Problem(http, StatusCodes.Status409Conflict, belirsiz ? "islem_sonucu_belirsiz" : "islem_suruyor",
                belirsiz
                    ? "Bu işlemin önceki gönderiminin sonucu belirsiz; tekrar kaydetmeden önce listeden kontrol edin."
                    : "Aynı işlem şu anda işleniyor; lütfen bekleyin.");
            return;
        }

        http.Response.StatusCode = mevcut.DurumKodu!.Value;
        http.Response.Headers[TekrarBasligi] = "true";
        if (mevcut.IcerikTuru is not null)
        {
            http.Response.ContentType = mevcut.IcerikTuru;
        }

        if (mevcut.Konum is not null)
        {
            http.Response.Headers.Location = mevcut.Konum;
        }

        if (mevcut.Govde is { Length: > 0 } govde)
        {
            await http.Response.Body.WriteAsync(govde, http.RequestAborted);
        }
    }

    // Aynı anahtar başka bir istekle (farklı adres ya da gövde) kullanılırsa yakalanabilsin diye istek özeti saklanır.
    private static async Task<string> OzetAsync(HttpRequest istek, CancellationToken ct)
    {
        istek.EnableBuffering();
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        sha.AppendData(Encoding.UTF8.GetBytes($"{istek.Method} {istek.Path}{istek.QueryString}\n"));
        var tampon = new byte[81920];
        int okunan;
        while ((okunan = await istek.Body.ReadAsync(tampon, ct)) > 0)
        {
            sha.AppendData(tampon, 0, okunan);
        }

        istek.Body.Position = 0;
        return Convert.ToHexString(sha.GetHashAndReset());
    }

    private static bool YazmaMi(string metot) => HttpMethods.IsPost(metot) || HttpMethods.IsPut(metot) || HttpMethods.IsPatch(metot) || HttpMethods.IsDelete(metot);

    private static Task Problem(HttpContext http, int durum, string kod, string baslik) =>
        Microsoft.AspNetCore.Http.Results.Problem(statusCode: durum, title: baslik, extensions: new Dictionary<string, object?> { ["kod"] = kod }).ExecuteAsync(http);
}

public static class TekrarKorumasiExtensions
{
    // Kimlik doğrulamadan sonra çalışmalı (firma ve kullanıcı anahtarın kapsamıdır).
    public static IApplicationBuilder UseTekrarKorumasi(this IApplicationBuilder app) => app.UseMiddleware<TekrarKorumasi>();
}
