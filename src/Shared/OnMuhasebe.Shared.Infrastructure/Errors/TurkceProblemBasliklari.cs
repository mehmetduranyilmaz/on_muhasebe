using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace OnMuhasebe.Shared.Infrastructure.Errors;

// Çerçevenin kendi ürettiği yanıtlar (429, 404, 405...) İngilizce başlık taşır; kullanıcıya Türkçe gösterilsin.
// Uygulamanın kendi Türkçe başlıklarına dokunulmaz.
internal static class TurkceProblemBasliklari
{
    private static readonly Dictionary<int, string> _basliklar = new()
    {
        [StatusCodes.Status400BadRequest] = "İstek geçersiz.",
        [StatusCodes.Status401Unauthorized] = "Oturumunuzun süresi doldu. Lütfen tekrar giriş yapın.",
        [StatusCodes.Status403Forbidden] = "Bu işlem için yetkiniz yok.",
        [StatusCodes.Status404NotFound] = "Kayıt bulunamadı.",
        [StatusCodes.Status405MethodNotAllowed] = "Bu işlem desteklenmiyor.",
        [StatusCodes.Status429TooManyRequests] = "Çok fazla deneme yapıldı. Lütfen biraz bekleyip tekrar deneyin.",
        [StatusCodes.Status500InternalServerError] = "Beklenmeyen bir hata oluştu."
    };

    public static void Uygula(ProblemDetailsContext context)
    {
        var durum = context.ProblemDetails.Status ?? context.HttpContext.Response.StatusCode;
        var baslik = context.ProblemDetails.Title;
        var cerceveBasligiMi = string.IsNullOrEmpty(baslik) || baslik == ReasonPhrases.GetReasonPhrase(durum)
            || baslik == "One or more validation errors occurred.";

        if (cerceveBasligiMi && _basliklar.TryGetValue(durum, out var turkce))
        {
            context.ProblemDetails.Title = turkce;
        }
    }
}
