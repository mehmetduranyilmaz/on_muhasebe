using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Yonetim.Contracts;
using OnMuhasebe.Modules.Yonetim.Domain;
using OnMuhasebe.Modules.Yonetim.Infrastructure;
using OnMuhasebe.Modules.Yonetim.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Yonetim.Features;

// Firma yöneticisinin kendi firmasının kullanıcılarını yönetmesi. Sorgular firma filtresiyle otomatik olarak oturumdaki firmaya sınırlanır.
// Kullanıcı eklemek davet göndermektir; yanıt, davet edilen e-postanın sistemde kayıtlı olup olmadığını ele vermez.
internal static class KullaniciIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var kullanicilar = grup.MapGroup("/kullanicilar")
            .RequireAuthorization(p => p.RequireRole(Roller.Yonetici));

        kullanicilar.MapGet("", ListeleAsync);
        kullanicilar.MapPost("", DavetEtAsync).Dogrula<KullaniciDavetIstegi>();
        kullanicilar.MapPost("/{kullaniciId:guid}/pasiflestir", PasiflestirAsync);
        kullanicilar.MapGet("/davetler", DavetleriListeleAsync);
        kullanicilar.MapPost("/davetler/{id:guid}/yeniden-gonder", YenidenGonderAsync);
        kullanicilar.MapDelete("/davetler/{id:guid}", IptalEtAsync);
    }

    internal sealed class DavetDogrulayici : AbstractValidator<KullaniciDavetIstegi>
    {
        public DavetDogrulayici()
        {
            RuleFor(x => x.Eposta).Eposta();
            RuleFor(x => x.Rol).FirmaRolu();
        }
    }

    private static async Task<IResult> ListeleAsync(YonetimDbContext db, CancellationToken cancellationToken)
    {
        var liste = await db.FirmaKullanicilari
            .AsNoTracking()
            .OrderBy(u => u.Kullanici.Ad).ThenBy(u => u.Kullanici.Soyad)
            .Select(u => new FirmaKullanicisiCevabi(
                u.KullaniciId, u.Kullanici.Ad, u.Kullanici.Soyad, u.Kullanici.Eposta, u.Rol, u.AktifMi))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(liste);
    }

    private static async Task<IResult> DavetleriListeleAsync(YonetimDbContext db, TimeProvider zaman, CancellationToken cancellationToken)
    {
        var simdi = zaman.GetUtcNow().UtcDateTime;
        var liste = await db.FirmaDavetleri
            .AsNoTracking()
            .Where(d => d.KabulTarihi == null && d.IptalTarihi == null)
            .OrderByDescending(d => d.SonGonderimTarihi)
            .Select(d => new DavetOzeti(d.Id, d.Eposta, d.Rol, d.SonGonderimTarihi, d.BitisTarihi))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(liste);
    }

    private static async Task<IResult> DavetEtAsync(
        KullaniciDavetIstegi istek,
        IMevcutKullanici mevcutKullanici,
        YonetimDbContext db,
        KimlikPostasi posta,
        ILisansSorgusu lisansSorgusu,
        TimeProvider zaman,
        CancellationToken cancellationToken)
    {
        var simdi = zaman.GetUtcNow().UtcDateTime;
        if (await SaltOkunurMuAsync(lisansSorgusu, mevcutKullanici, simdi, cancellationToken))
        {
            return LisansHatalari.SuresiDoldu.Problem();
        }

        var eposta = Kullanici.EpostaNormalize(istek.Eposta);
        if (await db.FirmaKullanicilari.AnyAsync(u => u.Kullanici.Eposta == eposta && u.AktifMi, cancellationToken))
        {
            return YonetimHatalari.ZatenFirmaUyesi.Problem();
        }

        // Bekleyen davetler de koltuk sayılır: limit, davetler kabul edilince aşılmasın.
        if (await db.FirmaLisanslari.Select(l => l.Paket.KullaniciLimiti).SingleOrDefaultAsync(cancellationToken) is { } limit
            && await db.FirmaKullanicilari.CountAsync(u => u.AktifMi, cancellationToken)
                + await db.FirmaDavetleri.CountAsync(d => d.Eposta != eposta && d.KabulTarihi == null && d.IptalTarihi == null && d.BitisTarihi > simdi, cancellationToken)
                >= limit)
        {
            return YonetimHatalari.KullaniciLimiti(limit).Problem();
        }

        // Aynı kişiye önceki bekleyen davetler geçersiz olur; geçerli olan yalnızca son gönderilen bağlantıdır.
        var oncekiler = await db.FirmaDavetleri
            .Where(d => d.Eposta == eposta && d.KabulTarihi == null && d.IptalTarihi == null)
            .ToListAsync(cancellationToken);
        oncekiler.ForEach(d => d.IptalEt(simdi));

        var (kod, ozet) = TekKullanimlikKod.Uret();
        var davet = FirmaDaveti.Olustur(eposta, istek.Rol, ozet, simdi);
        db.FirmaDavetleri.Add(davet);
        await db.SaveChangesAsync(cancellationToken);
        await DavetGonderAsync(db, posta, mevcutKullanici, davet, kod, cancellationToken);

        return TypedResults.Created($"/api/yonetim/kullanicilar/davetler/{davet.Id}",
            new DavetOzeti(davet.Id, davet.Eposta, davet.Rol, davet.SonGonderimTarihi, davet.BitisTarihi));
    }

    private static async Task<IResult> YenidenGonderAsync(
        Guid id,
        IMevcutKullanici mevcutKullanici,
        YonetimDbContext db,
        KimlikPostasi posta,
        ILisansSorgusu lisansSorgusu,
        TimeProvider zaman,
        CancellationToken cancellationToken)
    {
        var simdi = zaman.GetUtcNow().UtcDateTime;
        if (await SaltOkunurMuAsync(lisansSorgusu, mevcutKullanici, simdi, cancellationToken))
        {
            return LisansHatalari.SuresiDoldu.Problem();
        }

        var davet = await db.FirmaDavetleri.SingleOrDefaultAsync(d => d.Id == id && d.KabulTarihi == null && d.IptalTarihi == null, cancellationToken);
        if (davet is null)
        {
            return YonetimHatalari.DavetBulunamadi.Problem();
        }

        if (davet.SonGonderimTarihi > simdi - KullaniciKodu.YenidenGondermeAraligi)
        {
            return YonetimHatalari.DavetCokSik.Problem();
        }

        var (kod, ozet) = TekKullanimlikKod.Uret();
        davet.YenidenGonder(ozet, simdi);
        await db.SaveChangesAsync(cancellationToken);
        await DavetGonderAsync(db, posta, mevcutKullanici, davet, kod, cancellationToken);
        return TypedResults.Ok(new DavetOzeti(davet.Id, davet.Eposta, davet.Rol, davet.SonGonderimTarihi, davet.BitisTarihi));
    }

    private static async Task<IResult> IptalEtAsync(Guid id, YonetimDbContext db, TimeProvider zaman, CancellationToken cancellationToken)
    {
        var davet = await db.FirmaDavetleri.SingleOrDefaultAsync(d => d.Id == id && d.KabulTarihi == null, cancellationToken);
        if (davet is null)
        {
            return YonetimHatalari.DavetBulunamadi.Problem();
        }

        davet.IptalEt(zaman.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    // Lisansı dolan firmada kayıtlar salt okunur; yeni kullanıcı daveti de yazma sayılır.
    private static async Task<bool> SaltOkunurMuAsync(
        ILisansSorgusu lisansSorgusu, IMevcutKullanici mevcutKullanici, DateTime simdi, CancellationToken cancellationToken) =>
        await lisansSorgusu.GetirAsync(mevcutKullanici.FirmaId!.Value, cancellationToken) is { } lisans && lisans.SuresiDolduMu(simdi);

    private static async Task DavetGonderAsync(
        YonetimDbContext db, KimlikPostasi posta, IMevcutKullanici mevcutKullanici, FirmaDaveti davet, string kod, CancellationToken cancellationToken)
    {
        var firmaUnvani = await db.Firmalar.Where(f => f.Id == davet.FirmaId).Select(f => f.Unvan).SingleAsync(cancellationToken);
        var davetEden = await db.Kullanicilar.Where(k => k.Id == mevcutKullanici.KullaniciId)
            .Select(k => k.Ad + " " + k.Soyad).SingleAsync(cancellationToken);
        await posta.DavetAsync(davet.Eposta, firmaUnvani, davetEden, kod, cancellationToken);
    }

    private static async Task<IResult> PasiflestirAsync(
        Guid kullaniciId,
        IMevcutKullanici mevcutKullanici,
        YonetimDbContext db,
        TimeProvider zaman,
        CancellationToken cancellationToken)
    {
        var uyelik = await db.FirmaKullanicilari.SingleOrDefaultAsync(u => u.KullaniciId == kullaniciId, cancellationToken);
        if (uyelik is null)
        {
            return YonetimHatalari.KullaniciBulunamadi.Problem();
        }

        var sonuc = uyelik.Pasiflestir(mevcutKullanici.KullaniciId!.Value);
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        await db.SaveChangesAsync(cancellationToken);

        // Bu firmadaki açık oturumları kapat; access token en geç süresi dolunca (15 dk) geçersizleşir.
        var simdi = zaman.GetUtcNow().UtcDateTime;
        await db.RefreshTokenlar
            .Where(t => t.KullaniciId == kullaniciId && t.FirmaId == uyelik.FirmaId && t.IptalTarihi == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IptalTarihi, simdi), cancellationToken);

        return TypedResults.NoContent();
    }
}
