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
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.KasaBanka.Features;

internal static class KrediIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var krediler = grup.MapGroup("/krediler").Yetki(KasaBankaYetkileri.Kredi);
        krediler.MapGet("", ListeleAsync);
        krediler.MapGet("/{id:guid}", GetirAsync);
        krediler.MapPost("", OlusturAsync).Dogrula<KrediKaydetIstegi>();
        krediler.MapPost("/{id:guid}/taksitler/{sira:int}/ode", TaksitOdeAsync);
        krediler.MapPost("/{id:guid}/taksitler/{sira:int}/geri-al", TaksitGeriAlAsync).Islem(YetkiIslemleri.Iptal);
        krediler.MapPost("/{id:guid}/iptal", IptalAsync).Islem(YetkiIslemleri.Iptal);
    }

    internal sealed class Dogrulayici : AbstractValidator<KrediKaydetIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.Ad).NotEmpty().MaximumLength(150);
            RuleFor(x => x.Tur).IsInEnum();
            RuleFor(x => x.Tutar).GreaterThan(0).LessThan(1_000_000_000_000m);
            RuleFor(x => x.AylikFaizOrani).InclusiveBetween(0, 100);
            RuleFor(x => x.KullandirimMasrafi).GreaterThanOrEqualTo(0).LessThan(x => x.Tutar);
            RuleFor(x => x.Aciklama).MaximumLength(1000);
            RuleFor(x => x.Taksitler).NotEmpty().Must(t => t.Count <= 360).WithMessage("En fazla 360 taksit olabilir.");
        }
    }

    private static async Task<IResult> ListeleAsync(KasaBankaDbContext db, KrediDurumu? durum, CancellationToken cancellationToken)
    {
        var sorgu = db.Krediler.AsNoTracking().Include(k => k.Taksitler).AsQueryable();
        if (durum is not null)
        {
            sorgu = sorgu.Where(k => k.Durum == durum);
        }

        var krediler = await sorgu.OrderBy(k => k.Durum).ThenByDescending(k => k.KullandirimTarihi).ToListAsync(cancellationToken);
        var hesaplar = await HesapAdlariAsync(db, krediler.Select(k => k.HesapId), cancellationToken);
        return TypedResults.Ok(krediler.Select(k =>
        {
            var sonraki = k.Taksitler.Where(t => !t.OdendiMi).OrderBy(t => t.Sira).FirstOrDefault();
            return new KrediOzeti(k.Id, k.Kod, k.Ad, k.Tur, k.Durum, hesaplar.GetValueOrDefault(k.HesapId, ""), k.ParaBirimi, k.KullandirimTarihi,
                k.Tutar, k.KalanAnapara, k.Taksitler.Count(t => t.OdendiMi), k.Taksitler.Count, sonraki?.Vade, sonraki?.Toplam);
        }).ToList());
    }

    private static async Task<IResult> GetirAsync(Guid id, KasaBankaDbContext db, CancellationToken cancellationToken) =>
        await db.Krediler.AsNoTracking().Include(k => k.Taksitler).SingleOrDefaultAsync(k => k.Id == id, cancellationToken) is { } kredi
            ? TypedResults.Ok(await DetayAsync(db, kredi, cancellationToken))
            : KasaBankaHatalari.KrediBulunamadi.Problem();

    private static async Task<IResult> OlusturAsync(KrediKaydetIstegi istek, KrediServisi servis, KasaBankaDbContext db, CancellationToken cancellationToken)
    {
        var sonuc = await servis.OlusturAsync(istek, cancellationToken);
        return sonuc.BasariliMi
            ? TypedResults.Created($"/api/kasabanka/krediler/{sonuc.Deger.Id}", await DetayAsync(db, sonuc.Deger, cancellationToken))
            : sonuc.HttpSonucu();
    }

    private static async Task<IResult> TaksitOdeAsync(
        Guid id, int sira, TaksitOdemeIstegi istek, KrediServisi servis, KasaBankaDbContext db, CancellationToken cancellationToken) =>
        await SonucAsync(await servis.TaksitOdeAsync(id, sira, istek, cancellationToken), db, cancellationToken);

    private static async Task<IResult> TaksitGeriAlAsync(Guid id, int sira, KrediServisi servis, KasaBankaDbContext db, CancellationToken cancellationToken) =>
        await SonucAsync(await servis.TaksitGeriAlAsync(id, sira, cancellationToken), db, cancellationToken);

    private static async Task<IResult> IptalAsync(Guid id, KrediServisi servis, KasaBankaDbContext db, CancellationToken cancellationToken) =>
        await SonucAsync(await servis.IptalAsync(id, cancellationToken), db, cancellationToken);

    private static async Task<IResult> SonucAsync(Sonuc<Kredi> sonuc, KasaBankaDbContext db, CancellationToken cancellationToken) =>
        sonuc.BasariliMi ? TypedResults.Ok(await DetayAsync(db, sonuc.Deger, cancellationToken)) : sonuc.HttpSonucu();

    private static async Task<KrediDetayi> DetayAsync(KasaBankaDbContext db, Kredi k, CancellationToken cancellationToken)
    {
        var hesaplar = await HesapAdlariAsync(db, [k.HesapId], cancellationToken);
        var taksitler = k.Taksitler.OrderBy(t => t.Sira).ToList();
        return new KrediDetayi(k.Id, k.Kod, k.Ad, k.Tur, k.Durum, k.HesapId, hesaplar.GetValueOrDefault(k.HesapId, ""), k.ParaBirimi, k.KullandirimTarihi,
            k.Tutar, k.AylikFaizOrani, k.KullandirimMasrafi, k.Aciklama, k.KalanAnapara, taksitler.Sum(t => t.Faiz), taksitler.Sum(t => t.Vergi),
            taksitler.Where(t => t.OdendiMi).Sum(t => t.Toplam),
            taksitler.Select(t => new KrediTaksitiDetayi(t.Sira, t.Vade, t.Anapara, t.Faiz, t.Vergi, t.Toplam, t.OdemeTarihi, t.OdemeIslemId)).ToList(),
            k.Surum);
    }

    private static Task<Dictionary<Guid, string>> HesapAdlariAsync(KasaBankaDbContext db, IEnumerable<Guid> idler, CancellationToken cancellationToken)
    {
        var liste = idler.Distinct().ToList();
        return db.Hesaplar.AsNoTracking().IgnoreQueryFilters([ModulDbContext.SilinmeFiltresi]).Where(h => liste.Contains(h.Id))
            .ToDictionaryAsync(h => h.Id, h => h.Ad, cancellationToken);
    }
}
