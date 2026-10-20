using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.KasaBanka.Domain;
using OnMuhasebe.Modules.KasaBanka.Infrastructure;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.KasaBanka.Features;

// Gelir/gider kategorileri firma ayarıdır; ilk listelemede başlangıç kategorileri açılır.
internal static class KategoriIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var kategoriler = grup.MapGroup("/kategoriler");
        kategoriler.MapGet("", ListeleAsync);
        kategoriler.MapPost("", EkleAsync).Dogrula<KategoriKaydetIstegi>();
        kategoriler.MapPut("/{id:guid}", GuncelleAsync).Dogrula<KategoriKaydetIstegi>();
        kategoriler.MapPost("/{id:guid}/pasiflestir", (Guid id, KasaBankaDbContext db, CancellationToken ct) => AktiflikAsync(id, false, db, ct));
        kategoriler.MapPost("/{id:guid}/aktiflestir", (Guid id, KasaBankaDbContext db, CancellationToken ct) => AktiflikAsync(id, true, db, ct));
        kategoriler.MapDelete("/{id:guid}", SilAsync);
    }

    internal sealed class Dogrulayici : AbstractValidator<KategoriKaydetIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.Ad).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Tur).IsInEnum();
        }
    }

    private static async Task<IResult> ListeleAsync(KasaBankaDbContext db, KategoriServisi servis, CancellationToken cancellationToken)
    {
        await servis.VarsayilanlariHazirlaAsync(cancellationToken);
        return TypedResults.Ok(await db.Kategoriler.AsNoTracking()
            .OrderBy(k => k.Tur).ThenBy(k => k.Ad)
            .Select(k => new Kategori(k.Id, k.Ad, k.Tur, k.AktifMi))
            .ToListAsync(cancellationToken));
    }

    private static async Task<IResult> EkleAsync(KategoriKaydetIstegi istek, KasaBankaDbContext db, CancellationToken cancellationToken)
    {
        var ad = istek.Ad.Trim();
        if (await db.Kategoriler.AnyAsync(k => k.Tur == istek.Tur && k.Ad == ad, cancellationToken))
        {
            return KasaBankaHatalari.KategoriAdiKullaniliyor.Problem();
        }

        var kategori = GelirGiderKategorisi.Olustur(ad, istek.Tur);
        db.Kategoriler.Add(kategori);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/kasabanka/kategoriler/{kategori.Id}", new Kategori(kategori.Id, kategori.Ad, kategori.Tur, kategori.AktifMi));
    }

    // Tür değiştirilemez; yalnızca ad güncellenir.
    private static async Task<IResult> GuncelleAsync(Guid id, KategoriKaydetIstegi istek, KasaBankaDbContext db, CancellationToken cancellationToken)
    {
        var kategori = await db.Kategoriler.SingleOrDefaultAsync(k => k.Id == id, cancellationToken);
        if (kategori is null)
        {
            return KasaBankaHatalari.KategoriBulunamadi.Problem();
        }

        var ad = istek.Ad.Trim();
        if (await db.Kategoriler.AnyAsync(k => k.Id != id && k.Tur == kategori.Tur && k.Ad == ad, cancellationToken))
        {
            return KasaBankaHatalari.KategoriAdiKullaniliyor.Problem();
        }

        kategori.Guncelle(ad);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(new Kategori(kategori.Id, kategori.Ad, kategori.Tur, kategori.AktifMi));
    }

    private static async Task<IResult> AktiflikAsync(Guid id, bool aktif, KasaBankaDbContext db, CancellationToken cancellationToken)
    {
        var kategori = await db.Kategoriler.SingleOrDefaultAsync(k => k.Id == id, cancellationToken);
        if (kategori is null)
        {
            return KasaBankaHatalari.KategoriBulunamadi.Problem();
        }

        kategori.AktiflikAyarla(aktif);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> SilAsync(Guid id, KasaBankaDbContext db, CancellationToken cancellationToken)
    {
        var kategori = await db.Kategoriler.SingleOrDefaultAsync(k => k.Id == id, cancellationToken);
        if (kategori is null)
        {
            return KasaBankaHatalari.KategoriBulunamadi.Problem();
        }

        if (await db.GelirGiderKayitlari.AnyAsync(g => g.KategoriId == id, cancellationToken))
        {
            return KasaBankaHatalari.KullanilanKategoriSilinemez.Problem();
        }

        db.Kategoriler.Remove(kategori);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
