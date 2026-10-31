using System.Globalization;
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
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.KasaBanka.Features;

// Demirbaş (sabit kıymet) kartları, elden çıkarma ve yıllık amortisman listesi.
internal static class DemirbasIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var g = grup.MapGroup("/demirbaslar").Yetki(KasaBankaYetkileri.Demirbas);
        g.MapGet("", ListeAsync);
        g.MapGet("/{id:guid}", GetirAsync);
        g.MapPost("", OlusturAsync).Dogrula<DemirbasKaydetIstegi>();
        g.MapPut("/{id:guid}", GuncelleAsync).Dogrula<DemirbasKaydetIstegi>();
        g.MapPost("/{id:guid}/elden-cikar", ElCikarAsync).Dogrula<ElCikarmaIstegi>().Islem(YetkiIslemleri.Duzenle);
        g.MapPost("/{id:guid}/elden-cikarma-geri-al", (Guid id, KasaBankaDbContext db, CancellationToken ct) =>
            IslemAsync(id, db, d => d.ElCikarmayiGeriAl(), ct)).Islem(YetkiIslemleri.Duzenle);
        g.MapDelete("/{id:guid}", SilAsync);
    }

    internal sealed class Dogrulayici : AbstractValidator<DemirbasKaydetIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.SicilNo).MaximumLength(30);
            RuleFor(x => x.Ad).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Grup).IsInEnum();
            RuleFor(x => x.Yontem).IsInEnum();
            RuleFor(x => x.EdinmeBedeli).GreaterThan(0).LessThan(1_000_000_000_000m);
            RuleFor(x => x.FaydaliOmur).InclusiveBetween(1, 50);
            RuleFor(x => x.BelgeNo).MaximumLength(50);
            RuleFor(x => x.Zimmetli).MaximumLength(100);
            RuleFor(x => x.Konum).MaximumLength(100);
            RuleFor(x => x.Aciklama).MaximumLength(500);
        }
    }

    internal sealed class ElCikarmaDogrulayici : AbstractValidator<ElCikarmaIstegi>
    {
        public ElCikarmaDogrulayici()
        {
            RuleFor(x => x.Tur).IsInEnum();
            RuleFor(x => x.Bedel).GreaterThanOrEqualTo(0).LessThan(1_000_000_000_000m);
            RuleFor(x => x.Aciklama).MaximumLength(500);
        }
    }

    // Yıl verilmezse içinde bulunulan yıl. Elden çıkarılanlar, çıkarıldıkları yıldan sonra listede yer almaz.
    private static async Task<IResult> ListeAsync(int? yil, bool? elCikarilanlar, string? arama, KasaBankaDbContext db, TimeProvider zaman,
        CancellationToken cancellationToken)
    {
        var secilenYil = yil ?? TurkiyeSaati.Bugun(zaman).Year;
        var sorgu = db.Demirbaslar.AsNoTracking().Where(d => d.EdinmeTarihi.Year <= secilenYil);
        sorgu = elCikarilanlar == true
            ? sorgu.Where(d => d.ElCikarmaTarihi != null)
            : sorgu.Where(d => d.ElCikarmaTarihi == null || d.ElCikarmaTarihi.Value.Year >= secilenYil);
        if (!string.IsNullOrWhiteSpace(arama))
        {
            var desen = AramaDeseni.Icerir(arama);
            sorgu = sorgu.Where(d => EF.Functions.ILike(d.Ad, desen) || EF.Functions.ILike(d.SicilNo, desen)
                                     || (d.Zimmetli != null && EF.Functions.ILike(d.Zimmetli, desen)));
        }

        var kalemler = (await sorgu.OrderBy(d => d.SicilNo).ToListAsync(cancellationToken)).Select(d => Ozet(d, secilenYil)).ToList();
        return TypedResults.Ok(new AmortismanListesi(secilenYil, kalemler, kalemler.Sum(k => k.EdinmeBedeli), kalemler.Sum(k => k.YilAmortismani),
            kalemler.Sum(k => k.Birikmis), kalemler.Sum(k => k.NetDeger)));
    }

    private static async Task<IResult> GetirAsync(Guid id, KasaBankaDbContext db, CancellationToken cancellationToken) =>
        await db.Demirbaslar.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id, cancellationToken) is { } d
            ? TypedResults.Ok(Detay(d))
            : DemirbasHatalari.Bulunamadi.Problem();

    private static async Task<IResult> OlusturAsync(
        DemirbasKaydetIstegi istek, KasaBankaDbContext db, IsyeriSecici isyeriSecici, CancellationToken cancellationToken)
    {
        var sicil = string.IsNullOrWhiteSpace(istek.SicilNo) ? await SonrakiSicilAsync(db, istek.EdinmeTarihi.Year, cancellationToken) : istek.SicilNo.Trim();
        if (await db.Demirbaslar.IgnoreQueryFilters([ModulDbContext.IsyeriFiltresi]).AnyAsync(d => d.SicilNo == sicil, cancellationToken))
        {
            return DemirbasHatalari.SicilKullaniliyor.Problem();
        }

        if (await MerkezHatasiAsync(db, istek.MasrafMerkeziId, cancellationToken) is { } hata)
        {
            return hata;
        }

        var isyeri = await isyeriSecici.BelirleAsync(istek.IsyeriId, cancellationToken);
        if (!isyeri.BasariliMi)
        {
            return isyeri.Hata.Problem();
        }

        var sonuc = Demirbas.Olustur(sicil, istek, isyeri.Deger);
        if (!sonuc.BasariliMi)
        {
            return sonuc.Hata.Problem();
        }

        db.Demirbaslar.Add(sonuc.Deger);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/kasabanka/demirbaslar/{sonuc.Deger.Id}", Detay(sonuc.Deger));
    }

    private static async Task<IResult> GuncelleAsync(
        Guid id, DemirbasKaydetIstegi istek, KasaBankaDbContext db, IsyeriSecici isyeriSecici, CancellationToken cancellationToken)
    {
        var demirbas = await db.Demirbaslar.SingleOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (demirbas is null)
        {
            return DemirbasHatalari.Bulunamadi.Problem();
        }

        db.Entry(demirbas).Property(d => d.Surum).OriginalValue = istek.Surum;
        var sicil = string.IsNullOrWhiteSpace(istek.SicilNo) ? demirbas.SicilNo : istek.SicilNo.Trim();
        if (await db.Demirbaslar.IgnoreQueryFilters([ModulDbContext.IsyeriFiltresi]).AnyAsync(d => d.Id != id && d.SicilNo == sicil, cancellationToken))
        {
            return DemirbasHatalari.SicilKullaniliyor.Problem();
        }

        if (istek.MasrafMerkeziId != demirbas.MasrafMerkeziId && await MerkezHatasiAsync(db, istek.MasrafMerkeziId, cancellationToken) is { } hata)
        {
            return hata;
        }

        var isyeri = await isyeriSecici.DegisiklikAsync(demirbas.IsyeriId, istek.IsyeriId, cancellationToken);
        if (!isyeri.BasariliMi)
        {
            return isyeri.Hata.Problem();
        }

        var sonuc = demirbas.Guncelle(sicil, istek);
        if (!sonuc.BasariliMi)
        {
            return sonuc.Hata.Problem();
        }

        demirbas.IsyeriDegistir(isyeri.Deger);

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(Detay(demirbas));
    }

    private static Task<IResult> ElCikarAsync(Guid id, ElCikarmaIstegi istek, KasaBankaDbContext db, CancellationToken cancellationToken) =>
        IslemAsync(id, db, d => d.ElCikar(istek), cancellationToken);

    private static async Task<IResult> IslemAsync(Guid id, KasaBankaDbContext db, Func<Demirbas, Shared.Domain.Results.Sonuc> islem, CancellationToken cancellationToken)
    {
        var demirbas = await db.Demirbaslar.SingleOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (demirbas is null)
        {
            return DemirbasHatalari.Bulunamadi.Problem();
        }

        var sonuc = islem(demirbas);
        if (!sonuc.BasariliMi)
        {
            return sonuc.Hata.Problem();
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(Detay(demirbas));
    }

    private static async Task<IResult> SilAsync(Guid id, KasaBankaDbContext db, CancellationToken cancellationToken)
    {
        var demirbas = await db.Demirbaslar.SingleOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (demirbas is null)
        {
            return DemirbasHatalari.Bulunamadi.Problem();
        }

        db.Demirbaslar.Remove(demirbas);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult?> MerkezHatasiAsync(KasaBankaDbContext db, Guid? merkezId, CancellationToken cancellationToken) =>
        merkezId is { } id && !await db.MasrafMerkezleri.AnyAsync(m => m.Id == id && m.AktifMi, cancellationToken)
            ? KasaBankaHatalari.MasrafMerkeziGecersiz.Problem()
            : null;

    // Sicil: DMB + edinme yılı + 4 haneli sıra (ör. DMB20260007).
    private static async Task<string> SonrakiSicilAsync(KasaBankaDbContext db, int yil, CancellationToken cancellationToken)
    {
        var onek = $"DMB{yil.ToString(CultureInfo.InvariantCulture)}";
        var sonuncu = await db.Demirbaslar.IgnoreQueryFilters([ModulDbContext.IsyeriFiltresi]).Where(d => d.SicilNo.StartsWith(onek) && d.SicilNo.Length == onek.Length + 4)
            .MaxAsync(d => (string?)d.SicilNo, cancellationToken);
        var sira = sonuncu is not null && int.TryParse(sonuncu[onek.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var s) ? s + 1 : 1;
        return $"{onek}{sira.ToString("D4", CultureInfo.InvariantCulture)}";
    }

    private static DemirbasOzeti Ozet(Demirbas d, int yil)
    {
        var (yilTutari, birikmis) = AmortismanHesaplayici.Yil(d.Plan(), yil);
        return new DemirbasOzeti(d.Id, d.SicilNo, d.Ad, d.Grup, d.EdinmeTarihi, d.EdinmeBedeli, d.FaydaliOmur, d.Yontem, d.Zimmetli, d.ElCikarmaTarihi,
            yilTutari, birikmis, d.EdinmeBedeli - birikmis, d.IsyeriId);
    }

    private static DemirbasDetayi Detay(Demirbas d)
    {
        var plan = d.Plan();
        decimal? karZarar = d.ElCikarmaBedeli is { } bedel
            ? AmortismanHesaplayici.ElCikarmaSonucu(bedel, d.EdinmeBedeli, plan.Sum(p => p.Tutar))
            : null;
        return new DemirbasDetayi(d.Id, d.SicilNo, d.Ad, d.Grup, d.BinekOtomobilMi, d.EdinmeTarihi, d.EdinmeBedeli, d.FaydaliOmur, d.Yontem,
            d.BelgeNo, d.Zimmetli, d.Konum, d.MasrafMerkeziId, d.Aciklama, d.ElCikarmaTuru, d.ElCikarmaTarihi, d.ElCikarmaBedeli,
            d.ElCikarmaAciklamasi, karZarar, plan, d.Surum, d.IsyeriId);
    }
}
