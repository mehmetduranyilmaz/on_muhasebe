using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Yonetim.Contracts;
using OnMuhasebe.Modules.Yonetim.Domain;
using OnMuhasebe.Modules.Yonetim.Infrastructure;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Domain.Dogrulama;
using OnMuhasebe.Shared.Infrastructure.Bildirimler;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Yonetim.Features;

// Satış tarafı: paketlerin ücret/limit tanımı, ödeme hesabı ve firmaların havale bildirimlerinin onayı.
internal static class SistemAbonelikIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var sistem = grup.MapGroup("/sistem")
            .RequireAuthorization(p => p.RequireRole(Roller.SistemYoneticisi));

        sistem.MapGet("/paketler", PaketleriListeleAsync);
        sistem.MapPost("/paketler", PaketEkleAsync).Dogrula<PaketKaydetIstegi>();
        sistem.MapPut("/paketler/{kod}", PaketGuncelleAsync).Dogrula<PaketKaydetIstegi>();
        sistem.MapGet("/odeme-bilgileri", OdemeBilgileriniGetirAsync);
        sistem.MapPut("/odeme-bilgileri", OdemeBilgileriniKaydetAsync).Dogrula<OdemeBilgileri>();
        sistem.MapGet("/odeme-bildirimleri", BildirimleriListeleAsync);
        sistem.MapPost("/odeme-bildirimleri/{id:guid}/onayla", OnaylaAsync);
        sistem.MapPost("/odeme-bildirimleri/{id:guid}/reddet", ReddetAsync).Dogrula<BildirimRetIstegi>();
    }

    internal sealed class PaketDogrulayici : AbstractValidator<PaketKaydetIstegi>
    {
        public PaketDogrulayici()
        {
            RuleFor(x => x.Kod).NotEmpty().Matches("^[a-z0-9-]{2,50}$").WithMessage("Kod 2-50 karakter; küçük harf, rakam ve tire içerebilir.");
            RuleFor(x => x.Ad).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Aciklama).MaximumLength(500);
            RuleFor(x => x.Moduller).NotNull();
            RuleForEach(x => x.Moduller).Must(ModulKodlari.Tumu.Contains).WithMessage("Geçersiz modül kodu: {PropertyValue}");
            RuleFor(x => x.Moduller).Must(m => m.Count > 0).When(x => !x.TumModullerMi && x.Moduller is not null)
                .WithMessage("En az bir modül seçin.");
            RuleFor(x => x.KullaniciLimiti).GreaterThan(0).When(x => x.KullaniciLimiti is not null);
            RuleFor(x => x.AylikUcret).GreaterThanOrEqualTo(0).When(x => x.AylikUcret is not null);
            RuleFor(x => x.YillikUcret).GreaterThanOrEqualTo(0).When(x => x.YillikUcret is not null);
        }
    }

    internal sealed class OdemeBilgileriDogrulayici : AbstractValidator<OdemeBilgileri>
    {
        public OdemeBilgileriDogrulayici()
        {
            RuleFor(x => x.Alici).MaximumLength(200);
            RuleFor(x => x.Banka).MaximumLength(100);
            RuleFor(x => x.Iban).Must(Iban.GecerliMi).When(x => !string.IsNullOrWhiteSpace(x.Iban)).WithMessage("Geçerli bir IBAN girin.");
            RuleFor(x => x.Not).MaximumLength(500);
        }
    }

    internal sealed class RetDogrulayici : AbstractValidator<BildirimRetIstegi>
    {
        public RetDogrulayici() => RuleFor(x => x.Not).NotEmpty().MaximumLength(OdemeBildirimi.AciklamaUzunlugu);
    }

    private static async Task<IResult> PaketleriListeleAsync(YonetimDbContext db, CancellationToken cancellationToken)
    {
        var firmaSayilari = await db.FirmaLisanslari
            .IgnoreQueryFilters([ModulDbContext.FirmaFiltresi])
            .GroupBy(l => l.PaketId)
            .Select(g => new { PaketId = g.Key, Sayi = g.Count() })
            .ToDictionaryAsync(x => x.PaketId, x => x.Sayi, cancellationToken);
        var paketler = await db.Paketler.AsNoTracking().OrderBy(p => p.Sira).ThenBy(p => p.Ad).ToListAsync(cancellationToken);
        return TypedResults.Ok(paketler.Select(p => Ozet(p, firmaSayilari.GetValueOrDefault(p.Id))).ToList());
    }

    private static async Task<IResult> PaketEkleAsync(PaketKaydetIstegi istek, YonetimDbContext db, CancellationToken cancellationToken)
    {
        var kod = istek.Kod.Trim().ToLowerInvariant();
        if (await db.Paketler.AnyAsync(p => p.Kod == kod, cancellationToken))
        {
            return YonetimHatalari.PaketKoduKayitli.Problem();
        }

        var paket = Paket.Olustur(kod, Bilgiler(istek));
        db.Paketler.Add(paket);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/yonetim/sistem/paketler/{paket.Kod}", Ozet(paket, 0));
    }

    // Paketteki modül/limit değişikliği o paketi kullanan tüm firmalara yansır; lisans önbellekleri bu yüzden temizlenir.
    private static async Task<IResult> PaketGuncelleAsync(
        string kod, PaketKaydetIstegi istek, YonetimDbContext db, ILisansSorgusu lisansSorgusu, CancellationToken cancellationToken)
    {
        var paket = await db.Paketler.SingleOrDefaultAsync(p => p.Kod == kod, cancellationToken);
        if (paket is null)
        {
            return YonetimHatalari.PaketBulunamadi.Problem();
        }

        paket.Guncelle(Bilgiler(istek));
        await db.SaveChangesAsync(cancellationToken);

        var firmalar = await db.FirmaLisanslari
            .IgnoreQueryFilters([ModulDbContext.FirmaFiltresi])
            .Where(l => l.PaketId == paket.Id)
            .Select(l => l.FirmaId)
            .ToListAsync(cancellationToken);
        foreach (var firmaId in firmalar)
        {
            await lisansSorgusu.OnbellektenKaldirAsync(firmaId, cancellationToken);
        }

        return TypedResults.Ok(Ozet(paket, firmalar.Count));
    }

    private static async Task<IResult> OdemeBilgileriniGetirAsync(YonetimDbContext db, CancellationToken cancellationToken) =>
        TypedResults.Ok(OdemeBilgileriOlustur(await db.SistemAyarlari.AsNoTracking().FirstOrDefaultAsync(cancellationToken))
                        ?? new OdemeBilgileri(null, null, null, null));

    private static async Task<IResult> OdemeBilgileriniKaydetAsync(OdemeBilgileri istek, YonetimDbContext db, CancellationToken cancellationToken)
    {
        var ayar = await db.SistemAyarlari.FirstOrDefaultAsync(cancellationToken);
        if (ayar is null)
        {
            ayar = SistemAyari.Olustur();
            db.SistemAyarlari.Add(ayar);
        }

        ayar.OdemeBilgileriniGuncelle(istek.Alici, istek.Banka, istek.Iban, istek.Not);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(OdemeBilgileriOlustur(ayar));
    }

    private static async Task<IResult> BildirimleriListeleAsync(OdemeBildirimiDurumu? durum, YonetimDbContext db, CancellationToken cancellationToken)
    {
        var sorgu = db.OdemeBildirimleri.IgnoreQueryFilters([ModulDbContext.FirmaFiltresi]).AsNoTracking();
        if (durum is not null)
        {
            sorgu = sorgu.Where(b => b.Durum == durum);
        }

        return TypedResults.Ok(await Ozetler(sorgu.OrderByDescending(b => b.OlusturmaTarihi).Take(200), db).ToListAsync(cancellationToken));
    }

    // Yeni bitiş: lisans hâlâ geçerliyse mevcut bitişten, dolmuşsa bugünden itibaren istenen ay kadar. Paket bildirimdeki pakete geçer,
    // ek modüller korunur.
    private static async Task<IResult> OnaylaAsync(
        Guid id, BildirimOnayIstegi? istek, YonetimDbContext db, ILisansSorgusu lisansSorgusu, IEnumerable<IBildirimTetikleyici> tetikleyiciler,
        TimeProvider zaman, CancellationToken cancellationToken)
    {
        var bildirim = await db.OdemeBildirimleri
            .IgnoreQueryFilters([ModulDbContext.FirmaFiltresi])
            .Include(b => b.Paket)
            .SingleOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (bildirim is null)
        {
            return YonetimHatalari.BildirimBulunamadi.Problem();
        }

        var simdi = zaman.GetUtcNow().UtcDateTime;
        var lisans = await db.FirmaLisanslari
            .IgnoreQueryFilters([ModulDbContext.FirmaFiltresi])
            .Include(l => l.Paket)
            .SingleOrDefaultAsync(l => l.FirmaId == bildirim.FirmaId, cancellationToken);
        var yeniBitis = AbonelikHesabi.YeniBitis(lisans?.BitisTarihi, simdi, bildirim.AySayisi);

        var sonuc = bildirim.Onayla(yeniBitis, simdi, istek?.Not);
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        if (lisans is null)
        {
            db.FirmaLisanslari.Add(FirmaLisansi.Olustur(bildirim.FirmaId, bildirim.Paket, simdi, yeniBitis));
        }
        else
        {
            lisans.Guncelle(bildirim.Paket, yeniBitis, lisans.EkModuller);
        }

        await db.SaveChangesAsync(cancellationToken);
        await lisansSorgusu.OnbellektenKaldirAsync(bildirim.FirmaId, cancellationToken);
        await TetikleAsync(tetikleyiciler, bildirim.FirmaId, cancellationToken);
        return TypedResults.Ok(await Ozetler(db.OdemeBildirimleri.IgnoreQueryFilters([ModulDbContext.FirmaFiltresi]).Where(b => b.Id == id), db)
            .SingleAsync(cancellationToken));
    }

    private static async Task<IResult> ReddetAsync(
        Guid id, BildirimRetIstegi istek, YonetimDbContext db, IEnumerable<IBildirimTetikleyici> tetikleyiciler, TimeProvider zaman,
        CancellationToken cancellationToken)
    {
        var bildirim = await db.OdemeBildirimleri
            .IgnoreQueryFilters([ModulDbContext.FirmaFiltresi])
            .SingleOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (bildirim is null)
        {
            return YonetimHatalari.BildirimBulunamadi.Problem();
        }

        var sonuc = bildirim.Reddet(istek.Not, zaman.GetUtcNow().UtcDateTime);
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        await db.SaveChangesAsync(cancellationToken);
        await TetikleAsync(tetikleyiciler, bildirim.FirmaId, cancellationToken);
        return TypedResults.Ok(await Ozetler(db.OdemeBildirimleri.IgnoreQueryFilters([ModulDbContext.FirmaFiltresi]).Where(b => b.Id == id), db)
            .SingleAsync(cancellationToken));
    }

    // Firma yöneticisi sonucu saatlik çalışmayı beklemeden görsün.
    private static async Task TetikleAsync(IEnumerable<IBildirimTetikleyici> tetikleyiciler, Guid firmaId, CancellationToken ct)
    {
        foreach (var t in tetikleyiciler)
        {
            await t.FirmaIcinAsync(firmaId, ct);
        }
    }

    internal static IQueryable<OdemeBildirimiOzeti> Ozetler(IQueryable<OdemeBildirimi> sorgu, YonetimDbContext db) =>
        from b in sorgu
        join f in db.Firmalar on b.FirmaId equals f.Id
        select new OdemeBildirimiOzeti(
            b.Id, b.FirmaId, f.Unvan, b.Paket.Kod, b.Paket.Ad, b.AySayisi, b.Tutar,
            b.AySayisi == 1 ? b.Paket.AylikUcret : b.AySayisi == 12 ? b.Paket.YillikUcret : null,
            b.OdemeTarihi, b.Gonderen, b.Aciklama, b.Durum, b.SonucNotu, b.OlusturmaTarihi, b.SonucTarihi, b.YeniBitisTarihi);

    internal static OdemeBilgileri? OdemeBilgileriOlustur(SistemAyari? ayar) =>
        ayar is null ? null : new OdemeBilgileri(ayar.OdemeAlicisi, ayar.OdemeBankasi, ayar.OdemeIban, ayar.OdemeNotu);

    private static PaketBilgileri Bilgiler(PaketKaydetIstegi i) =>
        new(i.Ad, i.Aciklama, i.TumModullerMi, i.Moduller, i.KullaniciLimiti, i.AylikUcret, i.YillikUcret, i.SatistaMi, i.Sira);

    private static PaketOzeti Ozet(Paket p, int firmaSayisi) =>
        new(p.Kod, p.Ad, p.TumModullerMi, p.Moduller, p.Aciklama, p.KullaniciLimiti, p.AylikUcret, p.YillikUcret, p.SatistaMi, p.Sira, firmaSayisi);
}
