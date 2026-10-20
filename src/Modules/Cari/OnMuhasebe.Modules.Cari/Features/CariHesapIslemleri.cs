using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.Cari.Domain;
using OnMuhasebe.Modules.Cari.Infrastructure;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Shared.Contracts.Sayfalama;
using OnMuhasebe.Shared.Domain.Dogrulama;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Cari.Features;

internal static class CariHesapIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var hesaplar = grup.MapGroup("/hesaplar");
        hesaplar.MapGet("", ListeleAsync);
        hesaplar.MapGet("/{id:guid}", DetayAsync);
        hesaplar.MapPost("", EkleAsync).Dogrula<CariKaydetIstegi>();
        hesaplar.MapPut("/{id:guid}", GuncelleAsync).Dogrula<CariKaydetIstegi>();
        hesaplar.MapPost("/{id:guid}/pasiflestir", (Guid id, CariDbContext db, CancellationToken ct) =>
            DurumDegistirAsync(id, db, c => c.Pasiflestir(), ct));
        hesaplar.MapPost("/{id:guid}/aktiflestir", (Guid id, CariDbContext db, CancellationToken ct) =>
            DurumDegistirAsync(id, db, c => c.Aktiflestir(), ct));
        hesaplar.MapDelete("/{id:guid}", SilAsync);
    }

    internal sealed class Dogrulayici : AbstractValidator<CariKaydetIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.Kod).MaximumLength(20).Matches("^[A-Za-z0-9._-]*$")
                .WithMessage("Cari kodu yalnızca harf, rakam, nokta, tire ve alt çizgi içerebilir.");
            RuleFor(x => x.Unvan).NotEmpty().MaximumLength(250);
            RuleFor(x => x.Tur).IsInEnum();
            RuleFor(x => x.KisiTipi).IsInEnum();
            RuleFor(x => x.VergiNo)
                .Must(v => VergiKimlik.GecerliMi(v!.Trim()))
                .When(x => !string.IsNullOrWhiteSpace(x.VergiNo))
                .WithMessage("Geçerli bir vergi kimlik (10 hane) veya T.C. kimlik (11 hane) numarası girin.");
            RuleFor(x => x.VergiNo)
                .Must(v => v!.Trim().Length == 11)
                .When(x => x.KisiTipi == KisiTipi.Sahis && !string.IsNullOrWhiteSpace(x.VergiNo))
                .WithMessage("Şahıs carilerde T.C. kimlik numarası (11 hane) girilmelidir.");
            RuleFor(x => x.VergiDairesi).MaximumLength(100);
            RuleFor(x => x.Iban).Must(Iban.GecerliMi).When(x => !string.IsNullOrWhiteSpace(x.Iban))
                .WithMessage("Geçerli bir IBAN girin (TR ile başlayan 26 karakter).");
            RuleFor(x => x.Il).MaximumLength(50);
            RuleFor(x => x.Ilce).MaximumLength(50);
            RuleFor(x => x.Adres).MaximumLength(500);
            RuleFor(x => x.Telefon).MaximumLength(30);
            RuleFor(x => x.Eposta).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.Eposta));
            RuleFor(x => x.ParaBirimi).Must(ParaBirimleri.GecerliMi).WithMessage("Geçersiz para birimi.");
            RuleFor(x => x.VadeGun).InclusiveBetween(0, 365);
            RuleFor(x => x.RiskLimiti).GreaterThanOrEqualTo(0);
            RuleFor(x => x.YetkiliAd).MaximumLength(100);
            RuleFor(x => x.YetkiliTelefon).MaximumLength(30);
            RuleFor(x => x.Not).MaximumLength(1000);
        }
    }

    private static async Task<IResult> ListeleAsync(
        CariDbContext db,
        string? arama,
        CariTuru? tur,
        Guid? grupId,
        bool? aktif,
        int? sayfa,
        int? boyut,
        CancellationToken cancellationToken)
    {
        var sorgu = db.CariHesaplar.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(arama))
        {
            var desen = $"%{arama.Trim()}%";
            sorgu = sorgu.Where(c => EF.Functions.ILike(c.Unvan, desen) || EF.Functions.ILike(c.Kod, desen) || c.VergiNo == arama.Trim());
        }

        if (tur is { } t)
        {
            // "Müşteri ve tedarikçi" olan cariler her iki filtrede de görünür.
            sorgu = sorgu.Where(c => c.Tur == t || c.Tur == CariTuru.MusteriVeTedarikci);
        }

        if (grupId is not null)
        {
            sorgu = sorgu.Where(c => c.GrupId == grupId);
        }

        if (aktif is not null)
        {
            sorgu = sorgu.Where(c => c.AktifMi == aktif);
        }

        var sonuc = await sorgu
            .OrderBy(c => c.Unvan)
            .Select(c => new CariOzeti(
                c.Id, c.Kod, c.Unvan, c.Tur, c.Grup != null ? c.Grup.Ad : null, c.ParaBirimi, c.Telefon, c.AktifMi,
                db.CariHareketler.Where(h => h.CariHesapId == c.Id).Sum(h => h.Borc - h.Alacak),
                db.CariHareketler.Where(h => h.CariHesapId == c.Id).Sum(h => h.BorcTl - h.AlacakTl),
                c.RiskLimiti))
            .SayfalaAsync(new SayfalamaIstegi(sayfa ?? 1, boyut ?? SayfalamaIstegi.VarsayilanBoyut), cancellationToken);

        return TypedResults.Ok(sonuc);
    }

    private static async Task<IResult> DetayAsync(Guid id, CariDbContext db, CancellationToken cancellationToken)
    {
        var detay = await DetayGetirAsync(db, id, cancellationToken);
        return detay is null ? CariHatalari.CariBulunamadi.Problem() : TypedResults.Ok(detay);
    }

    private static async Task<IResult> EkleAsync(
        CariKaydetIstegi istek, CariDbContext db, CariKodUretici kodUretici, CancellationToken cancellationToken)
    {
        if (await GrupKontrolAsync(db, istek.GrupId, cancellationToken) is { } grupHatasi)
        {
            return grupHatasi.Problem();
        }

        var kod = string.IsNullOrWhiteSpace(istek.Kod) ? await kodUretici.SonrakiAsync(cancellationToken) : istek.Kod.Trim();
        if (await KodKullanimdaMiAsync(db, kod, haricId: null, cancellationToken))
        {
            return CariHatalari.KodKullaniliyor.Problem();
        }

        var cari = CariHesap.Olustur(kod, CariBilgileri.Istekten(istek));
        db.CariHesaplar.Add(cari);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"/api/cari/hesaplar/{cari.Id}", await DetayGetirAsync(db, cari.Id, cancellationToken));
    }

    private static async Task<IResult> GuncelleAsync(
        Guid id, CariKaydetIstegi istek, CariDbContext db, CariKodUretici kodUretici, CancellationToken cancellationToken)
    {
        var cari = await db.CariHesaplar.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (cari is null)
        {
            return CariHatalari.CariBulunamadi.Problem();
        }

        // İstemcinin gördüğü sürüm; arada başkası değiştirdiyse kayıt 409 ile reddedilir.
        db.Entry(cari).Property(c => c.Surum).OriginalValue = istek.Surum;

        if (await GrupKontrolAsync(db, istek.GrupId, cancellationToken) is { } grupHatasi)
        {
            return grupHatasi.Problem();
        }

        var kod = string.IsNullOrWhiteSpace(istek.Kod) ? cari.Kod : istek.Kod.Trim();
        if (kod != cari.Kod && await KodKullanimdaMiAsync(db, kod, cari.Id, cancellationToken))
        {
            return CariHatalari.KodKullaniliyor.Problem();
        }

        var hareketVarMi = await db.CariHareketler.AnyAsync(h => h.CariHesapId == id, cancellationToken);
        var sonuc = cari.Guncelle(kod, CariBilgileri.Istekten(istek), hareketVarMi);
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(await DetayGetirAsync(db, id, cancellationToken));
    }

    private static async Task<IResult> DurumDegistirAsync(
        Guid id, CariDbContext db, Action<CariHesap> islem, CancellationToken cancellationToken)
    {
        var cari = await db.CariHesaplar.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (cari is null)
        {
            return CariHatalari.CariBulunamadi.Problem();
        }

        islem(cari);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> SilAsync(Guid id, CariDbContext db, CancellationToken cancellationToken)
    {
        var cari = await db.CariHesaplar.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (cari is null)
        {
            return CariHatalari.CariBulunamadi.Problem();
        }

        var sonuc = CariHesap.SilinebilirMi(await db.CariHareketler.AnyAsync(h => h.CariHesapId == id, cancellationToken));
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        db.CariHesaplar.Remove(cari);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    internal static Task<CariDetayi?> DetayGetirAsync(CariDbContext db, Guid id, CancellationToken cancellationToken) =>
        db.CariHesaplar.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CariDetayi(
                c.Id, c.Kod, c.Unvan, c.Tur, c.KisiTipi, c.VergiNo, c.VergiDairesi, c.Il, c.Ilce, c.Adres, c.Telefon,
                c.Eposta, c.ParaBirimi, c.VadeGun, c.RiskLimiti, c.GrupId, c.Grup != null ? c.Grup.Ad : null,
                c.YetkiliAd, c.YetkiliTelefon, c.Not, c.AktifMi, c.FarkliDovizdeIslem, c.Iban,
                db.CariHareketler.Where(h => h.CariHesapId == c.Id).Sum(h => h.Borc - h.Alacak),
                db.CariHareketler.Where(h => h.CariHesapId == c.Id).Sum(h => h.BorcTl - h.AlacakTl),
                db.CariHareketler.Any(h => h.CariHesapId == c.Id),
                c.Surum))
            .SingleOrDefaultAsync(cancellationToken);

    private static Task<bool> KodKullanimdaMiAsync(CariDbContext db, string kod, Guid? haricId, CancellationToken cancellationToken) =>
        db.CariHesaplar
            .IgnoreQueryFilters([ModulDbContext.SilinmeFiltresi])
            .AnyAsync(c => c.Kod == kod && c.Id != haricId, cancellationToken);

    private static async Task<Hata?> GrupKontrolAsync(CariDbContext db, Guid? grupId, CancellationToken cancellationToken) =>
        grupId is null || await db.CariGruplari.AnyAsync(g => g.Id == grupId, cancellationToken)
            ? null
            : CariHatalari.GrupBulunamadi;
}
