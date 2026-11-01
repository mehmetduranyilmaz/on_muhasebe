using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Arac.Contracts;
using OnMuhasebe.Modules.Arac.Domain;
using OnMuhasebe.Modules.Arac.Infrastructure;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Sayfalama;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Arac.Features;

// Aracın süreli yükümlülükleri, MTV, masraf ve hasar kayıtları. Her işlemde araç sorgu filtresinden (firma, işyeri, silinme) geçer.
internal static class AracKayitIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var g = grup.MapGroup("").Yetki(AracYetkileri.Islem);

        g.MapGet("/yaklasanlar", YaklasanlarAsync);
        g.MapGet("/araclar/{aracId:guid}/yukumlulukler", YukumluluklerAsync);
        g.MapPost("/araclar/{aracId:guid}/yukumlulukler", YukumlulukEkleAsync).Dogrula<YukumlulukKaydetIstegi>();
        g.MapPut("/yukumlulukler/{id:guid}", YukumlulukGuncelleAsync).Dogrula<YukumlulukKaydetIstegi>();
        g.MapPost("/yukumlulukler/{id:guid}/yenile", YukumlulukYenileAsync).Islem(YetkiIslemleri.Duzenle).Dogrula<YukumlulukKaydetIstegi>();
        g.MapDelete("/yukumlulukler/{id:guid}", YukumlulukSilAsync);

        g.MapGet("/araclar/{aracId:guid}/mtv", MtvListeleAsync);
        g.MapPost("/araclar/{aracId:guid}/mtv", MtvEkleAsync).Dogrula<MtvKaydetIstegi>();
        g.MapPost("/mtv/{id:guid}/ode", MtvOdeAsync).Islem(YetkiIslemleri.Duzenle).Dogrula<MtvOdemeIstegi>();
        g.MapPost("/mtv/{id:guid}/odeme-geri-al", MtvGeriAlAsync).Islem(YetkiIslemleri.Duzenle);
        g.MapDelete("/mtv/{id:guid}", MtvSilAsync);

        g.MapGet("/araclar/{aracId:guid}/masraflar", MasraflarAsync);
        g.MapPost("/araclar/{aracId:guid}/masraflar", MasrafEkleAsync).Dogrula<MasrafKaydetIstegi>();
        g.MapPut("/masraflar/{id:guid}", MasrafGuncelleAsync).Dogrula<MasrafKaydetIstegi>();
        g.MapDelete("/masraflar/{id:guid}", MasrafSilAsync);

        g.MapGet("/araclar/{aracId:guid}/hasarlar", HasarlarAsync);
        g.MapPost("/araclar/{aracId:guid}/hasarlar", HasarEkleAsync).Dogrula<HasarKaydetIstegi>();
        g.MapPut("/hasarlar/{id:guid}", HasarGuncelleAsync).Dogrula<HasarKaydetIstegi>();
        g.MapDelete("/hasarlar/{id:guid}", HasarSilAsync);
    }

    internal sealed class YukumlulukDogrulayici : AbstractValidator<YukumlulukKaydetIstegi>
    {
        public YukumlulukDogrulayici()
        {
            RuleFor(x => x.Tur).IsInEnum();
            RuleFor(x => x.Ad).MaximumLength(100);
            RuleFor(x => x.Ad).NotEmpty().When(x => x.Tur == YukumlulukTuru.Diger).WithMessage("Diğer türünde ad gerekli.");
            RuleFor(x => x.BelgeNo).MaximumLength(100);
            RuleFor(x => x.Sirket).MaximumLength(200);
            RuleFor(x => x.Tutar).GreaterThanOrEqualTo(0);
            RuleFor(x => x.HatirlatmaGun).InclusiveBetween(1, 365).WithMessage("Hatırlatma günü 1 ile 365 arasında olmalıdır.");
        }
    }

    internal sealed class MtvDogrulayici : AbstractValidator<MtvKaydetIstegi>
    {
        public MtvDogrulayici()
        {
            RuleFor(x => x.Yil).InclusiveBetween(2000, 2100);
            RuleFor(x => x.Taksit).InclusiveBetween(1, 2).WithMessage("Taksit 1 (Ocak) ya da 2 (Temmuz) olmalıdır.");
            RuleFor(x => x.Tutar).GreaterThan(0);
        }
    }

    internal sealed class MtvOdemeDogrulayici : AbstractValidator<MtvOdemeIstegi>
    {
        public MtvOdemeDogrulayici() => RuleFor(x => x.OdemeTarihi).NotEmpty();
    }

    internal sealed class MasrafDogrulayici : AbstractValidator<MasrafKaydetIstegi>
    {
        public MasrafDogrulayici()
        {
            RuleFor(x => x.Tur).IsInEnum();
            RuleFor(x => x.Tutar).GreaterThan(0);
            RuleFor(x => x.Km).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Litre).GreaterThan(0);
            RuleFor(x => x.Tedarikci).MaximumLength(200);
            RuleFor(x => x.BelgeNo).MaximumLength(100);
            RuleFor(x => x.Aciklama).MaximumLength(500);
        }
    }

    internal sealed class HasarDogrulayici : AbstractValidator<HasarKaydetIstegi>
    {
        public HasarDogrulayici()
        {
            RuleFor(x => x.Kusur).IsInEnum();
            RuleFor(x => x.OnarimTutari).GreaterThanOrEqualTo(0);
            RuleFor(x => x.SigortaOdemesi).GreaterThanOrEqualTo(0);
            RuleFor(x => x.DosyaNo).MaximumLength(100);
            RuleFor(x => x.Aciklama).MaximumLength(1000);
        }
    }

    private static DateOnly Bugun(TimeProvider zaman) => DateOnly.FromDateTime(zaman.GetLocalNow().DateTime);

    private static Task<bool> AracVarAsync(AracDbContext db, Guid aracId, CancellationToken ct) => db.Araclar.AnyAsync(a => a.Id == aracId, ct);

    // Yenilenmemiş kayıtlardan, aktif araçlarda süresi dolan ve hatırlatma süresine girenler.
    private static async Task<IResult> YaklasanlarAsync(AracDbContext db, TimeProvider zaman, CancellationToken ct)
    {
        var bugun = Bugun(zaman);
        var kayitlar = await (
                from y in db.Yukumlulukler.AsNoTracking()
                join a in db.Araclar on y.AracId equals a.Id
                where a.Durum == AracDurumu.Aktif && !y.YenilendiMi && y.BitisTarihi <= bugun.AddDays(y.HatirlatmaGun)
                orderby y.BitisTarihi
                select new { y.Id, y.AracId, a.Plaka, y.Tur, y.Ad, y.BitisTarihi, y.HatirlatmaGun })
            .Take(100).ToListAsync(ct);
        return TypedResults.Ok(kayitlar.Select(k => new YaklasanYukumluluk(k.Id, k.AracId, k.Plaka, k.Tur, k.Ad, k.BitisTarihi,
            k.BitisTarihi.DayNumber - bugun.DayNumber, AracHesaplari.Durum(k.BitisTarihi, k.HatirlatmaGun, bugun))).ToList());
    }

    private static async Task<IResult> YukumluluklerAsync(Guid aracId, AracDbContext db, TimeProvider zaman, CancellationToken ct)
    {
        if (!await AracVarAsync(db, aracId, ct))
        {
            return AracHatalari.Bulunamadi.Problem();
        }

        var bugun = Bugun(zaman);
        var kayitlar = await db.Yukumlulukler.AsNoTracking().Where(y => y.AracId == aracId)
            .OrderBy(y => y.YenilendiMi).ThenBy(y => y.BitisTarihi).ToListAsync(ct);
        return TypedResults.Ok(kayitlar.Select(y => Cevap(y, bugun)).ToList());
    }

    private static async Task<IResult> YukumlulukEkleAsync(Guid aracId, YukumlulukKaydetIstegi istek, AracDbContext db, TimeProvider zaman, CancellationToken ct)
    {
        if (!await AracVarAsync(db, aracId, ct))
        {
            return AracHatalari.Bulunamadi.Problem();
        }

        var sonuc = AracYukumlulugu.Olustur(aracId, istek);
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        db.Yukumlulukler.Add(sonuc.Deger);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/arac/yukumlulukler/{sonuc.Deger.Id}", Cevap(sonuc.Deger, Bugun(zaman)));
    }

    private static async Task<IResult> YukumlulukGuncelleAsync(Guid id, YukumlulukKaydetIstegi istek, AracDbContext db, TimeProvider zaman, CancellationToken ct)
    {
        if (await BulAsync(db.Yukumlulukler, db, y => y.Id == id, y => y.AracId, ct) is not { } kayit)
        {
            return AracHatalari.KayitBulunamadi.Problem();
        }

        var sonuc = kayit.Guncelle(istek);
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(Cevap(kayit, Bugun(zaman)));
    }

    private static async Task<IResult> YukumlulukYenileAsync(Guid id, YukumlulukKaydetIstegi istek, AracDbContext db, TimeProvider zaman, CancellationToken ct)
    {
        if (await BulAsync(db.Yukumlulukler, db, y => y.Id == id, y => y.AracId, ct) is not { } kayit)
        {
            return AracHatalari.KayitBulunamadi.Problem();
        }

        var yeni = kayit.Yenile(istek);
        if (!yeni.BasariliMi)
        {
            return yeni.HttpSonucu();
        }

        db.Yukumlulukler.Add(yeni.Deger);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/arac/yukumlulukler/{yeni.Deger.Id}", Cevap(yeni.Deger, Bugun(zaman)));
    }

    private static async Task<IResult> YukumlulukSilAsync(Guid id, AracDbContext db, CancellationToken ct) =>
        await SilAsync(db.Yukumlulukler, db, y => y.Id == id, y => y.AracId, ct);

    private static async Task<IResult> MtvListeleAsync(Guid aracId, AracDbContext db, CancellationToken ct)
    {
        if (!await AracVarAsync(db, aracId, ct))
        {
            return AracHatalari.Bulunamadi.Problem();
        }

        return TypedResults.Ok(await db.MtvKayitlari.AsNoTracking().Where(m => m.AracId == aracId)
            .OrderByDescending(m => m.Yil).ThenByDescending(m => m.Taksit)
            .Select(m => new MtvCevabi(m.Id, m.AracId, m.Yil, m.Taksit, m.Tutar, m.SonOdemeTarihi, m.OdemeTarihi)).ToListAsync(ct));
    }

    private static async Task<IResult> MtvEkleAsync(Guid aracId, MtvKaydetIstegi istek, AracDbContext db, CancellationToken ct)
    {
        if (!await AracVarAsync(db, aracId, ct))
        {
            return AracHatalari.Bulunamadi.Problem();
        }

        if (await db.MtvKayitlari.AnyAsync(m => m.AracId == aracId && m.Yil == istek.Yil && m.Taksit == istek.Taksit, ct))
        {
            return AracHatalari.MtvKayitli.Problem();
        }

        var mtv = AracMtv.Olustur(aracId, istek);
        db.MtvKayitlari.Add(mtv);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/arac/mtv/{mtv.Id}", Cevap(mtv));
    }

    private static async Task<IResult> MtvOdeAsync(Guid id, MtvOdemeIstegi istek, AracDbContext db, CancellationToken ct) =>
        await MtvDegistirAsync(id, db, istek.OdemeTarihi, ct);

    private static async Task<IResult> MtvGeriAlAsync(Guid id, AracDbContext db, CancellationToken ct) =>
        await MtvDegistirAsync(id, db, null, ct);

    private static async Task<IResult> MtvDegistirAsync(Guid id, AracDbContext db, DateOnly? tarih, CancellationToken ct)
    {
        if (await BulAsync(db.MtvKayitlari, db, m => m.Id == id, m => m.AracId, ct) is not { } mtv)
        {
            return AracHatalari.KayitBulunamadi.Problem();
        }

        mtv.Ode(tarih);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(Cevap(mtv));
    }

    private static async Task<IResult> MtvSilAsync(Guid id, AracDbContext db, CancellationToken ct) =>
        await SilAsync(db.MtvKayitlari, db, m => m.Id == id, m => m.AracId, ct);

    private static async Task<IResult> MasraflarAsync(Guid aracId, AracDbContext db, int? sayfa, int? boyut, CancellationToken ct)
    {
        if (!await AracVarAsync(db, aracId, ct))
        {
            return AracHatalari.Bulunamadi.Problem();
        }

        return TypedResults.Ok(await db.Masraflar.AsNoTracking().Where(m => m.AracId == aracId)
            .OrderByDescending(m => m.Tarih).ThenByDescending(m => m.Id)
            .Select(m => new MasrafCevabi(m.Id, m.AracId, m.Tur, m.Tarih, m.Tutar, m.Km, m.Litre, m.Tedarikci, m.BelgeNo, m.FaturaId, m.Aciklama))
            .SayfalaAsync(new SayfalamaIstegi(sayfa ?? 1, boyut ?? SayfalamaIstegi.VarsayilanBoyut), ct));
    }

    private static async Task<IResult> MasrafEkleAsync(
        Guid aracId, MasrafKaydetIstegi istek, AracDbContext db, IFaturaMuhasebeSorgusu fatura, CancellationToken ct)
    {
        var arac = await db.Araclar.SingleOrDefaultAsync(a => a.Id == aracId, ct);
        if (arac is null)
        {
            return AracHatalari.Bulunamadi.Problem();
        }

        if (istek.FaturaId is { } faturaId && !await fatura.VarMiAsync(faturaId, ct))
        {
            return AracHatalari.FaturaGecersiz.Problem();
        }

        var masraf = AracMasrafi.Olustur(aracId, istek);
        db.Masraflar.Add(masraf);
        arac.KmIlerlet(istek.Km);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/arac/masraflar/{masraf.Id}", Cevap(masraf));
    }

    private static async Task<IResult> MasrafGuncelleAsync(
        Guid id, MasrafKaydetIstegi istek, AracDbContext db, IFaturaMuhasebeSorgusu fatura, CancellationToken ct)
    {
        if (await BulAsync(db.Masraflar, db, m => m.Id == id, m => m.AracId, ct) is not { } masraf)
        {
            return AracHatalari.KayitBulunamadi.Problem();
        }

        if (istek.FaturaId is { } faturaId && faturaId != masraf.FaturaId && !await fatura.VarMiAsync(faturaId, ct))
        {
            return AracHatalari.FaturaGecersiz.Problem();
        }

        masraf.Guncelle(istek);
        (await db.Araclar.SingleAsync(a => a.Id == masraf.AracId, ct)).KmIlerlet(istek.Km);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(Cevap(masraf));
    }

    private static async Task<IResult> MasrafSilAsync(Guid id, AracDbContext db, CancellationToken ct) =>
        await SilAsync(db.Masraflar, db, m => m.Id == id, m => m.AracId, ct);

    private static async Task<IResult> HasarlarAsync(Guid aracId, AracDbContext db, CancellationToken ct)
    {
        if (!await AracVarAsync(db, aracId, ct))
        {
            return AracHatalari.Bulunamadi.Problem();
        }

        return TypedResults.Ok(await db.Hasarlar.AsNoTracking().Where(h => h.AracId == aracId).OrderByDescending(h => h.Tarih)
            .Select(h => new HasarCevabi(h.Id, h.AracId, h.Tarih, h.Kusur, h.Aciklama, h.OnarimTutari, h.SigortaOdemesi, h.DosyaNo)).ToListAsync(ct));
    }

    private static async Task<IResult> HasarEkleAsync(Guid aracId, HasarKaydetIstegi istek, AracDbContext db, CancellationToken ct)
    {
        if (!await AracVarAsync(db, aracId, ct))
        {
            return AracHatalari.Bulunamadi.Problem();
        }

        var hasar = AracHasari.Olustur(aracId, istek);
        db.Hasarlar.Add(hasar);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/arac/hasarlar/{hasar.Id}", Cevap(hasar));
    }

    private static async Task<IResult> HasarGuncelleAsync(Guid id, HasarKaydetIstegi istek, AracDbContext db, CancellationToken ct)
    {
        if (await BulAsync(db.Hasarlar, db, h => h.Id == id, h => h.AracId, ct) is not { } hasar)
        {
            return AracHatalari.KayitBulunamadi.Problem();
        }

        hasar.Guncelle(istek);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(Cevap(hasar));
    }

    private static async Task<IResult> HasarSilAsync(Guid id, AracDbContext db, CancellationToken ct) =>
        await SilAsync(db.Hasarlar, db, h => h.Id == id, h => h.AracId, ct);

    // Alt kayıtların işyeri bilgisi yok: aracı görülemeyen kayıt bulunamadı sayılır.
    private static async Task<T?> BulAsync<T>(
        DbSet<T> kume, AracDbContext db, System.Linq.Expressions.Expression<Func<T, bool>> kosul, Func<T, Guid> aracId, CancellationToken ct)
        where T : class
    {
        var kayit = await kume.SingleOrDefaultAsync(kosul, ct);
        return kayit is not null && await AracVarAsync(db, aracId(kayit), ct) ? kayit : null;
    }

    private static async Task<IResult> SilAsync<T>(
        DbSet<T> kume, AracDbContext db, System.Linq.Expressions.Expression<Func<T, bool>> kosul, Func<T, Guid> aracId, CancellationToken ct)
        where T : class
    {
        if (await BulAsync(kume, db, kosul, aracId, ct) is not { } kayit)
        {
            return AracHatalari.KayitBulunamadi.Problem();
        }

        kume.Remove(kayit);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static YukumlulukCevabi Cevap(AracYukumlulugu y, DateOnly bugun) => new(
        y.Id, y.AracId, y.Tur, y.Ad, y.BaslangicTarihi, y.BitisTarihi, y.BelgeNo, y.Sirket, y.Tutar, y.HatirlatmaGun, y.YenilendiMi,
        y.BitisTarihi.DayNumber - bugun.DayNumber, AracHesaplari.Durum(y.BitisTarihi, y.HatirlatmaGun, bugun));

    private static MtvCevabi Cevap(AracMtv m) => new(m.Id, m.AracId, m.Yil, m.Taksit, m.Tutar, m.SonOdemeTarihi, m.OdemeTarihi);

    private static MasrafCevabi Cevap(AracMasrafi m) =>
        new(m.Id, m.AracId, m.Tur, m.Tarih, m.Tutar, m.Km, m.Litre, m.Tedarikci, m.BelgeNo, m.FaturaId, m.Aciklama);

    private static HasarCevabi Cevap(AracHasari h) => new(h.Id, h.AracId, h.Tarih, h.Kusur, h.Aciklama, h.OnarimTutari, h.SigortaOdemesi, h.DosyaNo);
}
