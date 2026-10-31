using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.KasaBanka.Domain;
using OnMuhasebe.Modules.KasaBanka.Infrastructure;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.KasaBanka.Features;

// Masraf merkezleri firma tanımıdır. Faturalar (başka modül) da merkeze bağlandığı için silinmez, pasifleştirilir.
internal static class MasrafMerkeziIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var g = grup.MapGroup("/masraf-merkezleri");
        g.MapGet("", ListeleAsync);
        g.MapPost("", EkleAsync).Dogrula<MasrafMerkeziKaydetIstegi>();
        g.MapPut("/{id:guid}", GuncelleAsync).Dogrula<MasrafMerkeziKaydetIstegi>();
        g.MapPost("/{id:guid}/pasiflestir", (Guid id, KasaBankaDbContext db, CancellationToken ct) => AktiflikAsync(id, false, db, ct));
        g.MapPost("/{id:guid}/aktiflestir", (Guid id, KasaBankaDbContext db, CancellationToken ct) => AktiflikAsync(id, true, db, ct));

        grup.MapGet("/raporlar/masraf-merkezi", TutarlarAsync);
    }

    internal sealed class Dogrulayici : AbstractValidator<MasrafMerkeziKaydetIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.Kod).NotEmpty().MaximumLength(20);
            RuleFor(x => x.Ad).NotEmpty().MaximumLength(100);
        }
    }

    private static async Task<IResult> ListeleAsync(KasaBankaDbContext db, CancellationToken cancellationToken) =>
        TypedResults.Ok(await db.MasrafMerkezleri.AsNoTracking()
            .OrderByDescending(m => m.AktifMi).ThenBy(m => m.Kod)
            .Select(m => new MasrafMerkeziOzeti(m.Id, m.Kod, m.Ad, m.AktifMi))
            .ToListAsync(cancellationToken));

    private static async Task<IResult> EkleAsync(MasrafMerkeziKaydetIstegi istek, KasaBankaDbContext db, CancellationToken cancellationToken)
    {
        var merkez = MasrafMerkezi.Olustur(istek.Kod, istek.Ad);
        if (await db.MasrafMerkezleri.AnyAsync(m => m.Kod == merkez.Kod, cancellationToken))
        {
            return KasaBankaHatalari.MasrafMerkeziKoduKullaniliyor.Problem();
        }

        db.MasrafMerkezleri.Add(merkez);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/kasabanka/masraf-merkezleri/{merkez.Id}", new MasrafMerkeziOzeti(merkez.Id, merkez.Kod, merkez.Ad, merkez.AktifMi));
    }

    private static async Task<IResult> GuncelleAsync(Guid id, MasrafMerkeziKaydetIstegi istek, KasaBankaDbContext db, CancellationToken cancellationToken)
    {
        var merkez = await db.MasrafMerkezleri.SingleOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (merkez is null)
        {
            return KasaBankaHatalari.MasrafMerkeziBulunamadi.Problem();
        }

        merkez.Guncelle(istek.Kod, istek.Ad);
        if (await db.MasrafMerkezleri.AnyAsync(m => m.Id != id && m.Kod == merkez.Kod, cancellationToken))
        {
            return KasaBankaHatalari.MasrafMerkeziKoduKullaniliyor.Problem();
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(new MasrafMerkeziOzeti(merkez.Id, merkez.Kod, merkez.Ad, merkez.AktifMi));
    }

    private static async Task<IResult> AktiflikAsync(Guid id, bool aktif, KasaBankaDbContext db, CancellationToken cancellationToken)
    {
        var merkez = await db.MasrafMerkezleri.SingleOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (merkez is null)
        {
            return KasaBankaHatalari.MasrafMerkeziBulunamadi.Problem();
        }

        merkez.AktiflikAyarla(aktif);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    // Kasa/banka gelir-gider kayıtlarının merkez bazında TL toplamı (ödendi + tahakkuk, iptaller hariç). Fatura tarafı Fatura modülündedir;
    // ekran ikisini birleştirir.
    private static async Task<IResult> TutarlarAsync(
        DateOnly? baslangic, DateOnly? bitis, KasaBankaDbContext db, TimeProvider zaman, CancellationToken cancellationToken)
    {
        var bugun = TurkiyeSaati.Bugun(zaman);
        var (bas, son) = (baslangic ?? new DateOnly(bugun.Year, 1, 1), bitis ?? bugun);
        if (son < bas)
        {
            return Hata.Dogrulama("tarih_araligi", "Bitiş tarihi başlangıçtan önce olamaz.").Problem();
        }

        var kalemler = await db.GelirGiderKayitlari.AsNoTracking()
            .Where(g => g.IptalTarihi == null && g.Tarih >= bas && g.Tarih <= son)
            .GroupBy(g => g.MasrafMerkeziId)
            .Select(x => new MerkezTutari(
                x.Key,
                x.Where(g => g.Tur == GelirGiderTuru.Gelir).Sum(g => g.TutarTl),
                x.Where(g => g.Tur == GelirGiderTuru.Gider).Sum(g => g.TutarTl),
                x.Count()))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(new MerkezTutarlari(bas, son, kalemler));
    }
}
