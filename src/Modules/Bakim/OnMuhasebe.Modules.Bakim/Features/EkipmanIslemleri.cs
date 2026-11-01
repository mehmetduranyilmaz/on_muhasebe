using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Bakim.Contracts;
using OnMuhasebe.Modules.Bakim.Domain;
using OnMuhasebe.Modules.Bakim.Infrastructure;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.Yonetim.Contracts;
using OnMuhasebe.Shared.Contracts.Sayfalama;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Bakim.Features;

// Ekipman kartları ve periyodik bakım planları.
internal static class EkipmanIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var g = grup.MapGroup("").Yetki(BakimYetkileri.Ekipman);
        g.MapGet("/ekipmanlar", ListeleAsync);
        g.MapGet("/ekipmanlar/{id:guid}", DetayAsync);
        g.MapPost("/ekipmanlar", EkleAsync).Dogrula<EkipmanKaydetIstegi>();
        g.MapPut("/ekipmanlar/{id:guid}", GuncelleAsync).Dogrula<EkipmanKaydetIstegi>();
        g.MapDelete("/ekipmanlar/{id:guid}", SilAsync);
        g.MapGet("/sorumlular", SorumlularAsync);
        g.MapGet("/yaklasanlar", YaklasanlarAsync);
        g.MapGet("/ekipmanlar/{ekipmanId:guid}/planlar", PlanlarAsync);
        g.MapPost("/ekipmanlar/{ekipmanId:guid}/planlar", PlanEkleAsync).Dogrula<BakimPlaniKaydetIstegi>();
        g.MapPut("/planlar/{id:guid}", PlanGuncelleAsync).Dogrula<BakimPlaniKaydetIstegi>();
        g.MapDelete("/planlar/{id:guid}", PlanSilAsync);
    }

    internal sealed class Dogrulayici : AbstractValidator<EkipmanKaydetIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.Kod).MaximumLength(20).Matches("^[A-Za-z0-9._-]*$").WithMessage("Ekipman kodu yalnızca harf, rakam, nokta, tire ve alt çizgi içerebilir.");
            RuleFor(x => x.Ad).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Tur).IsInEnum();
            RuleFor(x => x.Marka).MaximumLength(100);
            RuleFor(x => x.Model).MaximumLength(100);
            RuleFor(x => x.SeriNo).MaximumLength(100);
            RuleFor(x => x.UretimYili).InclusiveBetween(1900, 2100).When(x => x.UretimYili is not null);
            RuleFor(x => x.Konum).MaximumLength(200);
            RuleFor(x => x.SayacBirimi).IsInEnum();
            RuleFor(x => x.Sayac).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Durum).IsInEnum();
            RuleFor(x => x.Aciklama).MaximumLength(1000);
        }
    }

    internal sealed class PlanDogrulayici : AbstractValidator<BakimPlaniKaydetIstegi>
    {
        public PlanDogrulayici()
        {
            RuleFor(x => x.Ad).NotEmpty().MaximumLength(200);
            RuleFor(x => x.KontrolKurulusu).MaximumLength(200);
            RuleFor(x => x.PeriyotGun).InclusiveBetween(1, 3650).When(x => x.PeriyotGun is not null);
            RuleFor(x => x.PeriyotSayac).GreaterThan(0).When(x => x.PeriyotSayac is not null);
            RuleFor(x => x.HatirlatmaGun).InclusiveBetween(1, 365).WithMessage("Hatırlatma günü 1 ile 365 arasında olmalıdır.");
        }
    }

    private static DateOnly Bugun(TimeProvider zaman) => TurkiyeSaati.Bugun(zaman);

    private static async Task<IResult> ListeleAsync(
        BakimDbContext db, IFirmaRehberi rehber, IMevcutKullanici kullanici, TimeProvider zaman, string? arama, EkipmanDurumu? durum, Guid? isyeriId, int? sayfa, int? boyut, CancellationToken ct)
    {
        var bugun = Bugun(zaman);
        var sorgu = db.Ekipmanlar.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(arama))
        {
            var desen = AramaDeseni.Icerir(arama);
            sorgu = sorgu.Where(e => EF.Functions.ILike(e.Ad, desen) || EF.Functions.ILike(e.Kod, desen) || EF.Functions.ILike(e.SeriNo ?? "", desen) || EF.Functions.ILike(e.Konum ?? "", desen));
        }

        if (durum is not null)
        {
            sorgu = sorgu.Where(e => e.Durum == durum);
        }

        if (isyeriId is not null)
        {
            sorgu = sorgu.Where(e => e.IsyeriId == isyeriId);
        }

        var sonuc = await sorgu.OrderBy(e => e.Kod)
            .Select(e => new { e.Id, e.Kod, e.Ad, e.Tur, e.Konum, e.Durum, e.Sayac, e.SayacBirimi, e.SorumluKullaniciId, e.IsyeriId })
            .SayfalaAsync(new SayfalamaIstegi(sayfa ?? 1, boyut ?? SayfalamaIstegi.VarsayilanBoyut), ct);
        var idler = sonuc.Kayitlar.Select(e => e.Id).ToList();
        var planlar = await db.Planlar.AsNoTracking().Where(p => p.AktifMi && idler.Contains(p.EkipmanId))
            .Select(p => new { p.EkipmanId, p.Ad, p.SonrakiTarih, p.HatirlatmaGun, p.SonrakiSayac, p.PeriyotSayac }).ToListAsync(ct);
        var adlar = (await rehber.KullanicilarAsync(kullanici.FirmaId!.Value, ct)).ToDictionary(k => k.KullaniciId, k => k.AdSoyad);

        return TypedResults.Ok(new SayfaliSonuc<EkipmanOzeti>(sonuc.Kayitlar.Select(e =>
        {
            var enYakin = planlar.Where(p => p.EkipmanId == e.Id)
                .Select(p => new { p.Ad, p.SonrakiTarih, Durum = BakimHesaplari.Durum(p.SonrakiTarih, p.HatirlatmaGun, p.SonrakiSayac, p.PeriyotSayac, e.Sayac, bugun) })
                .OrderByDescending(p => p.Durum).ThenBy(p => p.SonrakiTarih ?? DateOnly.MaxValue).FirstOrDefault();
            return new EkipmanOzeti(e.Id, e.Kod, e.Ad, e.Tur, e.Konum, e.Durum, e.Sayac, e.SayacBirimi,
                e.SorumluKullaniciId is { } s ? adlar.GetValueOrDefault(s) : null, e.IsyeriId, enYakin?.Ad, enYakin?.SonrakiTarih, enYakin?.Durum);
        }).ToList(), sonuc.Sayfa, sonuc.Boyut, sonuc.ToplamKayit));
    }

    private static async Task<IResult> DetayAsync(Guid id, BakimDbContext db, CancellationToken ct)
    {
        var ekipman = await db.Ekipmanlar.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, ct);
        return ekipman is null ? BakimHatalari.EkipmanYok.Problem() : TypedResults.Ok(Detay(ekipman));
    }

    private static async Task<IResult> EkleAsync(
        EkipmanKaydetIstegi istek, BakimDbContext db, IMevcutKullanici kullanici, IFirmaRehberi rehber, IsyeriSecici isyeriSecici, IKasaBankaSorgusu kasa, CancellationToken ct)
    {
        if (await BagHatasiAsync(istek, null, kullanici, rehber, isyeriSecici, kasa, ct) is { } hata)
        {
            return hata.Problem();
        }

        var kod = string.IsNullOrWhiteSpace(istek.Kod) ? await KodUretici.SonrakiAsync(db, "bakim.ekipman", "E", kullanici.FirmaId!.Value, ct) : istek.Kod.Trim();
        if (await db.Ekipmanlar.IgnoreQueryFilters([ModulDbContext.SilinmeFiltresi, ModulDbContext.IsyeriFiltresi]).AnyAsync(e => e.Kod == kod, ct))
        {
            return BakimHatalari.KodKullaniliyor.Problem();
        }

        var ekipman = Ekipman.Olustur(kod, istek);
        db.Ekipmanlar.Add(ekipman);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/bakim/ekipmanlar/{ekipman.Id}", Detay(ekipman));
    }

    private static async Task<IResult> GuncelleAsync(
        Guid id, EkipmanKaydetIstegi istek, BakimDbContext db, IMevcutKullanici kullanici, IFirmaRehberi rehber, IsyeriSecici isyeriSecici, IKasaBankaSorgusu kasa, CancellationToken ct)
    {
        var ekipman = await db.Ekipmanlar.SingleOrDefaultAsync(e => e.Id == id, ct);
        if (ekipman is null)
        {
            return BakimHatalari.EkipmanYok.Problem();
        }

        if (await BagHatasiAsync(istek, ekipman, kullanici, rehber, isyeriSecici, kasa, ct) is { } hata)
        {
            return hata.Problem();
        }

        var kod = string.IsNullOrWhiteSpace(istek.Kod) ? ekipman.Kod : istek.Kod.Trim();
        if (kod != ekipman.Kod && await db.Ekipmanlar.IgnoreQueryFilters([ModulDbContext.SilinmeFiltresi, ModulDbContext.IsyeriFiltresi]).AnyAsync(e => e.Kod == kod && e.Id != id, ct))
        {
            return BakimHatalari.KodKullaniliyor.Problem();
        }

        db.Entry(ekipman).Property(e => e.Surum).OriginalValue = istek.Surum;
        ekipman.Guncelle(kod, istek);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(Detay(ekipman));
    }

    private static async Task<IResult> SilAsync(Guid id, BakimDbContext db, CancellationToken ct)
    {
        var ekipman = await db.Ekipmanlar.SingleOrDefaultAsync(e => e.Id == id, ct);
        if (ekipman is null)
        {
            return BakimHatalari.EkipmanYok.Problem();
        }

        db.Ekipmanlar.Remove(ekipman);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> SorumlularAsync(IFirmaRehberi rehber, IMevcutKullanici kullanici, CancellationToken ct) =>
        TypedResults.Ok((await rehber.KullanicilarAsync(kullanici.FirmaId!.Value, ct))
            .OrderBy(k => k.AdSoyad).Select(k => new BakimSorumlusu(k.KullaniciId, k.AdSoyad)).ToList());

    private static async Task<IResult> YaklasanlarAsync(BakimDbContext db, TimeProvider zaman, CancellationToken ct)
    {
        var bugun = Bugun(zaman);
        var kayitlar = await (
                from p in db.Planlar.AsNoTracking()
                join e in db.Ekipmanlar on p.EkipmanId equals e.Id
                where p.AktifMi && e.Durum != EkipmanDurumu.HizmetDisi
                select new { p.Id, p.EkipmanId, e.Kod, EkipmanAdi = e.Ad, e.Sayac, p.Ad, p.YasalMi, p.SonrakiTarih, p.SonrakiSayac, p.PeriyotSayac, p.HatirlatmaGun })
            .ToListAsync(ct);
        return TypedResults.Ok(kayitlar
            .Select(k => new YaklasanBakim(k.Id, k.EkipmanId, k.Kod, k.EkipmanAdi, k.Ad, k.YasalMi, k.SonrakiTarih, k.SonrakiSayac, k.Sayac,
                BakimHesaplari.Durum(k.SonrakiTarih, k.HatirlatmaGun, k.SonrakiSayac, k.PeriyotSayac, k.Sayac, bugun)))
            .Where(y => y.Durum != PlanDurumu.Zamaninda)
            .OrderByDescending(y => y.Durum).ThenBy(y => y.SonrakiTarih ?? DateOnly.MaxValue).Take(100).ToList());
    }

    private static async Task<IResult> PlanlarAsync(Guid ekipmanId, BakimDbContext db, TimeProvider zaman, CancellationToken ct)
    {
        var ekipman = await db.Ekipmanlar.AsNoTracking().SingleOrDefaultAsync(e => e.Id == ekipmanId, ct);
        if (ekipman is null)
        {
            return BakimHatalari.EkipmanYok.Problem();
        }

        var planlar = await db.Planlar.AsNoTracking().Where(p => p.EkipmanId == ekipmanId).OrderByDescending(p => p.AktifMi).ThenBy(p => p.SonrakiTarih).ToListAsync(ct);
        return TypedResults.Ok(planlar.Select(p => Cevap(p, ekipman.Sayac, Bugun(zaman))).ToList());
    }

    private static async Task<IResult> PlanEkleAsync(Guid ekipmanId, BakimPlaniKaydetIstegi istek, BakimDbContext db, TimeProvider zaman, CancellationToken ct)
    {
        var ekipman = await db.Ekipmanlar.AsNoTracking().SingleOrDefaultAsync(e => e.Id == ekipmanId, ct);
        if (ekipman is null)
        {
            return BakimHatalari.EkipmanYok.Problem();
        }

        if (istek.PeriyotSayac is not null && ekipman.SayacBirimi == SayacBirimi.Yok)
        {
            return BakimHatalari.SayacBirimiYok.Problem();
        }

        var sonuc = BakimPlani.Olustur(ekipmanId, istek, Bugun(zaman), ekipman.Sayac);
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        db.Planlar.Add(sonuc.Deger);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/bakim/planlar/{sonuc.Deger.Id}", Cevap(sonuc.Deger, ekipman.Sayac, Bugun(zaman)));
    }

    private static async Task<IResult> PlanGuncelleAsync(Guid id, BakimPlaniKaydetIstegi istek, BakimDbContext db, TimeProvider zaman, CancellationToken ct)
    {
        var plan = await db.Planlar.SingleOrDefaultAsync(p => p.Id == id, ct);
        var ekipman = plan is null ? null : await db.Ekipmanlar.AsNoTracking().SingleOrDefaultAsync(e => e.Id == plan.EkipmanId, ct);
        if (plan is null || ekipman is null)
        {
            return BakimHatalari.PlanYok.Problem();
        }

        if (istek.PeriyotSayac is not null && ekipman.SayacBirimi == SayacBirimi.Yok)
        {
            return BakimHatalari.SayacBirimiYok.Problem();
        }

        var sonuc = plan.Guncelle(istek, Bugun(zaman), ekipman.Sayac);
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(Cevap(plan, ekipman.Sayac, Bugun(zaman)));
    }

    private static async Task<IResult> PlanSilAsync(Guid id, BakimDbContext db, CancellationToken ct)
    {
        var plan = await db.Planlar.SingleOrDefaultAsync(p => p.Id == id && db.Ekipmanlar.Any(e => e.Id == p.EkipmanId), ct);
        if (plan is null)
        {
            return BakimHatalari.PlanYok.Problem();
        }

        db.Planlar.Remove(plan);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<Hata?> BagHatasiAsync(
        EkipmanKaydetIstegi istek, Ekipman? mevcut, IMevcutKullanici kullanici, IFirmaRehberi rehber, IsyeriSecici isyeriSecici, IKasaBankaSorgusu kasa, CancellationToken ct)
    {
        if (istek.SorumluKullaniciId is { } sorumlu && sorumlu != mevcut?.SorumluKullaniciId
            && (await rehber.KullanicilarAsync(kullanici.FirmaId!.Value, ct)).All(k => k.KullaniciId != sorumlu))
        {
            return BakimHatalari.SorumluGecersiz;
        }

        if (istek.IsyeriId is { } isyeri && isyeri != mevcut?.IsyeriId)
        {
            var secim = await isyeriSecici.BelirleAsync(isyeri, ct);
            if (!secim.BasariliMi)
            {
                return secim.Hata;
            }
        }

        if (istek.MasrafMerkeziId is { } merkez && merkez != mevcut?.MasrafMerkeziId && !await kasa.MasrafMerkeziAktifMiAsync(merkez, ct))
        {
            return BakimHatalari.MasrafMerkeziGecersiz;
        }

        if (istek.DemirbasId is { } demirbas && demirbas != mevcut?.DemirbasId && !await kasa.DemirbasVarMiAsync(demirbas, ct))
        {
            return BakimHatalari.DemirbasGecersiz;
        }

        return null;
    }

    private static EkipmanDetayi Detay(Ekipman e) => new(
        e.Id, e.Kod, e.Ad, e.Tur, e.Marka, e.Model, e.SeriNo, e.UretimYili, e.Konum, e.IsyeriId, e.SorumluKullaniciId, e.DemirbasId, e.MasrafMerkeziId, e.SayacBirimi, e.Sayac, e.Durum,
        e.Aciklama, e.Surum);

    private static BakimPlaniCevabi Cevap(BakimPlani p, decimal? sayac, DateOnly bugun) => new(
        p.Id, p.EkipmanId, p.Ad, p.YasalMi, p.KontrolKurulusu, p.PeriyotGun, p.PeriyotSayac, p.SonYapilmaTarihi, p.SonYapilmaSayaci, p.SonrakiTarih, p.SonrakiSayac, p.HatirlatmaGun, p.AktifMi,
        BakimHesaplari.Durum(p.SonrakiTarih, p.HatirlatmaGun, p.SonrakiSayac, p.PeriyotSayac, sayac, bugun));
}
