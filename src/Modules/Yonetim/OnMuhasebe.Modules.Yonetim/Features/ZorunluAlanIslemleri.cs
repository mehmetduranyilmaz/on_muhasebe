using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Yonetim.Domain;
using OnMuhasebe.Modules.Yonetim.Infrastructure;
using OnMuhasebe.Shared.Contracts.Ayarlar;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Modules;
using OnMuhasebe.Shared.Infrastructure.Results;

namespace OnMuhasebe.Modules.Yonetim.Features;

// Ayarlar > Zorunlu alanlar. Tanımlar modüllerden toplanır; yalnızca firmanın lisanslı modüllerininkiler listelenir ve kaydedilebilir.
internal static class ZorunluAlanIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        grup.MapGet("/zorunlu-alanlar", GetirAsync).RequireAuthorization();
        grup.MapPut("/zorunlu-alanlar", KaydetAsync).RequireAuthorization(p => p.RequireRole(Roller.Yonetici));
    }

    private static async Task<IResult> GetirAsync(
        IEnumerable<IModul> moduller, ILisansSorgusu lisansSorgusu, ZorunluAlanSorgusu sorgu, IMevcutKullanici mevcutKullanici, CancellationToken ct)
    {
        var tanimlar = await TanimlarAsync(moduller, lisansSorgusu, mevcutKullanici, ct);
        var secili = await sorgu.SeciliAsync(ct);
        return TypedResults.Ok(new ZorunluAlanlarCevabi(tanimlar, tanimlar.Select(t => t.Kod).Where(secili.Contains).ToList()));
    }

    private static async Task<IResult> KaydetAsync(
        ZorunluAlanlarIstegi istek, IEnumerable<IModul> moduller, ILisansSorgusu lisansSorgusu, ZorunluAlanSorgusu sorgu, IMevcutKullanici mevcutKullanici,
        YonetimDbContext db, CancellationToken ct)
    {
        var tanimlar = await TanimlarAsync(moduller, lisansSorgusu, mevcutKullanici, ct);
        var gecerli = tanimlar.Select(t => t.Kod).ToHashSet();
        var istenen = (istek.Secili ?? []).Distinct().ToList();
        if (istenen.FirstOrDefault(k => !gecerli.Contains(k)) is { } bilinmeyen)
        {
            return Hata.Dogrulama("zorunlu_alan_gecersiz", $"\"{bilinmeyen}\" zorunlu yapılabilecek bir alan değil.").Problem();
        }

        // Lisansı kapanmış modülün eski seçimleri korunur; yalnızca listelenen alanlar güncellenir.
        var mevcut = await db.ZorunluAlanlar.ToListAsync(ct);
        db.ZorunluAlanlar.RemoveRange(mevcut.Where(z => gecerli.Contains(z.Kod) && !istenen.Contains(z.Kod)));
        db.ZorunluAlanlar.AddRange(istenen.Where(k => mevcut.All(z => z.Kod != k)).Select(FirmaZorunluAlani.Olustur));
        await db.SaveChangesAsync(ct);
        await sorgu.OnbellektenKaldirAsync(mevcutKullanici.FirmaId!.Value, ct);
        return TypedResults.Ok(new ZorunluAlanlarCevabi(tanimlar, tanimlar.Select(t => t.Kod).Where(istenen.Contains).ToList()));
    }

    private static async Task<List<ZorunluAlanTanimi>> TanimlarAsync(
        IEnumerable<IModul> moduller, ILisansSorgusu lisansSorgusu, IMevcutKullanici mevcutKullanici, CancellationToken ct)
    {
        var lisans = await lisansSorgusu.GetirAsync(mevcutKullanici.FirmaId!.Value, ct);
        var acik = lisans?.Moduller.ToHashSet() ?? [];
        return moduller.SelectMany(m => m.ZorunluAlanlar).Where(t => acik.Contains(t.Modul)).ToList();
    }
}
