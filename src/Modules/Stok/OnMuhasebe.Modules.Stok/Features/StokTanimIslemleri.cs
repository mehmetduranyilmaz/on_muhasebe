using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Modules.Stok.Domain;
using OnMuhasebe.Modules.Stok.Infrastructure;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;
using StokGrubuDto = OnMuhasebe.Modules.Stok.Contracts.StokGrubu;
using DepoDto = OnMuhasebe.Modules.Stok.Contracts.Depo;

namespace OnMuhasebe.Modules.Stok.Features;

// Depo, stok grubu ve firma stok ayarları.
internal static class StokTanimIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var depolar = grup.MapGroup("/depolar");
        depolar.MapGet("", DepolariListeleAsync);
        depolar.MapPost("", DepoEkleAsync).Dogrula<DepoKaydetIstegi>();
        depolar.MapPut("/{id:guid}", DepoGuncelleAsync).Dogrula<DepoKaydetIstegi>();
        depolar.MapPost("/{id:guid}/varsayilan", VarsayilanYapAsync);
        depolar.MapDelete("/{id:guid}", DepoSilAsync);

        var gruplar = grup.MapGroup("/gruplar");
        gruplar.MapGet("", GruplariListeleAsync);
        gruplar.MapPost("", GrupEkleAsync).Dogrula<StokGrubuKaydetIstegi>();
        gruplar.MapPut("/{id:guid}", GrupGuncelleAsync).Dogrula<StokGrubuKaydetIstegi>();
        gruplar.MapDelete("/{id:guid}", GrupSilAsync);

        grup.MapGet("/ayarlar", AyarlariGetirAsync);
        grup.MapPut("/ayarlar", AyarlariKaydetAsync).RequireAuthorization(p => p.RequireRole(Roller.Yonetici));
    }

    internal sealed class DepoDogrulayici : AbstractValidator<DepoKaydetIstegi>
    {
        public DepoDogrulayici() => RuleFor(x => x.Ad).NotEmpty().MaximumLength(100);
    }

    internal sealed class GrupDogrulayici : AbstractValidator<StokGrubuKaydetIstegi>
    {
        public GrupDogrulayici() => RuleFor(x => x.Ad).NotEmpty().MaximumLength(100);
    }

    private static async Task<IResult> DepolariListeleAsync(StokDbContext db, DepoServisi depoServisi, CancellationToken cancellationToken)
    {
        await depoServisi.VarsayilanAsync(cancellationToken);
        return TypedResults.Ok(await db.Depolar.AsNoTracking()
            .OrderByDescending(d => d.VarsayilanMi).ThenBy(d => d.Ad)
            .Select(d => new DepoDto(d.Id, d.Ad, d.VarsayilanMi, d.AktifMi))
            .ToListAsync(cancellationToken));
    }

    private static async Task<IResult> DepoEkleAsync(DepoKaydetIstegi istek, StokDbContext db, DepoServisi depoServisi, CancellationToken cancellationToken)
    {
        await depoServisi.VarsayilanAsync(cancellationToken);
        var ad = istek.Ad.Trim();
        if (await db.Depolar.AnyAsync(d => d.Ad == ad, cancellationToken))
        {
            return StokHatalari.AdKullaniliyor.Problem();
        }

        var depo = Domain.Depo.Olustur(ad);
        db.Depolar.Add(depo);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/stok/depolar/{depo.Id}", new DepoDto(depo.Id, depo.Ad, depo.VarsayilanMi, depo.AktifMi));
    }

    private static async Task<IResult> DepoGuncelleAsync(Guid id, DepoKaydetIstegi istek, StokDbContext db, CancellationToken cancellationToken)
    {
        var depo = await db.Depolar.SingleOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (depo is null)
        {
            return StokHatalari.DepoBulunamadi.Problem();
        }

        var ad = istek.Ad.Trim();
        if (await db.Depolar.AnyAsync(d => d.Ad == ad && d.Id != id, cancellationToken))
        {
            return StokHatalari.AdKullaniliyor.Problem();
        }

        depo.AdDegistir(ad);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> VarsayilanYapAsync(Guid id, StokDbContext db, CancellationToken cancellationToken)
    {
        var depolar = await db.Depolar.ToListAsync(cancellationToken);
        if (depolar.Find(d => d.Id == id) is null)
        {
            return StokHatalari.DepoBulunamadi.Problem();
        }

        depolar.ForEach(d => d.VarsayilanYap(d.Id == id));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> DepoSilAsync(Guid id, StokDbContext db, CancellationToken cancellationToken)
    {
        var depo = await db.Depolar.SingleOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (depo is null)
        {
            return StokHatalari.DepoBulunamadi.Problem();
        }

        if (depo.VarsayilanMi)
        {
            return StokHatalari.VarsayilanDepoSilinemez.Problem();
        }

        if (await db.StokHareketleri.AnyAsync(h => h.DepoId == id, cancellationToken))
        {
            return StokHatalari.HareketliDepoSilinemez.Problem();
        }

        db.Depolar.Remove(depo);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> GruplariListeleAsync(StokDbContext db, CancellationToken cancellationToken) =>
        TypedResults.Ok(await db.StokGruplari.AsNoTracking()
            .OrderBy(g => g.Ad)
            .Select(g => new StokGrubuDto(g.Id, g.Ad, db.StokKartlari.Count(k => k.GrupId == g.Id)))
            .ToListAsync(cancellationToken));

    private static async Task<IResult> GrupEkleAsync(StokGrubuKaydetIstegi istek, StokDbContext db, CancellationToken cancellationToken)
    {
        var ad = istek.Ad.Trim();
        if (await db.StokGruplari.AnyAsync(g => g.Ad == ad, cancellationToken))
        {
            return StokHatalari.AdKullaniliyor.Problem();
        }

        var grup = Domain.StokGrubu.Olustur(ad);
        db.StokGruplari.Add(grup);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/stok/gruplar/{grup.Id}", new StokGrubuDto(grup.Id, grup.Ad, 0));
    }

    private static async Task<IResult> GrupGuncelleAsync(Guid id, StokGrubuKaydetIstegi istek, StokDbContext db, CancellationToken cancellationToken)
    {
        var grup = await db.StokGruplari.SingleOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (grup is null)
        {
            return StokHatalari.GrupBulunamadi.Problem();
        }

        var ad = istek.Ad.Trim();
        if (await db.StokGruplari.AnyAsync(g => g.Ad == ad && g.Id != id, cancellationToken))
        {
            return StokHatalari.AdKullaniliyor.Problem();
        }

        grup.AdDegistir(ad);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> GrupSilAsync(Guid id, StokDbContext db, CancellationToken cancellationToken)
    {
        var grup = await db.StokGruplari.SingleOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (grup is null)
        {
            return StokHatalari.GrupBulunamadi.Problem();
        }

        if (await db.StokKartlari.IgnoreQueryFilters([ModulDbContext.SilinmeFiltresi]).AnyAsync(k => k.GrupId == id, cancellationToken))
        {
            return StokHatalari.GrupKullaniliyor.Problem();
        }

        db.StokGruplari.Remove(grup);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> AyarlariGetirAsync(StokAyarServisi ayarServisi, CancellationToken cancellationToken)
    {
        var ayar = await ayarServisi.GetirAsync(cancellationToken);
        return TypedResults.Ok(new StokAyarlari(ayar.MaliyetYontemi, ayar.EksiStokDavranisi));
    }

    private static async Task<IResult> AyarlariKaydetAsync(StokAyarlari istek, StokDbContext db, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(istek.MaliyetYontemi) || !Enum.IsDefined(istek.EksiStokDavranisi))
        {
            return Shared.Domain.Results.Hata.Dogrulama("ayar_gecersiz", "Geçersiz ayar değeri.").Problem();
        }

        var ayar = await db.StokAyarlari.SingleOrDefaultAsync(cancellationToken);
        if (ayar is null)
        {
            ayar = StokAyari.Varsayilan();
            db.StokAyarlari.Add(ayar);
        }

        ayar.Guncelle(istek.MaliyetYontemi, istek.EksiStokDavranisi);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(new StokAyarlari(ayar.MaliyetYontemi, ayar.EksiStokDavranisi));
    }
}
