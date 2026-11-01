using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Modules.Fatura.Domain;
using OnMuhasebe.Modules.Fatura.Infrastructure;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Sayfalama;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Bildirimler;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Fatura.Features;

// Tekrar planı kartı ve elle üretim. Kaydederken fatura kurallarından geçirilir: hatalı satır dönem gelince değil, şimdi söylenir.
internal static class TekrarFaturaIslemleri
{
    private static readonly string BelgeEkleme = FaturaYetkileri.Belge.Yetki(YetkiIslemleri.Ekle);

    public static void Map(RouteGroupBuilder grup)
    {
        var g = grup.MapGroup("/tekrar-planlari").Yetki(FaturaYetkileri.Tekrar);
        g.MapGet("", ListeleAsync);
        g.MapGet("/{id:guid}", GetirAsync);
        g.MapPost("", EkleAsync).Dogrula<TekrarPlaniKaydetIstegi>();
        g.MapPut("/{id:guid}", GuncelleAsync).Dogrula<TekrarPlaniKaydetIstegi>();
        g.MapDelete("/{id:guid}", SilAsync);
        g.MapPost("/{id:guid}/duraklat", (Guid id, FaturaDbContext db, ICariSorgusu cari, CancellationToken ct) => DurumAsync(id, p => p.Duraklat(), db, cari, ct)).Islem(YetkiIslemleri.Duzenle);
        g.MapPost("/{id:guid}/devam", (Guid id, FaturaDbContext db, ICariSorgusu cari, CancellationToken ct) => DurumAsync(id, p => p.DevamEt(), db, cari, ct)).Islem(YetkiIslemleri.Duzenle);
        g.MapPost("/{id:guid}/uret", UretAsync).Islem(YetkiIslemleri.Ekle);
    }

    internal sealed class Dogrulayici : AbstractValidator<TekrarPlaniKaydetIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.Ad).NotEmpty().WithMessage("Plan adını yazın.").MaximumLength(200);
            RuleFor(x => x.CariHesapId).NotEmpty().WithMessage("Cari seçin.");
            RuleFor(x => x.Siklik).IsInEnum();
            RuleFor(x => x.Gun).InclusiveBetween(1, 31).WithMessage("Ayın günü 1 ile 31 arasında olmalıdır.");
            RuleFor(x => x.VadeGun).InclusiveBetween(0, 365).When(x => x.VadeGun is not null);
            RuleFor(x => x.Aciklama).MaximumLength(500);
            RuleFor(x => x.StopajOrani).InclusiveBetween(0m, 100m);
            RuleFor(x => x.ArtisOrani).InclusiveBetween(0m, 500m).When(x => x.ArtisOrani is not null);
            RuleFor(x => x.ArtisAyi).InclusiveBetween(1, 12).When(x => x.ArtisAyi is not null);
            RuleFor(x => x.Satirlar).NotEmpty().WithMessage("En az bir satır ekleyin.").Must(s => s is null || s.Count <= 100).WithMessage("En çok 100 satır olabilir.");
        }
    }

    private static async Task<IResult> ListeleAsync(FaturaDbContext db, ICariSorgusu cari, string? arama, TekrarDurumu? durum, int? sayfa, int? boyut, CancellationToken ct)
    {
        var sorgu = db.TekrarPlanlari.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(arama))
        {
            var desen = AramaDeseni.Icerir(arama);
            sorgu = sorgu.Where(p => EF.Functions.ILike(p.Ad, desen) || EF.Functions.ILike(p.Kod, desen));
        }

        if (durum is not null)
        {
            sorgu = sorgu.Where(p => p.Durum == durum);
        }

        var sonuc = await sorgu.OrderBy(p => p.SonrakiTarih == null).ThenBy(p => p.SonrakiTarih).ThenBy(p => p.Kod)
            .Select(p => new TekrarPlaniOzeti(p.Id, p.Kod, p.Ad, p.CariHesapId, "", p.Siklik, p.SonrakiTarih, p.Durum, p.OtomatikKesinlestir, p.SonHata, p.IsyeriId))
            .SayfalaAsync(new SayfalamaIstegi(sayfa ?? 1, boyut ?? SayfalamaIstegi.VarsayilanBoyut), ct);
        var unvanlar = await cari.UnvanlarAsync(sonuc.Kayitlar.Select(p => p.CariHesapId).Distinct().ToList(), ct);
        return TypedResults.Ok(new SayfaliSonuc<TekrarPlaniOzeti>(
            sonuc.Kayitlar.Select(p => p with { CariUnvan = unvanlar.GetValueOrDefault(p.CariHesapId, "") }).ToList(), sonuc.Sayfa, sonuc.Boyut, sonuc.ToplamKayit));
    }

    private static async Task<IResult> GetirAsync(Guid id, FaturaDbContext db, ICariSorgusu cari, CancellationToken ct)
    {
        var plan = await db.TekrarPlanlari.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct);
        return plan is null ? TekrarHatalari.Bulunamadi.Problem() : TypedResults.Ok(await DetayAsync(plan, db, cari, ct));
    }

    private static async Task<IResult> EkleAsync(
        TekrarPlaniKaydetIstegi istek, FaturaDbContext db, ICariSorgusu cari, FaturaHazirlayici hazirlayici, IsyeriSecici isyeriSecici, IYetkiDenetimi yetki, IMevcutKullanici kullanici, CancellationToken ct)
    {
        var denetim = await DenetleAsync(istek, hazirlayici, isyeriSecici, yetki, ct);
        if (!denetim.BasariliMi)
        {
            return denetim.Hata.Problem();
        }

        var kod = await KodUretici.SonrakiAsync(db, "fatura.tekrar_plani", "TP", kullanici.FirmaId!.Value, ct);
        var plan = TekrarPlani.Olustur(kod, istek, denetim.Deger);
        db.TekrarPlanlari.Add(plan);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/fatura/tekrar-planlari/{plan.Id}", await DetayAsync(plan, db, cari, ct));
    }

    private static async Task<IResult> GuncelleAsync(
        Guid id, TekrarPlaniKaydetIstegi istek, FaturaDbContext db, ICariSorgusu cari, FaturaHazirlayici hazirlayici, IsyeriSecici isyeriSecici, IYetkiDenetimi yetki, CancellationToken ct)
    {
        var plan = await db.TekrarPlanlari.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null)
        {
            return TekrarHatalari.Bulunamadi.Problem();
        }

        var denetim = await DenetleAsync(istek, hazirlayici, isyeriSecici, yetki, ct);
        if (!denetim.BasariliMi)
        {
            return denetim.Hata.Problem();
        }

        db.Entry(plan).Property(p => p.Surum).OriginalValue = istek.Surum;
        var sonDonem = await db.TekrarUretimleri.Where(u => u.PlanId == id).OrderByDescending(u => u.Donem).Select(u => (DateOnly?)u.Donem).FirstOrDefaultAsync(ct);
        plan.Guncelle(istek, denetim.Deger, sonDonem);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await DetayAsync(plan, db, cari, ct));
    }

    private static async Task<IResult> SilAsync(Guid id, FaturaDbContext db, CancellationToken ct)
    {
        var plan = await db.TekrarPlanlari.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null)
        {
            return TekrarHatalari.Bulunamadi.Problem();
        }

        // Kesilmiş faturalar silinmez, yalnız plan durur.
        db.TekrarPlanlari.Remove(plan);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> DurumAsync(Guid id, Func<TekrarPlani, Sonuc> islem, FaturaDbContext db, ICariSorgusu cari, CancellationToken ct)
    {
        var plan = await db.TekrarPlanlari.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null)
        {
            return TekrarHatalari.Bulunamadi.Problem();
        }

        var sonuc = islem(plan);
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await DetayAsync(plan, db, cari, ct));
    }

    // Arka plandaki üretimle aynı yol: tarihi gelen dönemler kesilir. Geleceğe fatura kesilmez.
    private static async Task<IResult> UretAsync(
        Guid id, FaturaDbContext db, ICariSorgusu cari, TekrarCalistirici calistirici, IYetkiDenetimi yetki, IMevcutKullanici kullanici,
        IEnumerable<IBildirimTetikleyici> tetikleyiciler, TimeProvider zaman, CancellationToken ct)
    {
        if (!await yetki.VarMiAsync(BelgeEkleme, ct))
        {
            return TekrarHatalari.FaturaYetkisi.Problem();
        }

        var plan = await db.TekrarPlanlari.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null)
        {
            return TekrarHatalari.Bulunamadi.Problem();
        }

        if (plan.Durum != TekrarDurumu.Aktif)
        {
            return TekrarHatalari.Pasif.Problem();
        }

        if (plan.SonrakiTarih > TurkiyeSaati.Bugun(zaman))
        {
            return TekrarHatalari.HenuzDegil.Problem();
        }

        var firmaId = kullanici.FirmaId!.Value;
        var (uretilen, hata) = await calistirici.PlanAsync(firmaId, id, ct);
        foreach (var t in tetikleyiciler)
        {
            await t.FirmaIcinAsync(firmaId, ct);
        }

        var guncel = await db.TekrarPlanlari.AsNoTracking().SingleAsync(p => p.Id == id, ct);
        return TypedResults.Ok(new TekrarUretSonucu(uretilen, hata, await DetayAsync(guncel, db, cari, ct)));
    }

    private static async Task<Sonuc<Guid>> DenetleAsync(TekrarPlaniKaydetIstegi istek, FaturaHazirlayici hazirlayici, IsyeriSecici isyeriSecici, IYetkiDenetimi yetki, CancellationToken ct)
    {
        if (!await yetki.VarMiAsync(BelgeEkleme, ct))
        {
            return TekrarHatalari.FaturaYetkisi;
        }

        if (istek.Bitis < istek.Baslangic)
        {
            return TekrarHatalari.BitisOnce;
        }

        if ((istek.ArtisOrani is null) != (istek.ArtisAyi is null))
        {
            return TekrarHatalari.ArtisEksik;
        }

        var isyeri = await isyeriSecici.BelirleAsync(istek.IsyeriId, ct);
        if (!isyeri.BasariliMi)
        {
            return isyeri.Hata;
        }

        var ornek = new FaturaKaydetIstegi(FaturaTuru.Satis, null, istek.CariHesapId, null, istek.Baslangic, null, null, null, GenelIskontoTuru.Yuzde, 0, null, istek.Satirlar, 0, null, istek.StopajOrani, istek.MasrafMerkeziId, isyeri.Deger);
        var hazirlik = await hazirlayici.HazirlaAsync(ornek, ct);
        return hazirlik.BasariliMi ? isyeri.Deger : hazirlik.Hata;
    }

    private static async Task<TekrarPlaniDetayi> DetayAsync(TekrarPlani p, FaturaDbContext db, ICariSorgusu cari, CancellationToken ct)
    {
        var uretimler = await (from u in db.TekrarUretimleri.AsNoTracking()
                               join f in db.Faturalar.AsNoTracking() on u.FaturaId equals f.Id
                               where u.PlanId == p.Id
                               orderby u.Donem descending
                               select new TekrarUretimCevabi(u.Donem, u.FaturaId, f.Numara, u.UygulananArtis, u.OlusturmaTarihi)).Take(60).ToListAsync(ct);
        var bilgi = await cari.GetirAsync(p.CariHesapId, ct);
        return new TekrarPlaniDetayi(p.Id, p.Kod, p.Ad, p.CariHesapId, bilgi?.Unvan ?? "", bilgi?.Kod, bilgi?.ParaBirimi ?? "TRY", p.Siklik, p.Gun, p.Baslangic, p.Bitis, p.VadeGun, p.Aciklama,
            p.OtomatikKesinlestir, p.StopajOrani, p.MasrafMerkeziId, p.IsyeriId, p.ArtisOrani, p.ArtisAyi, p.SonrakiTarih, p.Durum, p.SonHata, p.SonHataTarihi, p.Satirlar, uretimler, p.Surum);
    }
}
