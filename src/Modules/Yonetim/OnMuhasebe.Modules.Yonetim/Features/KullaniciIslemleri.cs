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
using OnMuhasebe.Shared.Domain.Results;
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
        var kullanicilar = grup.MapGroup("/kullanicilar").RequireYetki(YonetimYetkileri.Kullanici);

        kullanicilar.MapGet("", ListeleAsync);
        kullanicilar.MapPost("", DavetEtAsync).Dogrula<KullaniciDavetIstegi>();
        kullanicilar.MapPost("/{kullaniciId:guid}/pasiflestir", PasiflestirAsync).Islem(YetkiIslemleri.Duzenle);
        kullanicilar.MapPut("/{kullaniciId:guid}/yetkiler", YetkileriAtaAsync).Dogrula<KullaniciYetkiIstegi>();
        kullanicilar.MapGet("/davetler", DavetleriListeleAsync);
        kullanicilar.MapPost("/davetler/{id:guid}/yeniden-gonder", YenidenGonderAsync).Islem(YetkiIslemleri.Ekle);
        kullanicilar.MapDelete("/davetler/{id:guid}", IptalEtAsync);
    }

    internal sealed class DavetDogrulayici : AbstractValidator<KullaniciDavetIstegi>
    {
        public DavetDogrulayici()
        {
            RuleFor(x => x.Eposta).Eposta();
            RuleFor(x => x.RolId).NotEmpty();
        }
    }

    internal sealed class YetkiDogrulayici : AbstractValidator<KullaniciYetkiIstegi>
    {
        public YetkiDogrulayici()
        {
            RuleFor(x => x.RolId).NotEmpty();
            RuleFor(x => x.EkYetkiler).Must(l => l is null || l.Count <= 500);
            RuleFor(x => x.YasakYetkiler).Must(l => l is null || l.Count <= 500);
        }
    }

    private static async Task<IResult> ListeleAsync(YonetimDbContext db, CancellationToken cancellationToken)
    {
        var liste = await db.FirmaKullanicilari
            .AsNoTracking()
            .OrderBy(u => u.Kullanici.Ad).ThenBy(u => u.Kullanici.Soyad)
            .Select(u => new FirmaKullanicisiCevabi(
                u.KullaniciId, u.Kullanici.Ad, u.Kullanici.Soyad, u.Kullanici.Eposta, u.RolId, u.Rol.Ad, u.Rol.TamYetkiliMi,
                u.EkYetkiler, u.YasakYetkiler, u.AktifMi, u.Isyerleri, u.VarsayilanIsyeriId))
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
            .Select(d => new DavetOzeti(d.Id, d.Eposta, d.RolId, d.Rol.Ad, d.SonGonderimTarihi, d.BitisTarihi))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(liste);
    }

    private static async Task<IResult> DavetEtAsync(
        KullaniciDavetIstegi istek,
        IMevcutKullanici mevcutKullanici,
        YonetimDbContext db,
        KimlikPostasi posta,
        ILisansSorgusu lisansSorgusu,
        IYetkiSorgusu yetkiSorgusu,
        TimeProvider zaman,
        CancellationToken cancellationToken)
    {
        var simdi = zaman.GetUtcNow().UtcDateTime;
        if (await SaltOkunurMuAsync(lisansSorgusu, mevcutKullanici, simdi, cancellationToken))
        {
            return LisansHatalari.SuresiDoldu.Problem();
        }

        var rol = await db.Roller.AsNoTracking().SingleOrDefaultAsync(r => r.Id == istek.RolId, cancellationToken);
        if (rol is null)
        {
            return YonetimHatalari.RolBulunamadi.Problem();
        }

        if (rol.TamYetkiliMi && !await TamYetkiliMiAsync(yetkiSorgusu, mevcutKullanici, cancellationToken))
        {
            return YonetimHatalari.YoneticiAtamaYetkisiYok.Problem();
        }

        var eposta = Kullanici.EpostaNormalize(istek.Eposta);
        if (await db.FirmaKullanicilari.AnyAsync(u => u.Kullanici.Eposta == eposta && u.AktifMi, cancellationToken))
        {
            return YonetimHatalari.ZatenFirmaUyesi.Problem();
        }

        // Bekleyen davetler de koltuk sayılır: limit, davetler kabul edilince aşılmasın. Mali müşavir daveti limite sayılmaz.
        if (!rol.MaliMusavirMi
            && await KullaniciLimiti.AsimAsync(db, mevcutKullanici.FirmaId!.Value, 1, simdi, cancellationToken, haricEposta: eposta) is { } limitHatasi)
        {
            return limitHatasi.Problem();
        }

        // Aynı kişiye önceki bekleyen davetler geçersiz olur; geçerli olan yalnızca son gönderilen bağlantıdır.
        var oncekiler = await db.FirmaDavetleri
            .Where(d => d.Eposta == eposta && d.KabulTarihi == null && d.IptalTarihi == null)
            .ToListAsync(cancellationToken);
        oncekiler.ForEach(d => d.IptalEt(simdi));

        var (kod, ozet) = TekKullanimlikKod.Uret();
        var davet = FirmaDaveti.Olustur(eposta, rol.Id, ozet, simdi);
        db.FirmaDavetleri.Add(davet);
        await db.SaveChangesAsync(cancellationToken);
        await DavetGonderAsync(db, posta, mevcutKullanici, davet, kod, cancellationToken);

        return TypedResults.Created($"/api/yonetim/kullanicilar/davetler/{davet.Id}",
            new DavetOzeti(davet.Id, davet.Eposta, davet.RolId, rol.Ad, davet.SonGonderimTarihi, davet.BitisTarihi));
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

        var davet = await db.FirmaDavetleri.Include(d => d.Rol)
            .SingleOrDefaultAsync(d => d.Id == id && d.KabulTarihi == null && d.IptalTarihi == null, cancellationToken);
        if (davet is null)
        {
            return YonetimHatalari.DavetBulunamadi.Problem();
        }

        var rol = davet.Rol;
        if (davet.SonGonderimTarihi > simdi - KullaniciKodu.YenidenGondermeAraligi)
        {
            return YonetimHatalari.DavetCokSik.Problem();
        }

        var (kod, ozet) = TekKullanimlikKod.Uret();
        davet.YenidenGonder(ozet, simdi);
        await db.SaveChangesAsync(cancellationToken);
        await DavetGonderAsync(db, posta, mevcutKullanici, davet, kod, cancellationToken);
        return TypedResults.Ok(new DavetOzeti(davet.Id, davet.Eposta, davet.RolId, rol.Ad, davet.SonGonderimTarihi, davet.BitisTarihi));
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
        IYetkiSorgusu yetkiSorgusu,
        TimeProvider zaman,
        CancellationToken cancellationToken)
    {
        var uyelik = await db.FirmaKullanicilari.Include(u => u.Rol).SingleOrDefaultAsync(u => u.KullaniciId == kullaniciId, cancellationToken);
        if (uyelik is null)
        {
            return YonetimHatalari.KullaniciBulunamadi.Problem();
        }

        var sonuc = uyelik.Pasiflestir(mevcutKullanici.KullaniciId!.Value);
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        if (uyelik.Rol.TamYetkiliMi && await YoneticiKorumasiAsync(db, yetkiSorgusu, mevcutKullanici, uyelik, cancellationToken) is { } hata)
        {
            return hata.Problem();
        }

        await db.SaveChangesAsync(cancellationToken);
        await yetkiSorgusu.OnbellektenKaldirAsync(uyelik.FirmaId, cancellationToken);

        // Bu firmadaki açık oturumları kapat; yetki sorgusu pasif üyeye yetki vermediği için access token da hemen işlevsizleşir.
        var simdi = zaman.GetUtcNow().UtcDateTime;
        await db.RefreshTokenlar
            .Where(t => t.KullaniciId == kullaniciId && t.FirmaId == uyelik.FirmaId && t.IptalTarihi == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IptalTarihi, simdi), cancellationToken);

        return TypedResults.NoContent();
    }

    // Kişi kendi yetkisini değiştiremez; Yönetici rolünü yalnızca bir yönetici verir ya da alır ve firmada en az bir aktif yönetici kalır.
    private static async Task<IResult> YetkileriAtaAsync(
        Guid kullaniciId,
        KullaniciYetkiIstegi istek,
        IMevcutKullanici mevcutKullanici,
        YonetimDbContext db,
        IYetkiSorgusu yetkiSorgusu,
        YetkiKatalogu katalog,
        TimeProvider zaman,
        CancellationToken cancellationToken)
    {
        if (kullaniciId == mevcutKullanici.KullaniciId)
        {
            return YonetimHatalari.KendiYetkisiniDegistiremez.Problem();
        }

        var uyelik = await db.FirmaKullanicilari.Include(u => u.Rol).SingleOrDefaultAsync(u => u.KullaniciId == kullaniciId, cancellationToken);
        if (uyelik is null)
        {
            return YonetimHatalari.KullaniciBulunamadi.Problem();
        }

        var rol = await db.Roller.AsNoTracking().SingleOrDefaultAsync(r => r.Id == istek.RolId, cancellationToken);
        if (rol is null)
        {
            return YonetimHatalari.RolBulunamadi.Problem();
        }

        if (rol.TamYetkiliMi != uyelik.Rol.TamYetkiliMi
            && await YoneticiKorumasiAsync(db, yetkiSorgusu, mevcutKullanici, uyelik, cancellationToken) is { } hata)
        {
            return hata.Problem();
        }

        var ek = istek.EkYetkiler ?? [];
        var yasak = istek.YasakYetkiler ?? [];
        if (ek.Concat(yasak).FirstOrDefault(y => !katalog.Yetkiler.Contains(y)) is { } gecersiz)
        {
            return YonetimHatalari.YetkiGecersiz(gecersiz).Problem();
        }

        // Mali müşavir rolündeki kişiye de yalnız görüntüleme/aktarım yetkisi eklenebilir; normal role geçişte limite sayılır.
        if (rol.MaliMusavirMi && ek.FirstOrDefault(y => !FirmaRolu.MaliMusavirYetkisiMi(y)) is { } mmYasak)
        {
            return YonetimHatalari.MaliMusavirYetkisi(mmYasak).Problem();
        }

        if (uyelik.Rol.MaliMusavirMi && !rol.MaliMusavirMi
            && await KullaniciLimiti.AsimAsync(db, uyelik.FirmaId, 1, zaman.GetUtcNow().UtcDateTime, cancellationToken) is { } limitHatasi)
        {
            return limitHatasi.Problem();
        }

        // Yönetici tüm işyerlerine girer; kısıtlı listede yalnız firmanın işyerleri olabilir.
        var isyerleri = rol.TamYetkiliMi ? [] : (istek.Isyerleri ?? []).Distinct().ToList();
        if (isyerleri.Count > 0 && await db.Isyerleri.CountAsync(i => isyerleri.Contains(i.Id), cancellationToken) != isyerleri.Count)
        {
            return IsyeriHatalari.Bulunamadi.Problem();
        }

        if (istek.VarsayilanIsyeriId is { } varsayilan && !await db.Isyerleri.AnyAsync(i => i.Id == varsayilan, cancellationToken))
        {
            return IsyeriHatalari.Bulunamadi.Problem();
        }

        uyelik.YetkileriAta(rol.Id, rol.TamYetkiliMi ? [] : ek, rol.TamYetkiliMi ? [] : yasak);
        uyelik.IsyerleriniAta(isyerleri, istek.VarsayilanIsyeriId);
        await db.SaveChangesAsync(cancellationToken);
        await yetkiSorgusu.OnbellektenKaldirAsync(uyelik.FirmaId, cancellationToken);
        return TypedResults.NoContent();
    }

    // Yönetici üyeliğine dokunan işlem: işlemi yapan yönetici olmalı ve işlemden sonra başka bir aktif yönetici kalmalı.
    private static async Task<Hata?> YoneticiKorumasiAsync(
        YonetimDbContext db, IYetkiSorgusu yetkiSorgusu, IMevcutKullanici mevcutKullanici, FirmaKullanicisi hedef, CancellationToken cancellationToken)
    {
        if (!await TamYetkiliMiAsync(yetkiSorgusu, mevcutKullanici, cancellationToken))
        {
            return YonetimHatalari.YoneticiAtamaYetkisiYok;
        }

        if (hedef.Rol.TamYetkiliMi
            && !await db.FirmaKullanicilari.AnyAsync(u => u.Id != hedef.Id && u.AktifMi && u.Rol.TamYetkiliMi && u.Kullanici.AktifMi, cancellationToken))
        {
            return YonetimHatalari.SonYonetici;
        }

        return null;
    }

    private static async Task<bool> TamYetkiliMiAsync(IYetkiSorgusu yetkiSorgusu, IMevcutKullanici mevcutKullanici, CancellationToken cancellationToken) =>
        (await yetkiSorgusu.GetirAsync(mevcutKullanici.FirmaId!.Value, mevcutKullanici.KullaniciId!.Value, cancellationToken)).TamYetkiliMi;
}
