using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Yonetim.Contracts;
using OnMuhasebe.Modules.Yonetim.Domain;
using OnMuhasebe.Modules.Yonetim.Infrastructure;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Yonetim.Features;

// Ayarlar > Roller. Yetki tanımları modüllerden toplanır; yalnızca lisanslı modüllerinkiler listelenir ve verilebilir.
// Lisansı kapanan modülün role verilmiş yetkileri silinmez: modül yeniden kiralanınca rol eski haliyle çalışır.
internal static class RolIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        grup.MapGet("/yetki-tanimlari", TanimlarAsync).RequireYetki(YonetimYetkileri.Kullanici, YetkiIslemleri.Goruntule);

        var roller = grup.MapGroup("/roller").RequireYetki(YonetimYetkileri.Kullanici);
        roller.MapGet("", ListeleAsync);
        roller.MapPost("", EkleAsync).Dogrula<RolKaydetIstegi>();
        roller.MapPut("/{id:guid}", GuncelleAsync).Dogrula<RolKaydetIstegi>();
        roller.MapDelete("/{id:guid}", SilAsync);
    }

    internal sealed class Dogrulayici : AbstractValidator<RolKaydetIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.Ad).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Aciklama).MaximumLength(500);
            RuleFor(x => x.Yetkiler).NotNull().Must(l => l.Count <= 500);
        }
    }

    private static async Task<IResult> TanimlarAsync(
        YetkiKatalogu katalog, ILisansSorgusu lisansSorgusu, IMevcutKullanici mevcutKullanici, CancellationToken ct) =>
        TypedResults.Ok(await LisansliKaynaklarAsync(katalog, lisansSorgusu, mevcutKullanici, ct));

    private static async Task<IResult> ListeleAsync(YonetimDbContext db, CancellationToken ct)
    {
        var roller = await db.Roller.AsNoTracking()
            .OrderByDescending(r => r.TamYetkiliMi).ThenBy(r => r.Ad)
            .Select(r => new RolCevabi(r.Id, r.Ad, r.Aciklama, r.TamYetkiliMi, r.Yetkiler,
                db.FirmaKullanicilari.Count(u => u.RolId == r.Id && u.AktifMi)))
            .ToListAsync(ct);
        return TypedResults.Ok(roller);
    }

    private static async Task<IResult> EkleAsync(
        RolKaydetIstegi istek, YonetimDbContext db, YetkiKatalogu katalog, ILisansSorgusu lisansSorgusu, IMevcutKullanici mevcutKullanici,
        CancellationToken ct)
    {
        var yetkiler = await YetkileriDenetleAsync(istek.Yetkiler, [], katalog, lisansSorgusu, mevcutKullanici, ct);
        if (!yetkiler.BasariliMi)
        {
            return yetkiler.Hata.Problem();
        }

        if (await AdKayitliMiAsync(db, istek.Ad, null, ct))
        {
            return YonetimHatalari.RolAdiKayitli.Problem();
        }

        var rol = FirmaRolu.Olustur(istek.Ad, istek.Aciklama, yetkiler.Deger);
        db.Roller.Add(rol);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/yonetim/roller/{rol.Id}", new RolCevabi(rol.Id, rol.Ad, rol.Aciklama, false, rol.Yetkiler, 0));
    }

    private static async Task<IResult> GuncelleAsync(
        Guid id, RolKaydetIstegi istek, YonetimDbContext db, YetkiKatalogu katalog, ILisansSorgusu lisansSorgusu, IMevcutKullanici mevcutKullanici,
        IYetkiSorgusu yetkiSorgusu, CancellationToken ct)
    {
        var rol = await db.Roller.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (rol is null)
        {
            return YonetimHatalari.RolBulunamadi.Problem();
        }

        // Yönetici olmayan, kendi rolüne yetki ekleyerek yetkisini büyütemez.
        if (await db.FirmaKullanicilari.AnyAsync(u => u.KullaniciId == mevcutKullanici.KullaniciId && u.RolId == id, ct)
            && !(await yetkiSorgusu.GetirAsync(mevcutKullanici.FirmaId!.Value, mevcutKullanici.KullaniciId!.Value, ct)).TamYetkiliMi)
        {
            return YonetimHatalari.KendiYetkisiniDegistiremez.Problem();
        }

        var yetkiler = await YetkileriDenetleAsync(istek.Yetkiler, rol.Yetkiler, katalog, lisansSorgusu, mevcutKullanici, ct);
        if (!yetkiler.BasariliMi)
        {
            return yetkiler.Hata.Problem();
        }

        if (await AdKayitliMiAsync(db, istek.Ad, id, ct))
        {
            return YonetimHatalari.RolAdiKayitli.Problem();
        }

        var sonuc = rol.Guncelle(istek.Ad, istek.Aciklama, yetkiler.Deger);
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        await db.SaveChangesAsync(ct);
        await yetkiSorgusu.OnbellektenKaldirAsync(rol.FirmaId, ct);
        var kullaniciSayisi = await db.FirmaKullanicilari.CountAsync(u => u.RolId == rol.Id && u.AktifMi, ct);
        return TypedResults.Ok(new RolCevabi(rol.Id, rol.Ad, rol.Aciklama, false, rol.Yetkiler, kullaniciSayisi));
    }

    // Pasif üyelikler ve geçmiş davetler de rolü gösterir; yalnızca hiç bağlantısı olmayan rol silinir.
    private static async Task<IResult> SilAsync(Guid id, YonetimDbContext db, CancellationToken ct)
    {
        var rol = await db.Roller.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (rol is null)
        {
            return YonetimHatalari.RolBulunamadi.Problem();
        }

        if (rol.TamYetkiliMi)
        {
            return YonetimHatalari.YoneticiRoluDegistirilemez.Problem();
        }

        if (await db.FirmaKullanicilari.AnyAsync(u => u.RolId == id, ct) || await db.FirmaDavetleri.AnyAsync(d => d.RolId == id, ct))
        {
            return YonetimHatalari.RolKullaniliyor.Problem();
        }

        db.Roller.Remove(rol);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    // Verilebilecek yetkiler lisanslı modüllerinkilerdir. Rolde zaten olan lisanssız modül yetkileri korunur.
    private static async Task<Sonuc<List<string>>> YetkileriDenetleAsync(
        IReadOnlyList<string> istenen, IReadOnlyList<string> mevcut, YetkiKatalogu katalog, ILisansSorgusu lisansSorgusu,
        IMevcutKullanici mevcutKullanici, CancellationToken ct)
    {
        var verilebilir = (await LisansliKaynaklarAsync(katalog, lisansSorgusu, mevcutKullanici, ct)).SelectMany(k => k.Yetkiler()).ToHashSet();
        if (istenen.FirstOrDefault(y => !verilebilir.Contains(y)) is { } gecersiz)
        {
            return YonetimHatalari.YetkiGecersiz(gecersiz);
        }

        return istenen.Concat(mevcut.Where(y => !verilebilir.Contains(y) && katalog.Yetkiler.Contains(y))).Distinct().ToList();
    }

    private static async Task<IReadOnlyList<YetkiKaynagi>> LisansliKaynaklarAsync(
        YetkiKatalogu katalog, ILisansSorgusu lisansSorgusu, IMevcutKullanici mevcutKullanici, CancellationToken ct)
    {
        var lisans = await lisansSorgusu.GetirAsync(mevcutKullanici.FirmaId!.Value, ct);
        return katalog.Lisansli(lisans?.Moduller ?? []);
    }

    private static Task<bool> AdKayitliMiAsync(YonetimDbContext db, string ad, Guid? haricId, CancellationToken ct)
    {
        var temiz = ad.Trim();
        return db.Roller.AnyAsync(r => r.Ad == temiz && r.Id != haricId, ct);
    }
}
