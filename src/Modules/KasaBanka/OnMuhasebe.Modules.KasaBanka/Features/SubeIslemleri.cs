using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.KasaBanka.Domain;
using OnMuhasebe.Modules.KasaBanka.Infrastructure;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.KasaBanka.Features;

// Bankaya bağlı şube kartları. Banka hesabı açarken şube seçilir/eklenir; portföy yöneticisi ve iletişim bilgileri şubededir.
internal static class SubeIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var subeler = grup.MapGroup("/subeler").Yetki(KasaBankaYetkileri.Tanim);
        subeler.MapGet("", ListeleAsync);
        subeler.MapGet("/{id:guid}", DetayAsync);
        subeler.MapPost("", EkleAsync).Dogrula<SubeKaydetIstegi>();
        subeler.MapPut("/{id:guid}", GuncelleAsync).Dogrula<SubeKaydetIstegi>();
        subeler.MapDelete("/{id:guid}", SilAsync);
    }

    internal sealed class Dogrulayici : AbstractValidator<SubeKaydetIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.BankaAdi).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Kod).MaximumLength(10);
            RuleFor(x => x.Ad).NotEmpty().MaximumLength(150);
            RuleFor(x => x.PortfoyYoneticisi).MaximumLength(150);
            RuleFor(x => x.Telefon).MaximumLength(30);
            RuleFor(x => x.Faks).MaximumLength(30);
        }
    }

    private static async Task<IResult> ListeleAsync(KasaBankaDbContext db, string? banka, string? arama, CancellationToken cancellationToken)
    {
        var sorgu = db.Subeler.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(banka))
        {
            sorgu = sorgu.Where(s => s.BankaAdi == banka.Trim());
        }

        if (!string.IsNullOrWhiteSpace(arama))
        {
            var desen = AramaDeseni.Icerir(arama);
            sorgu = sorgu.Where(s => EF.Functions.ILike(s.Ad, desen) || (s.PortfoyYoneticisi != null && EF.Functions.ILike(s.PortfoyYoneticisi, desen)));
        }

        var kayitlar = await sorgu
            .OrderBy(s => s.BankaAdi).ThenBy(s => s.Ad)
            .Select(s => new SubeOzeti(s.Id, s.BankaAdi, s.Kod, s.Ad, s.PortfoyYoneticisi, s.Telefon, s.Faks))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(kayitlar);
    }

    private static async Task<IResult> DetayAsync(Guid id, KasaBankaDbContext db, CancellationToken cancellationToken) =>
        await db.Subeler.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new SubeDetayi(s.Id, s.BankaAdi, s.Kod, s.Ad, s.PortfoyYoneticisi, s.Telefon, s.Faks, s.Surum))
            .SingleOrDefaultAsync(cancellationToken) is { } detay
            ? TypedResults.Ok(detay)
            : KasaBankaHatalari.SubeBulunamadi.Problem();

    private static async Task<IResult> EkleAsync(SubeKaydetIstegi istek, KasaBankaDbContext db, CancellationToken cancellationToken)
    {
        var sube = Sube.Olustur(istek);
        db.Subeler.Add(sube);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/kasabanka/subeler/{sube.Id}", sube.Id);
    }

    private static async Task<IResult> GuncelleAsync(Guid id, SubeKaydetIstegi istek, KasaBankaDbContext db, CancellationToken cancellationToken)
    {
        var sube = await db.Subeler.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (sube is null)
        {
            return KasaBankaHatalari.SubeBulunamadi.Problem();
        }

        db.Entry(sube).Property(s => s.Surum).OriginalValue = istek.Surum;
        sube.Guncelle(istek);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> SilAsync(Guid id, KasaBankaDbContext db, CancellationToken cancellationToken)
    {
        var sube = await db.Subeler.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (sube is null)
        {
            return KasaBankaHatalari.SubeBulunamadi.Problem();
        }

        if (await db.Hesaplar.IgnoreQueryFilters([ModulDbContext.SilinmeFiltresi]).AnyAsync(h => h.SubeId == id, cancellationToken))
        {
            return KasaBankaHatalari.SubeKullaniliyor.Problem();
        }

        db.Subeler.Remove(sube);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
