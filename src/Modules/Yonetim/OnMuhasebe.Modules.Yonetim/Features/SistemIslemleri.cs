using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Yonetim.Contracts;
using OnMuhasebe.Modules.Yonetim.Domain;
using OnMuhasebe.Modules.Yonetim.Infrastructure;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Yonetim.Features;

// Ürünü satan tarafın (SistemYoneticisi) firma açma ve lisans atama işlemleri.
internal static class SistemIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var sistem = grup.MapGroup("/sistem")
            .RequireAuthorization(p => p.RequireRole(Roller.SistemYoneticisi));

        sistem.MapPost("/firmalar", FirmaOlusturAsync).Dogrula<FirmaOlusturIstegi>();
        sistem.MapPut("/firmalar/{firmaId:guid}/lisans", LisansAtaAsync).Dogrula<LisansAtaIstegi>();
    }

    internal sealed class FirmaOlusturDogrulayici : AbstractValidator<FirmaOlusturIstegi>
    {
        public FirmaOlusturDogrulayici()
        {
            RuleFor(x => x.FirmaUnvani).NotEmpty().MaximumLength(250);
            RuleFor(x => x.VergiNo).Matches("^[0-9]{10,11}$").When(x => !string.IsNullOrWhiteSpace(x.VergiNo));
            RuleFor(x => x.VergiDairesi).MaximumLength(100);
            RuleFor(x => x.YoneticiAd).NotEmpty().MaximumLength(100);
            RuleFor(x => x.YoneticiSoyad).NotEmpty().MaximumLength(100);
            RuleFor(x => x.YoneticiEposta).Eposta();
            RuleFor(x => x.YoneticiSifre).Sifre().When(x => x.YoneticiSifre is not null);
            RuleFor(x => x.PaketKodu).NotEmpty();
        }
    }

    internal sealed class LisansAtaDogrulayici : AbstractValidator<LisansAtaIstegi>
    {
        public LisansAtaDogrulayici()
        {
            RuleFor(x => x.PaketKodu).NotEmpty();
            RuleForEach(x => x.EkModuller).Must(ModulKodlari.Tumu.Contains).WithMessage("Geçersiz modül kodu: {PropertyValue}");
        }
    }

    private static async Task<IResult> FirmaOlusturAsync(
        FirmaOlusturIstegi istek,
        YonetimDbContext db,
        IPasswordHasher<Kullanici> hasher,
        TimeProvider zaman,
        CancellationToken cancellationToken)
    {
        var paket = await db.Paketler.SingleOrDefaultAsync(p => p.Kod == istek.PaketKodu, cancellationToken);
        if (paket is null)
        {
            return YonetimHatalari.PaketBulunamadi.Problem();
        }

        var eposta = Kullanici.EpostaNormalize(istek.YoneticiEposta);
        var yonetici = await db.Kullanicilar.SingleOrDefaultAsync(k => k.Eposta == eposta, cancellationToken);
        if (yonetici is null)
        {
            if (string.IsNullOrEmpty(istek.YoneticiSifre))
            {
                return YonetimHatalari.SifreGerekli.Problem();
            }

            yonetici = Kullanici.Olustur(istek.YoneticiAd, istek.YoneticiSoyad, istek.YoneticiEposta);
            yonetici.SifreBelirle(hasher.HashPassword(yonetici, istek.YoneticiSifre));
            db.Kullanicilar.Add(yonetici);
        }

        var firma = Firma.Olustur(istek.FirmaUnvani, istek.VergiNo, istek.VergiDairesi);
        db.AddRange(
            firma,
            FirmaKullanicisi.Olustur(yonetici.Id, Roller.Yonetici, firma.Id),
            FirmaLisansi.Olustur(firma.Id, paket, zaman.GetUtcNow().UtcDateTime, GunSonu(istek.LisansBitisTarihi)));
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"/api/yonetim/sistem/firmalar/{firma.Id}", new FirmaOlusturCevabi(firma.Id, yonetici.Id));
    }

    private static async Task<IResult> LisansAtaAsync(
        Guid firmaId,
        LisansAtaIstegi istek,
        YonetimDbContext db,
        ILisansSorgusu lisansSorgusu,
        TimeProvider zaman,
        CancellationToken cancellationToken)
    {
        if (!await db.Firmalar.AnyAsync(f => f.Id == firmaId, cancellationToken))
        {
            return YonetimHatalari.FirmaBulunamadi.Problem();
        }

        var paket = await db.Paketler.SingleOrDefaultAsync(p => p.Kod == istek.PaketKodu, cancellationToken);
        if (paket is null)
        {
            return YonetimHatalari.PaketBulunamadi.Problem();
        }

        var lisans = await db.FirmaLisanslari
            .IgnoreQueryFilters([ModulDbContext.FirmaFiltresi])
            .SingleOrDefaultAsync(l => l.FirmaId == firmaId, cancellationToken);

        var bitis = GunSonu(istek.BitisTarihi);
        if (lisans is null)
        {
            db.FirmaLisanslari.Add(FirmaLisansi.Olustur(firmaId, paket, zaman.GetUtcNow().UtcDateTime, bitis, istek.EkModuller));
        }
        else
        {
            lisans.Guncelle(paket, bitis, istek.EkModuller);
        }

        await db.SaveChangesAsync(cancellationToken);
        await lisansSorgusu.OnbellektenKaldirAsync(firmaId, cancellationToken);
        return TypedResults.NoContent();
    }

    // Bitiş günü dahil: o günün sonuna kadar geçerli.
    private static DateTime GunSonu(DateOnly tarih) =>
        tarih.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
}
