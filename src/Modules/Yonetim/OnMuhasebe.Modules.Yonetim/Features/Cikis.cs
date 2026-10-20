using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Yonetim.Infrastructure;
using OnMuhasebe.Modules.Yonetim.Infrastructure.Kimlik;

namespace OnMuhasebe.Modules.Yonetim.Features;

internal static class Cikis
{
    public static void Map(RouteGroupBuilder grup) =>
        grup.MapPost("/cikis", IsleAsync).AllowAnonymous();

    private static async Task<IResult> IsleAsync(
        HttpContext http, YonetimDbContext db, TimeProvider zaman, CancellationToken cancellationToken)
    {
        var token = YenilemeCerezi.Oku(http.Request);
        if (!string.IsNullOrEmpty(token))
        {
            var ozet = TokenServisi.Ozetle(token);
            var kayit = await db.RefreshTokenlar.SingleOrDefaultAsync(t => t.TokenOzeti == ozet, cancellationToken);
            if (kayit is not null)
            {
                kayit.IptalEt(zaman.GetUtcNow().UtcDateTime);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        YenilemeCerezi.Sil(http.Response);
        return TypedResults.NoContent();
    }
}
