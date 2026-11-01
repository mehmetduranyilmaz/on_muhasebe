using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Arac.Contracts;
using OnMuhasebe.Modules.Arac.Domain;
using OnMuhasebe.Modules.Arac.Infrastructure;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Sayfalama;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Arac.Features;

internal static class AracIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var araclar = grup.MapGroup("/araclar").Yetki(AracYetkileri.Kart);
        araclar.MapGet("", ListeleAsync);
        araclar.MapGet("/{id:guid}", DetayAsync);
        araclar.MapPost("", EkleAsync).Dogrula<AracKaydetIstegi>();
        araclar.MapPut("/{id:guid}", GuncelleAsync).Dogrula<AracKaydetIstegi>();
        araclar.MapDelete("/{id:guid}", SilAsync);
    }

    internal sealed class Dogrulayici : AbstractValidator<AracKaydetIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.Plaka).NotEmpty().MaximumLength(20);
            RuleFor(x => x.Marka).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Model).NotEmpty().MaximumLength(100);
            RuleFor(x => x.ModelYili).InclusiveBetween(1950, 2100).When(x => x.ModelYili is not null);
            RuleFor(x => x.Tur).IsInEnum();
            RuleFor(x => x.Yakit).IsInEnum();
            RuleFor(x => x.Mulkiyet).IsInEnum();
            RuleFor(x => x.Durum).IsInEnum();
            RuleFor(x => x.SasiNo).MaximumLength(30);
            RuleFor(x => x.MotorNo).MaximumLength(30);
            RuleFor(x => x.RuhsatSahibi).MaximumLength(200);
            RuleFor(x => x.Surucu).MaximumLength(150);
            RuleFor(x => x.SurucuTelefon).MaximumLength(30);
            RuleFor(x => x.GuncelKm).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Aciklama).MaximumLength(1000);
        }
    }

    private static async Task<IResult> ListeleAsync(
        AracDbContext db, TimeProvider zaman, string? arama, AracDurumu? durum, Guid? isyeriId, int? sayfa, int? boyut, CancellationToken ct)
    {
        var bugun = DateOnly.FromDateTime(zaman.GetLocalNow().DateTime);
        var sorgu = db.Araclar.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(arama))
        {
            var desen = AramaDeseni.Icerir(arama);
            var plaka = AramaDeseni.Icerir(Plakalar.Normalize(arama));
            sorgu = sorgu.Where(a => EF.Functions.ILike(a.Plaka, plaka) || EF.Functions.ILike(a.Marka, desen) || EF.Functions.ILike(a.Model, desen)
                                     || EF.Functions.ILike(a.Surucu ?? "", desen));
        }

        sorgu = sorgu.Where(a => a.Durum == (durum ?? AracDurumu.Aktif));
        if (isyeriId is not null)
        {
            sorgu = sorgu.Where(a => a.IsyeriId == isyeriId);
        }

        var sonuc = await sorgu.OrderBy(a => a.Plaka)
            .Select(a => new
            {
                a.Id, a.Plaka, a.Marka, a.Model, a.ModelYili, a.Tur, a.Durum, a.Surucu, a.GuncelKm, a.IsyeriId,
                Yakin = db.Yukumlulukler.Where(y => y.AracId == a.Id && !y.YenilendiMi).OrderBy(y => y.BitisTarihi)
                    .Select(y => new { y.Tur, y.Ad, y.BitisTarihi, y.HatirlatmaGun }).FirstOrDefault()
            })
            .SayfalaAsync(new SayfalamaIstegi(sayfa ?? 1, boyut ?? SayfalamaIstegi.VarsayilanBoyut), ct);

        return TypedResults.Ok(new SayfaliSonuc<AracOzeti>(
            sonuc.Kayitlar.Select(a => new AracOzeti(a.Id, a.Plaka, a.Marka, a.Model, a.ModelYili, a.Tur, a.Durum, a.Surucu, a.GuncelKm, a.IsyeriId,
                a.Yakin is null ? null : a.Yakin.Tur == YukumlulukTuru.Diger && a.Yakin.Ad is not null ? a.Yakin.Ad : AracMetinleri.Yukumluluk(a.Yakin.Tur),
                a.Yakin?.BitisTarihi,
                a.Yakin is null ? null : AracHesaplari.Durum(a.Yakin.BitisTarihi, a.Yakin.HatirlatmaGun, bugun))).ToList(),
            sonuc.Sayfa, sonuc.Boyut, sonuc.ToplamKayit));
    }

    private static async Task<IResult> DetayAsync(Guid id, AracDbContext db, CancellationToken ct)
    {
        var arac = await db.Araclar.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct);
        return arac is null ? AracHatalari.Bulunamadi.Problem() : TypedResults.Ok(Detay(arac));
    }

    private static async Task<IResult> EkleAsync(
        AracKaydetIstegi istek, AracDbContext db, IsyeriSecici isyeriSecici, IKasaBankaSorgusu kasa, CancellationToken ct)
    {
        if (await HataAsync(istek, null, null, db, isyeriSecici, kasa, ct) is { } hata)
        {
            return hata.Problem();
        }

        var arac = AracKarti.Olustur(istek);
        db.Araclar.Add(arac);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/arac/araclar/{arac.Id}", Detay(arac));
    }

    private static async Task<IResult> GuncelleAsync(
        Guid id, AracKaydetIstegi istek, AracDbContext db, IsyeriSecici isyeriSecici, IKasaBankaSorgusu kasa, CancellationToken ct)
    {
        var arac = await db.Araclar.SingleOrDefaultAsync(a => a.Id == id, ct);
        if (arac is null)
        {
            return AracHatalari.Bulunamadi.Problem();
        }

        if (await HataAsync(istek, id, arac, db, isyeriSecici, kasa, ct) is { } hata)
        {
            return hata.Problem();
        }

        db.Entry(arac).Property(a => a.Surum).OriginalValue = istek.Surum;
        arac.Guncelle(istek);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(Detay(arac));
    }

    private static async Task<IResult> SilAsync(Guid id, AracDbContext db, CancellationToken ct)
    {
        var arac = await db.Araclar.SingleOrDefaultAsync(a => a.Id == id, ct);
        if (arac is null)
        {
            return AracHatalari.Bulunamadi.Problem();
        }

        db.Araclar.Remove(arac);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    // Değişmeyen bağlar (kapatılmış işyeri, pasif masraf merkezi) korunur; yalnız yeni verilen bağ denetlenir.
    private static async Task<Hata?> HataAsync(
        AracKaydetIstegi istek, Guid? haricId, AracKarti? mevcut, AracDbContext db, IsyeriSecici isyeriSecici, IKasaBankaSorgusu kasa, CancellationToken ct)
    {
        var plaka = Plakalar.Normalize(istek.Plaka);
        if (!Plakalar.GecerliMi(plaka, istek.Tur))
        {
            return AracHatalari.PlakaGecersiz;
        }

        if (await db.Araclar.IgnoreQueryFilters([ModulDbContext.IsyeriFiltresi]).AnyAsync(a => a.Plaka == plaka && a.Id != haricId, ct))
        {
            return AracHatalari.PlakaKayitli;
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
            return AracHatalari.MasrafMerkeziGecersiz;
        }

        if (istek.DemirbasId is { } demirbas && demirbas != mevcut?.DemirbasId && !await kasa.DemirbasVarMiAsync(demirbas, ct))
        {
            return AracHatalari.DemirbasGecersiz;
        }

        return null;
    }

    private static AracDetayi Detay(AracKarti a) => new(
        a.Id, a.Plaka, a.Marka, a.Model, a.ModelYili, a.Tur, a.Yakit, a.SasiNo, a.MotorNo, a.RuhsatSahibi, a.Mulkiyet, a.IsyeriId, a.Surucu, a.SurucuTelefon,
        a.GuncelKm, a.Durum, a.DemirbasId, a.MasrafMerkeziId, a.Aciklama, a.Surum);
}
