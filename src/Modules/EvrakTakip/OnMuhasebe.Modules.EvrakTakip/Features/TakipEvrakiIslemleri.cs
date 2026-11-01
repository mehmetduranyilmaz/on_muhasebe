using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.EvrakTakip.Contracts;
using OnMuhasebe.Modules.EvrakTakip.Domain;
using OnMuhasebe.Modules.EvrakTakip.Infrastructure;
using OnMuhasebe.Modules.Yonetim.Contracts;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Sayfalama;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.EvrakTakip.Features;

internal static class TakipEvrakiIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var evraklar = grup.MapGroup("").Yetki(EvrakTakipYetkileri.Kayit);
        evraklar.MapGet("", ListeleAsync);
        evraklar.MapGet("/sayilar", SayilarAsync);
        evraklar.MapGet("/sorumlular", SorumlularAsync);
        evraklar.MapGet("/{id:guid}", DetayAsync);
        evraklar.MapPost("", EkleAsync).Dogrula<TakipEvrakiKaydetIstegi>();
        evraklar.MapPut("/{id:guid}", GuncelleAsync).Dogrula<TakipEvrakiKaydetIstegi>();
        evraklar.MapDelete("/{id:guid}", SilAsync);
        evraklar.MapPost("/{id:guid}/yenile", YenileAsync).Islem(YetkiIslemleri.Duzenle).Dogrula<TakipYenilemeIstegi>();
        evraklar.MapPost("/{id:guid}/pasiflestir", (Guid id, EvrakTakipDbContext db, TimeProvider zaman, CancellationToken ct) =>
            DurumDegistirAsync(id, db, e => e.Pasiflestir(), zaman, ct)).Islem(YetkiIslemleri.Duzenle);
        evraklar.MapPost("/{id:guid}/aktiflestir", (Guid id, EvrakTakipDbContext db, TimeProvider zaman, CancellationToken ct) =>
            DurumDegistirAsync(id, db, e => e.Aktiflestir(), zaman, ct)).Islem(YetkiIslemleri.Duzenle);
    }

    internal sealed class Dogrulayici : AbstractValidator<TakipEvrakiKaydetIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.Ad).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Tur).IsInEnum();
            RuleFor(x => x.BelgeNo).MaximumLength(100);
            RuleFor(x => x.Kurum).MaximumLength(200);
            RuleFor(x => x.HatirlatmaGun).InclusiveBetween(1, 365).WithMessage("Hatırlatma günü 1 ile 365 arasında olmalıdır.");
            RuleFor(x => x.Aciklama).MaximumLength(1000);
        }
    }

    internal sealed class YenilemeDogrulayici : AbstractValidator<TakipYenilemeIstegi>
    {
        public YenilemeDogrulayici()
        {
            RuleFor(x => x.YeniBelgeNo).MaximumLength(100);
            RuleFor(x => x.Not).MaximumLength(500);
        }
    }

    private static DateOnly Bugun(TimeProvider zaman) => TurkiyeSaati.Bugun(zaman);

    private static async Task<IResult> ListeleAsync(
        EvrakTakipDbContext db, IFirmaRehberi rehber, IMevcutKullanici kullanici, TimeProvider zaman, string? arama, TakipEvrakDurumu? durum, TakipEvrakTuru? tur,
        Guid? isyeriId, int? sayfa, int? boyut, CancellationToken ct)
    {
        var bugun = Bugun(zaman);
        var sorgu = db.TakipEvraklari.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(arama))
        {
            var desen = AramaDeseni.Icerir(arama);
            sorgu = sorgu.Where(e => EF.Functions.ILike(e.Ad, desen) || EF.Functions.ILike(e.BelgeNo ?? "", desen) || EF.Functions.ILike(e.Kurum ?? "", desen));
        }

        if (tur is not null)
        {
            sorgu = sorgu.Where(e => e.Tur == tur);
        }

        if (isyeriId is not null)
        {
            sorgu = sorgu.Where(e => e.IsyeriId == isyeriId);
        }

        sorgu = durum switch
        {
            TakipEvrakDurumu.Pasif => sorgu.Where(e => !e.AktifMi),
            TakipEvrakDurumu.SuresiDoldu => sorgu.Where(e => e.AktifMi && e.BitisTarihi < bugun),
            TakipEvrakDurumu.Yaklasiyor => sorgu.Where(e => e.AktifMi && e.BitisTarihi >= bugun && e.BitisTarihi <= bugun.AddDays(e.HatirlatmaGun)),
            TakipEvrakDurumu.Gecerli => sorgu.Where(e => e.AktifMi && e.BitisTarihi > bugun.AddDays(e.HatirlatmaGun)),
            _ => sorgu.Where(e => e.AktifMi)
        };

        var sonuc = await sorgu.OrderBy(e => e.BitisTarihi).ThenBy(e => e.Ad)
            .Select(e => new { e.Id, e.Ad, e.Tur, e.BelgeNo, e.Kurum, e.BitisTarihi, e.HatirlatmaGun, e.AktifMi, e.SorumluKullaniciId, e.IsyeriId })
            .SayfalaAsync(new SayfalamaIstegi(sayfa ?? 1, boyut ?? SayfalamaIstegi.VarsayilanBoyut), ct);

        var adlar = (await rehber.KullanicilarAsync(kullanici.FirmaId!.Value, ct)).ToDictionary(k => k.KullaniciId, k => k.AdSoyad);
        return TypedResults.Ok(new SayfaliSonuc<TakipEvrakiOzeti>(
            sonuc.Kayitlar.Select(e => new TakipEvrakiOzeti(e.Id, e.Ad, e.Tur, e.BelgeNo, e.Kurum, e.BitisTarihi, e.BitisTarihi.DayNumber - bugun.DayNumber,
                TakipEvrakTurleri.Durum(e.AktifMi, e.BitisTarihi, e.HatirlatmaGun, bugun),
                e.SorumluKullaniciId is { } s ? adlar.GetValueOrDefault(s) : null, e.IsyeriId)).ToList(),
            sonuc.Sayfa, sonuc.Boyut, sonuc.ToplamKayit));
    }

    private static async Task<IResult> SayilarAsync(EvrakTakipDbContext db, TimeProvider zaman, Guid? isyeriId, CancellationToken ct)
    {
        var bugun = Bugun(zaman);
        var kayitlar = db.TakipEvraklari.AsNoTracking().Where(e => isyeriId == null || e.IsyeriId == isyeriId);
        return TypedResults.Ok(new TakipEvrakSayilari(
            await kayitlar.CountAsync(e => e.AktifMi && e.BitisTarihi > bugun.AddDays(e.HatirlatmaGun), ct),
            await kayitlar.CountAsync(e => e.AktifMi && e.BitisTarihi >= bugun && e.BitisTarihi <= bugun.AddDays(e.HatirlatmaGun), ct),
            await kayitlar.CountAsync(e => e.AktifMi && e.BitisTarihi < bugun, ct),
            await kayitlar.CountAsync(e => !e.AktifMi, ct)));
    }

    private static async Task<IResult> SorumlularAsync(IFirmaRehberi rehber, IMevcutKullanici kullanici, CancellationToken ct) =>
        TypedResults.Ok((await rehber.KullanicilarAsync(kullanici.FirmaId!.Value, ct))
            .OrderBy(k => k.AdSoyad).Select(k => new TakipSorumlusu(k.KullaniciId, k.AdSoyad)).ToList());

    private static async Task<IResult> DetayAsync(Guid id, EvrakTakipDbContext db, TimeProvider zaman, CancellationToken ct)
    {
        var evrak = await db.TakipEvraklari.AsNoTracking().Include(e => e.Yenilemeler).SingleOrDefaultAsync(e => e.Id == id, ct);
        return evrak is null ? TakipHatalari.Bulunamadi.Problem() : TypedResults.Ok(Detay(evrak, Bugun(zaman)));
    }

    private static async Task<IResult> EkleAsync(
        TakipEvrakiKaydetIstegi istek, EvrakTakipDbContext db, IFirmaRehberi rehber, IsyeriSecici isyeriSecici, IMevcutKullanici kullanici, TimeProvider zaman, CancellationToken ct)
    {
        if (await BaglantiHatasiAsync(istek, null, rehber, isyeriSecici, kullanici, ct) is { } hata)
        {
            return hata.Problem();
        }

        var sonuc = TakipEvraki.Olustur(istek);
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        db.TakipEvraklari.Add(sonuc.Deger);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/evrak-takibi/{sonuc.Deger.Id}", Detay(sonuc.Deger, Bugun(zaman)));
    }

    private static async Task<IResult> GuncelleAsync(
        Guid id, TakipEvrakiKaydetIstegi istek, EvrakTakipDbContext db, IFirmaRehberi rehber, IsyeriSecici isyeriSecici, IMevcutKullanici kullanici, TimeProvider zaman,
        CancellationToken ct)
    {
        var evrak = await db.TakipEvraklari.Include(e => e.Yenilemeler).SingleOrDefaultAsync(e => e.Id == id, ct);
        if (evrak is null)
        {
            return TakipHatalari.Bulunamadi.Problem();
        }

        if (await BaglantiHatasiAsync(istek, evrak.IsyeriId, rehber, isyeriSecici, kullanici, ct) is { } hata)
        {
            return hata.Problem();
        }

        db.Entry(evrak).Property(e => e.Surum).OriginalValue = istek.Surum;
        var sonuc = evrak.Guncelle(istek);
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(Detay(evrak, Bugun(zaman)));
    }

    private static async Task<IResult> YenileAsync(
        Guid id, TakipYenilemeIstegi istek, EvrakTakipDbContext db, IMevcutKullanici kullanici, TimeProvider zaman, CancellationToken ct)
    {
        var evrak = await db.TakipEvraklari.Include(e => e.Yenilemeler).SingleOrDefaultAsync(e => e.Id == id, ct);
        if (evrak is null)
        {
            return TakipHatalari.Bulunamadi.Problem();
        }

        var sonuc = evrak.Yenile(istek, kullanici.KullaniciAdi);
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(Detay(evrak, Bugun(zaman)));
    }

    private static async Task<IResult> DurumDegistirAsync(Guid id, EvrakTakipDbContext db, Action<TakipEvraki> islem, TimeProvider zaman, CancellationToken ct)
    {
        var evrak = await db.TakipEvraklari.Include(e => e.Yenilemeler).SingleOrDefaultAsync(e => e.Id == id, ct);
        if (evrak is null)
        {
            return TakipHatalari.Bulunamadi.Problem();
        }

        islem(evrak);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(Detay(evrak, Bugun(zaman)));
    }

    private static async Task<IResult> SilAsync(Guid id, EvrakTakipDbContext db, CancellationToken ct)
    {
        var evrak = await db.TakipEvraklari.SingleOrDefaultAsync(e => e.Id == id, ct);
        if (evrak is null)
        {
            return TakipHatalari.Bulunamadi.Problem();
        }

        db.TakipEvraklari.Remove(evrak);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    // Sorumlu firmanın aktif kullanıcısı, işyeri kullanıcının girebildiği bir işyeri olmalı. Boş işyeri firma genelidir; değişmeyen işyeri (kapatılmış olsa da) korunur.
    private static async Task<Hata?> BaglantiHatasiAsync(
        TakipEvrakiKaydetIstegi istek, Guid? mevcutIsyeri, IFirmaRehberi rehber, IsyeriSecici isyeriSecici, IMevcutKullanici kullanici, CancellationToken ct)
    {
        if (istek.SorumluKullaniciId is { } sorumlu && (await rehber.KullanicilarAsync(kullanici.FirmaId!.Value, ct)).All(k => k.KullaniciId != sorumlu))
        {
            return TakipHatalari.SorumluGecersiz;
        }

        if (istek.IsyeriId is { } isyeri && isyeri != mevcutIsyeri)
        {
            var secim = await isyeriSecici.BelirleAsync(isyeri, ct);
            return secim.BasariliMi ? null : secim.Hata;
        }

        return null;
    }

    private static TakipEvrakiDetayi Detay(TakipEvraki e, DateOnly bugun) => new(
        e.Id, e.Ad, e.Tur, e.BelgeNo, e.Kurum, e.BaslangicTarihi, e.BitisTarihi, e.HatirlatmaGun, e.SorumluKullaniciId, e.IsyeriId, e.Aciklama, e.AktifMi,
        TakipEvrakTurleri.Durum(e.AktifMi, e.BitisTarihi, e.HatirlatmaGun, bugun), e.Surum,
        e.Yenilemeler.OrderByDescending(y => y.YeniBitisTarihi)
            .Select(y => new TakipYenilemesi(y.EskiBitisTarihi, y.YeniBitisTarihi, y.EskiBelgeNo, y.Not, y.Yapan, y.OlusturmaTarihi)).ToList());
}
