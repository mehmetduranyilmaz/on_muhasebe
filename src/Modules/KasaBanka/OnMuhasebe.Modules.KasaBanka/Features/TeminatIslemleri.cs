using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.KasaBanka.Domain;
using OnMuhasebe.Modules.KasaBanka.Infrastructure;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.KasaBanka.Features;

// Teminat takip defteri: alınan/verilen teminatların kaydı ve durumu. Kasa/muhasebe hareketi oluşturmaz (nazım hesap).
internal static class TeminatIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var teminatlar = grup.MapGroup("/teminatlar");
        teminatlar.MapGet("", ListeleAsync);
        teminatlar.MapGet("/{id:guid}", DetayAsync);
        teminatlar.MapPost("", EkleAsync).Dogrula<TeminatKaydetIstegi>();
        teminatlar.MapPut("/{id:guid}", GuncelleAsync).Dogrula<TeminatKaydetIstegi>();
        teminatlar.MapPost("/{id:guid}/durum", DurumDegistirAsync);
        teminatlar.MapDelete("/{id:guid}", SilAsync);
    }

    internal sealed class Dogrulayici : AbstractValidator<TeminatKaydetIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.Tur).IsInEnum();
            RuleFor(x => x.Yon).IsInEnum();
            RuleFor(x => x.TarafAdi).NotEmpty().MaximumLength(250);
            RuleFor(x => x.ReferansNo).MaximumLength(100);
            RuleFor(x => x.ParaBirimi).Must(ParaBirimleri.GecerliMi).WithMessage("Geçersiz para birimi.");
            RuleFor(x => x.Tutar).GreaterThan(0);
            RuleFor(x => x.VadeTarihi).GreaterThanOrEqualTo(x => x.VerilisTarihi)
                .When(x => x.VadeTarihi is not null).WithMessage("Vade tarihi veriliş tarihinden önce olamaz.");
            RuleFor(x => x.Aciklama).MaximumLength(1000);
        }
    }

    private static async Task<IResult> ListeleAsync(
        KasaBankaDbContext db, TeminatYonu? yon, TeminatDurumu? durum, string? arama, CancellationToken cancellationToken)
    {
        var sorgu = db.Teminatlar.AsNoTracking();
        if (yon is not null)
        {
            sorgu = sorgu.Where(t => t.Yon == yon);
        }

        if (durum is not null)
        {
            sorgu = sorgu.Where(t => t.Durum == durum);
        }

        if (!string.IsNullOrWhiteSpace(arama))
        {
            var desen = AramaDeseni.Icerir(arama);
            sorgu = sorgu.Where(t => EF.Functions.ILike(t.TarafAdi, desen) || (t.ReferansNo != null && EF.Functions.ILike(t.ReferansNo, desen)));
        }

        var kayitlar = await sorgu
            .OrderBy(t => t.Durum == TeminatDurumu.Aktif ? 0 : 1)
            .ThenBy(t => t.VadeTarihi ?? DateOnly.MaxValue)
            .ThenByDescending(t => t.VerilisTarihi)
            .Select(t => new TeminatOzeti(
                t.Id, t.Tur, t.Yon, t.TarafAdi, t.ReferansNo, t.Tutar, t.ParaBirimi, t.VerilisTarihi, t.VadeTarihi, t.Durum))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(kayitlar);
    }

    private static async Task<IResult> DetayAsync(Guid id, KasaBankaDbContext db, CancellationToken cancellationToken) =>
        await db.Teminatlar.AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new TeminatDetayi(
                t.Id, t.Tur, t.Yon, t.TarafAdi, t.ReferansNo, t.Tutar, t.ParaBirimi, t.VerilisTarihi, t.VadeTarihi,
                t.IadeTarihi, t.Durum, t.Aciklama, t.Surum))
            .SingleOrDefaultAsync(cancellationToken) is { } detay
            ? TypedResults.Ok(detay)
            : KasaBankaHatalari.TeminatBulunamadi.Problem();

    private static async Task<IResult> EkleAsync(TeminatKaydetIstegi istek, KasaBankaDbContext db, CancellationToken cancellationToken)
    {
        var teminat = Teminat.Olustur(istek);
        db.Teminatlar.Add(teminat);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/kasabanka/teminatlar/{teminat.Id}", teminat.Id);
    }

    private static async Task<IResult> GuncelleAsync(Guid id, TeminatKaydetIstegi istek, KasaBankaDbContext db, CancellationToken cancellationToken)
    {
        var teminat = await db.Teminatlar.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (teminat is null)
        {
            return KasaBankaHatalari.TeminatBulunamadi.Problem();
        }

        db.Entry(teminat).Property(t => t.Surum).OriginalValue = istek.Surum;
        teminat.Guncelle(istek);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> DurumDegistirAsync(Guid id, TeminatDurumIstegi istek, KasaBankaDbContext db, CancellationToken cancellationToken)
    {
        var teminat = await db.Teminatlar.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (teminat is null)
        {
            return KasaBankaHatalari.TeminatBulunamadi.Problem();
        }

        teminat.DurumDegistir(istek.Durum, istek.IadeTarihi);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> SilAsync(Guid id, KasaBankaDbContext db, CancellationToken cancellationToken)
    {
        var teminat = await db.Teminatlar.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (teminat is null)
        {
            return KasaBankaHatalari.TeminatBulunamadi.Problem();
        }

        db.Teminatlar.Remove(teminat);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
