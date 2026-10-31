using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Yonetim.Contracts;
using OnMuhasebe.Modules.Yonetim.Domain;
using OnMuhasebe.Modules.Yonetim.Infrastructure;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;

namespace OnMuhasebe.Modules.Yonetim.Features;

internal static class Ben
{
    public static void Map(RouteGroupBuilder grup) =>
        grup.MapGet("/ben", IsleAsync).RequireAuthorization();

    private static async Task<IResult> IsleAsync(
        IMevcutKullanici mevcutKullanici,
        YonetimDbContext db,
        ILisansSorgusu lisansSorgusu,
        IYetkiSorgusu yetkiSorgusu,
        YetkiKatalogu katalog,
        TimeProvider zaman,
        CancellationToken cancellationToken)
    {
        var kullanici = await db.Kullanicilar.AsNoTracking()
            .SingleOrDefaultAsync(k => k.Id == mevcutKullanici.KullaniciId, cancellationToken);
        if (kullanici is null)
        {
            return YonetimHatalari.OturumGecersiz.Problem();
        }

        var firmalar = await db.FirmaKullanicilari
            .IgnoreQueryFilters([ModulDbContext.FirmaFiltresi])
            .AsNoTracking()
            .Where(u => u.KullaniciId == kullanici.Id && u.AktifMi && u.Firma.AktifMi)
            .OrderBy(u => u.Firma.Unvan)
            .Select(u => new FirmaOzeti(u.FirmaId, u.Firma.Unvan, u.Rol.Ad))
            .ToListAsync(cancellationToken);

        var aktifFirma = firmalar.Find(f => f.Id == mevcutKullanici.FirmaId);
        LisansOzeti? lisans = null;
        YetkiOzeti? yetki = null;
        if (aktifFirma is not null && await lisansSorgusu.GetirAsync(aktifFirma.Id, cancellationToken) is { } durum)
        {
            lisans = new LisansOzeti(durum.Moduller, durum.BitisTarihi, durum.SuresiDolduMu(zaman.GetUtcNow().UtcDateTime));

            // Ekran menü ve düğmeleri buna göre gösterir; lisanssız modülün yetkisi listelenmez.
            var lisansli = katalog.Lisansli(durum.Moduller).SelectMany(k => k.Yetkiler()).ToHashSet();
            var etkin = await yetkiSorgusu.GetirAsync(aktifFirma.Id, kullanici.Id, cancellationToken);
            yetki = new YetkiOzeti(etkin.TamYetkiliMi, lisansli.Where(etkin.VarMi).Order().ToList());
        }

        return TypedResults.Ok(new KullaniciBilgisi(
            kullanici.Id, kullanici.Ad, kullanici.Soyad, kullanici.Eposta, kullanici.SistemYoneticisiMi,
            aktifFirma, firmalar, lisans, yetki));
    }
}
