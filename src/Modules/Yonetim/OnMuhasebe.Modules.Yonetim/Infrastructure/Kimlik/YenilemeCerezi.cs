using Microsoft.AspNetCore.Http;

namespace OnMuhasebe.Modules.Yonetim.Infrastructure.Kimlik;

// Refresh token yalnızca HttpOnly çerezde taşınır; JavaScript erişemez, sadece kimlik endpoint'lerine gönderilir.
internal static class YenilemeCerezi
{
    private const string Ad = "om_yenile";
    private const string Yol = "/api/yonetim";

    public static void Yaz(HttpResponse response, string token, DateTime bitis) =>
        response.Cookies.Append(Ad, token, Secenekler(bitis));

    public static string? Oku(HttpRequest request) => request.Cookies[Ad];

    public static void Sil(HttpResponse response) => response.Cookies.Delete(Ad, Secenekler(null));

    private static CookieOptions Secenekler(DateTime? bitis) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Yol,
        Expires = bitis,
        IsEssential = true
    };
}
