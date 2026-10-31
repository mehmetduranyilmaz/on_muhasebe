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
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Contracts.Sayfalama;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
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

        sistem.MapGet("/firmalar", FirmalariListeleAsync);
        sistem.MapPost("/firmalar", FirmaOlusturAsync).Dogrula<FirmaOlusturIstegi>();
        sistem.MapPut("/firmalar/{firmaId:guid}/lisans", LisansAtaAsync).Dogrula<LisansAtaIstegi>();
    }

    private static async Task<IResult> FirmalariListeleAsync(
        YonetimDbContext db, string? arama, int? sayfa, int? boyut, CancellationToken cancellationToken)
    {
        var sorgu = db.Firmalar.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(arama))
        {
            var desen = AramaDeseni.Icerir(arama);
            sorgu = sorgu.Where(f => EF.Functions.ILike(f.Unvan, desen) || f.VergiNo == arama.Trim());
        }

        var firmalar = await sorgu
            .OrderBy(f => f.Unvan)
            .Select(f => new { f.Id, f.Unvan, f.VergiNo, f.AktifMi })
            .SayfalaAsync(new SayfalamaIstegi(sayfa ?? 1, boyut ?? SayfalamaIstegi.VarsayilanBoyut), cancellationToken);

        // Lisans ve kullanıcı sayısı yalnızca bu sayfadaki firmalar için ayrı sorgularla toplanır.
        var idler = firmalar.Kayitlar.Select(f => f.Id).ToList();
        var lisanslar = await db.FirmaLisanslari
            .IgnoreQueryFilters([ModulDbContext.FirmaFiltresi])
            .AsNoTracking()
            .Where(l => idler.Contains(l.FirmaId))
            .Select(l => new { l.FirmaId, l.Paket.Kod, l.BitisTarihi, l.EkModuller })
            .ToDictionaryAsync(l => l.FirmaId, cancellationToken);
        var kullaniciSayilari = await db.FirmaKullanicilari
            .IgnoreQueryFilters([ModulDbContext.FirmaFiltresi])
            .Where(u => idler.Contains(u.FirmaId) && u.AktifMi)
            .GroupBy(u => u.FirmaId)
            .Select(g => new { FirmaId = g.Key, Sayi = g.Count() })
            .ToDictionaryAsync(x => x.FirmaId, x => x.Sayi, cancellationToken);

        var kayitlar = firmalar.Kayitlar.Select(f =>
        {
            var lisans = lisanslar.GetValueOrDefault(f.Id);
            return new SistemFirmaOzeti(
                f.Id, f.Unvan, f.VergiNo, f.AktifMi, lisans?.Kod, lisans?.BitisTarihi,
                lisans?.EkModuller ?? [], kullaniciSayilari.GetValueOrDefault(f.Id));
        }).ToList();

        return TypedResults.Ok(new SayfaliSonuc<SistemFirmaOzeti>(kayitlar, firmalar.Sayfa, firmalar.Boyut, firmalar.ToplamKayit));
    }

    internal sealed class FirmaOlusturDogrulayici : AbstractValidator<FirmaOlusturIstegi>
    {
        public FirmaOlusturDogrulayici()
        {
            RuleFor(x => x.FirmaUnvani).NotEmpty().MaximumLength(250);
            RuleFor(x => x.VergiNo).VergiNo();
            RuleFor(x => x.VergiDairesi).MaximumLength(100);
            RuleFor(x => x.YoneticiEposta).Eposta();
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
        YetkiKatalogu katalog,
        KimlikPostasi posta,
        TimeProvider zaman,
        CancellationToken cancellationToken)
    {
        var paket = await db.Paketler.SingleOrDefaultAsync(p => p.Kod == istek.PaketKodu, cancellationToken);
        if (paket is null)
        {
            return YonetimHatalari.PaketBulunamadi.Problem();
        }

        // Yönetici hesabı doğrudan bağlanmaz; davetle e-postanın sahibi katılır (bu adresle başkasının açtığı hesaba firma geçmez).
        var simdi = zaman.GetUtcNow().UtcDateTime;
        var firma = Firma.Olustur(istek.FirmaUnvani, istek.VergiNo, istek.VergiDairesi);
        var roller = RolSablonlari.Olustur(katalog.Tumu, firma.Id);
        var (kod, ozet) = TekKullanimlikKod.Uret();
        db.Add(firma);
        db.Roller.AddRange(roller);
        db.Isyerleri.Add(Isyeri.MerkezOlustur(firma.Id));
        db.AddRange(
            FirmaLisansi.Olustur(firma.Id, paket, simdi, GunSonu(istek.LisansBitisTarihi)),
            FirmaDaveti.Olustur(istek.YoneticiEposta, roller[0].Id, ozet, simdi, firma.Id));
        await db.SaveChangesAsync(cancellationToken);
        await posta.DavetAsync(Kullanici.EpostaNormalize(istek.YoneticiEposta), firma.Unvan, "Ön Muhasebe", kod, cancellationToken);

        return TypedResults.Created($"/api/yonetim/sistem/firmalar/{firma.Id}", new FirmaOlusturCevabi(firma.Id));
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
