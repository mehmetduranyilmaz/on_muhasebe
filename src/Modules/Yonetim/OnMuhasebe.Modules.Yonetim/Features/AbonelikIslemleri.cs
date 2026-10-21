using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Yonetim.Contracts;
using OnMuhasebe.Modules.Yonetim.Domain;
using OnMuhasebe.Modules.Yonetim.Infrastructure;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Yonetim.Features;

// Firma yöneticisinin abonelik ekranı: mevcut paket ve süre, satıştaki paketlerin ücretleri, ödeme hesabı ve "havale yaptım" bildirimi.
// Lisans süresi dolmuş (salt okunur) firma da bildirim gönderebilir; modül lisans filtresi bu uçlarda yoktur.
internal static class AbonelikIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var abonelik = grup.MapGroup("/abonelik").RequireAuthorization(p => p.RequireRole(Roller.Yonetici));

        abonelik.MapGet("", GetirAsync);
        abonelik.MapPost("/odeme-bildirimleri", BildirAsync).Dogrula<OdemeBildirimiIstegi>();
    }

    internal sealed class Dogrulayici : AbstractValidator<OdemeBildirimiIstegi>
    {
        public Dogrulayici(TimeProvider zaman)
        {
            RuleFor(x => x.PaketKodu).NotEmpty();
            RuleFor(x => x.AySayisi).Must(a => a is 1 or 12).WithMessage("Süre aylık (1) ya da yıllık (12) olmalıdır.");
            RuleFor(x => x.Tutar).GreaterThan(0);
            RuleFor(x => x.OdemeTarihi)
                .Must(t => t <= DateOnly.FromDateTime(zaman.GetLocalNow().DateTime).AddDays(1))
                .WithMessage("Ödeme tarihi ileri bir tarih olamaz.");
            RuleFor(x => x.Gonderen).NotEmpty().MaximumLength(OdemeBildirimi.GonderenUzunlugu);
            RuleFor(x => x.Aciklama).MaximumLength(OdemeBildirimi.AciklamaUzunlugu);
        }
    }

    private static async Task<IResult> GetirAsync(IMevcutKullanici mevcutKullanici, YonetimDbContext db, TimeProvider zaman, CancellationToken ct)
    {
        var firmaId = mevcutKullanici.FirmaId!.Value;
        var simdi = zaman.GetUtcNow().UtcDateTime;
        var lisans = await db.FirmaLisanslari.AsNoTracking().Include(l => l.Paket).SingleOrDefaultAsync(ct);
        var kullaniciSayisi = await db.FirmaKullanicilari.CountAsync(u => u.AktifMi, ct);
        var paketler = await db.Paketler.AsNoTracking()
            .Where(p => p.SatistaMi)
            .OrderBy(p => p.Sira).ThenBy(p => p.Ad)
            .Select(p => new SatistakiPaket(p.Kod, p.Ad, p.Aciklama, p.TumModullerMi, p.Moduller, p.KullaniciLimiti, p.AylikUcret, p.YillikUcret))
            .ToListAsync(ct);
        var odeme = SistemAbonelikIslemleri.OdemeBilgileriOlustur(await db.SistemAyarlari.AsNoTracking().FirstOrDefaultAsync(ct));
        var bildirimler = await SistemAbonelikIslemleri
            .Ozetler(db.OdemeBildirimleri.AsNoTracking().Where(b => b.FirmaId == firmaId).OrderByDescending(b => b.OlusturmaTarihi).Take(20), db)
            .ToListAsync(ct);

        return TypedResults.Ok(new AbonelikDurumu(
            lisans?.Paket.Kod,
            lisans?.Paket.Ad,
            lisans?.BitisTarihi,
            lisans is null ? 0 : AbonelikHesabi.KalanGun(lisans.BitisTarihi, simdi),
            lisans is null || lisans.BitisTarihi <= simdi,
            kullaniciSayisi,
            lisans?.Paket.KullaniciLimiti,
            paketler,
            odeme?.Iban is null ? null : odeme,
            bildirimler));
    }

    private static async Task<IResult> BildirAsync(
        OdemeBildirimiIstegi istek, IMevcutKullanici mevcutKullanici, YonetimDbContext db, CancellationToken ct)
    {
        var paket = await db.Paketler.SingleOrDefaultAsync(p => p.Kod == istek.PaketKodu, ct);
        if (paket is null)
        {
            return YonetimHatalari.PaketBulunamadi.Problem();
        }

        if (!paket.SatistaMi)
        {
            return YonetimHatalari.PaketSatistaDegil.Problem();
        }

        if (paket.Ucret(istek.AySayisi) is null)
        {
            return YonetimHatalari.SureSatilmiyor.Problem();
        }

        if (await db.SistemAyarlari.AllAsync(a => a.OdemeIban == null, ct))
        {
            return YonetimHatalari.OdemeBilgisiYok.Problem();
        }

        // Aynı ödemenin iki kez bildirilmesini ve onay beklerken yığılmayı önler.
        if (await db.OdemeBildirimleri.AnyAsync(b => b.Durum == OdemeBildirimiDurumu.Bekliyor, ct))
        {
            return YonetimHatalari.BekleyenBildirimVar.Problem();
        }

        var bildirim = OdemeBildirimi.Olustur(paket, istek.AySayisi, istek.Tutar, istek.OdemeTarihi, istek.Gonderen, istek.Aciklama);
        db.OdemeBildirimleri.Add(bildirim);
        await db.SaveChangesAsync(ct);

        var ozet = await SistemAbonelikIslemleri.Ozetler(db.OdemeBildirimleri.Where(b => b.Id == bildirim.Id), db).SingleAsync(ct);
        return TypedResults.Created($"/api/yonetim/abonelik/odeme-bildirimleri/{bildirim.Id}", ozet);
    }
}
