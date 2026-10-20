using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.Cari.Domain;
using OnMuhasebe.Modules.Cari.Infrastructure;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Cari.Features;

// Açık kalemler (hangi fatura ne kadar ödendi), elle kapama düzeltmesi ve vade analizi.
internal static class CariKapamaIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        grup.MapGet("/hesaplar/{cariId:guid}/acik-kalemler", AcikKalemlerAsync);
        grup.MapGet("/hareketler/{hareketId:guid}/kapamalar", DetayAsync);
        grup.MapPut("/hareketler/{hareketId:guid}/kapamalar", ElleKapatAsync).Dogrula<ElleKapamaIstegi>();
        grup.MapDelete("/hareketler/{hareketId:guid}/kapamalar", OtomatigeDonAsync);
        grup.MapGet("/raporlar/vade-analizi", VadeAnaliziAsync);
    }

    internal sealed class Dogrulayici : AbstractValidator<ElleKapamaIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.Kapamalar).NotNull().Must(k => k.Count <= 500);
            RuleForEach(x => x.Kapamalar).ChildRules(k => k.RuleFor(x => x.Tutar).GreaterThan(0));
        }
    }

    private static async Task<IResult> AcikKalemlerAsync(
        Guid cariId, DateOnly? tarih, CariDbContext db, CariKapamaServisi kapama, TimeProvider zaman, CancellationToken cancellationToken)
    {
        if (!await db.CariHesaplar.AnyAsync(c => c.Id == cariId, cancellationToken))
        {
            return CariHatalari.CariBulunamadi.Problem();
        }

        return TypedResults.Ok(await kapama.AcikKalemlerAsync(cariId, tarih ?? TurkiyeSaati.Bugun(zaman), cancellationToken));
    }

    private static async Task<IResult> DetayAsync(Guid hareketId, CariKapamaServisi kapama, CancellationToken cancellationToken) =>
        await kapama.DetayAsync(hareketId, cancellationToken) is { } detay
            ? TypedResults.Ok(detay)
            : CariHatalari.HareketBulunamadi.Problem();

    private static async Task<IResult> ElleKapatAsync(
        Guid hareketId, ElleKapamaIstegi istek, CariKapamaServisi kapama, CancellationToken cancellationToken)
    {
        var sonuc = await kapama.ElleKapatAsync(hareketId, istek.Kapamalar, cancellationToken);
        return sonuc.BasariliMi ? TypedResults.Ok(await kapama.DetayAsync(hareketId, cancellationToken)) : sonuc.HttpSonucu();
    }

    private static async Task<IResult> OtomatigeDonAsync(Guid hareketId, CariKapamaServisi kapama, CancellationToken cancellationToken)
    {
        var sonuc = await kapama.OtomatigeDonAsync(hareketId, cancellationToken);
        return sonuc.BasariliMi ? TypedResults.Ok(await kapama.DetayAsync(hareketId, cancellationToken)) : sonuc.HttpSonucu();
    }

    // Alacak: carinin bize borçlu olduğu açık kalemler (satış faturası...); Borc: bizim cariye borcumuz (alış faturası, avans...).
    private static async Task<IResult> VadeAnaliziAsync(
        DateOnly? tarih, CariDbContext db, CariKapamaServisi kapama, TimeProvider zaman, CancellationToken cancellationToken)
    {
        var raporTarihi = tarih ?? TurkiyeSaati.Bugun(zaman);
        var kalemler = await kapama.TumAcikKalemlerAsync(raporTarihi, cancellationToken);
        var cariIdleri = kalemler.Select(k => k.CariId).Distinct().ToList();
        var cariler = await db.CariHesaplar.AsNoTracking()
            .Where(c => cariIdleri.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => new { c.Kod, c.Unvan, c.ParaBirimi }, cancellationToken);

        var satirlar = kalemler
            .Where(k => cariler.ContainsKey(k.CariId))
            .GroupBy(k => k.CariId)
            .Select(g =>
            {
                var c = cariler[g.Key];
                var liste = g.Select(x => x.Kalem).ToList();
                return new VadeAnaliziSatiri(g.Key, c.Kod, c.Unvan, c.ParaBirimi,
                    Dilimle(liste.Where(k => k.Yon == HareketYonu.Borc)),
                    Dilimle(liste.Where(k => k.Yon == HareketYonu.Alacak)),
                    Math.Max(0, liste.Max(k => k.GecikmeGun)));
            })
            .OrderByDescending(s => s.Alacak.VadesiGecen + s.Borc.VadesiGecen)
            .ThenBy(s => s.Unvan)
            .ToList();

        return TypedResults.Ok(new VadeAnalizi(raporTarihi, satirlar));
    }

    private static VadeDilimleri Dilimle(IEnumerable<AcikKalem> kalemler)
    {
        var liste = kalemler.ToList();
        decimal Topla(Func<int, bool> kosul) => liste.Where(k => kosul(k.GecikmeGun)).Sum(k => k.Kalan);
        return new VadeDilimleri(
            Topla(g => g <= 0), Topla(g => g is >= 1 and <= 30), Topla(g => g is >= 31 and <= 60), Topla(g => g is >= 61 and <= 90), Topla(g => g > 90));
    }
}
