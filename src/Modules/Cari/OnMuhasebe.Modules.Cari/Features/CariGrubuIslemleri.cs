using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.Cari.Domain;
using OnMuhasebe.Modules.Cari.Infrastructure;
using OnMuhasebe.Shared.Contracts.Tanimlar;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Cari.Features;

internal static class CariGrubuIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var gruplar = grup.MapGroup("/gruplar");
        gruplar.MapGet("", ListeleAsync);
        gruplar.MapPost("", EkleAsync).Dogrula<GrupKaydetIstegi>();
        gruplar.MapPut("/{id:guid}", GuncelleAsync).Dogrula<GrupKaydetIstegi>();
        gruplar.MapDelete("/{id:guid}", SilAsync);
    }

    internal sealed class Dogrulayici : AbstractValidator<GrupKaydetIstegi>
    {
        public Dogrulayici() => RuleFor(x => x.Ad).NotEmpty().MaximumLength(100);
    }

    private static async Task<IResult> ListeleAsync(CariDbContext db, CancellationToken cancellationToken) =>
        TypedResults.Ok(await db.CariGruplari.AsNoTracking()
            .OrderBy(g => g.Ad)
            .Select(g => new GrupOzeti(g.Id, g.Ad, db.CariHesaplar.Count(c => c.GrupId == g.Id)))
            .ToListAsync(cancellationToken));

    private static async Task<IResult> EkleAsync(GrupKaydetIstegi istek, CariDbContext db, CancellationToken cancellationToken)
    {
        if (await AdKullanimdaMiAsync(db, istek.Ad, null, cancellationToken))
        {
            return CariHatalari.GrupAdiKullaniliyor.Problem();
        }

        var grup = CariGrup.Olustur(istek.Ad);
        db.CariGruplari.Add(grup);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/cari/gruplar/{grup.Id}", new GrupOzeti(grup.Id, grup.Ad, 0));
    }

    private static async Task<IResult> GuncelleAsync(
        Guid id, GrupKaydetIstegi istek, CariDbContext db, CancellationToken cancellationToken)
    {
        var grup = await db.CariGruplari.SingleOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (grup is null)
        {
            return CariHatalari.GrupBulunamadi.Problem();
        }

        if (await AdKullanimdaMiAsync(db, istek.Ad, id, cancellationToken))
        {
            return CariHatalari.GrupAdiKullaniliyor.Problem();
        }

        grup.AdDegistir(istek.Ad);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> SilAsync(Guid id, CariDbContext db, CancellationToken cancellationToken)
    {
        var grup = await db.CariGruplari.SingleOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (grup is null)
        {
            return CariHatalari.GrupBulunamadi.Problem();
        }

        // Silinmiş cariler de grubu referans ettiği için onlar da sayılır.
        if (await db.CariHesaplar.IgnoreQueryFilters([ModulDbContext.SilinmeFiltresi])
                .AnyAsync(c => c.GrupId == id, cancellationToken))
        {
            return CariHatalari.GrupKullaniliyor.Problem();
        }

        db.CariGruplari.Remove(grup);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static Task<bool> AdKullanimdaMiAsync(CariDbContext db, string ad, Guid? haricId, CancellationToken cancellationToken)
    {
        var temizAd = ad.Trim();
        return db.CariGruplari.AnyAsync(g => g.Ad == temizAd && g.Id != haricId, cancellationToken);
    }
}
