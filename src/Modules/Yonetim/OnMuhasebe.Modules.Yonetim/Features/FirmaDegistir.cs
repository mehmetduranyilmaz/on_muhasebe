using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Yonetim.Contracts;
using OnMuhasebe.Modules.Yonetim.Domain;
using OnMuhasebe.Modules.Yonetim.Infrastructure;
using OnMuhasebe.Modules.Yonetim.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;

namespace OnMuhasebe.Modules.Yonetim.Features;

internal static class FirmaDegistir
{
    public static void Map(RouteGroupBuilder grup) =>
        grup.MapPost("/firma-degistir", IsleAsync).RequireAuthorization();

    private static async Task<IResult> IsleAsync(
        FirmaDegistirIstegi istek,
        HttpContext http,
        IMevcutKullanici mevcutKullanici,
        YonetimDbContext db,
        OturumServisi oturumServisi,
        CancellationToken cancellationToken)
    {
        var kullanici = await db.Kullanicilar.SingleOrDefaultAsync(k => k.Id == mevcutKullanici.KullaniciId, cancellationToken);
        if (kullanici is null || !kullanici.AktifMi)
        {
            return YonetimHatalari.OturumGecersiz.Problem();
        }

        var uyeMi = await db.FirmaKullanicilari
            .IgnoreQueryFilters([ModulDbContext.FirmaFiltresi])
            .AnyAsync(u => u.KullaniciId == kullanici.Id && u.FirmaId == istek.FirmaId && u.AktifMi && u.Firma.AktifMi,
                cancellationToken);
        if (!uyeMi)
        {
            return YonetimHatalari.FirmaUyesiDegil.Problem();
        }

        var token = YenilemeCerezi.Oku(http.Request);
        var ozet = token is null ? null : TokenServisi.Ozetle(token);
        var eskiToken = ozet is null
            ? null
            : await db.RefreshTokenlar.SingleOrDefaultAsync(
                t => t.TokenOzeti == ozet && t.KullaniciId == kullanici.Id && t.IptalTarihi == null, cancellationToken);

        var sonuc = await oturumServisi.AcAsync(kullanici, istek.FirmaId, eskiToken, cancellationToken);
        return sonuc.HttpSonucu();
    }
}
