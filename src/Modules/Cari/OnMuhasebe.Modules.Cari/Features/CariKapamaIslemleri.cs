using System.Globalization;
using System.Linq.Expressions;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.Cari.Domain;
using OnMuhasebe.Modules.Cari.Infrastructure;
using OnMuhasebe.Modules.Doviz.Contracts;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Shared.Contracts.Sayfalama;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Infrastructure.Persistence;
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
        grup.MapGet("/hesaplar/{cariId:guid}/avans", AvansDurumuAsync);
        grup.MapPost("/avans-mahsubu", AvansMahsupAsync);
    }

    private static async Task<IResult> AvansDurumuAsync(Guid cariId, CariKapamaServisi kapama, CancellationToken cancellationToken) =>
        await kapama.AvansDurumuAsync(cariId, cancellationToken) is { } durum ? TypedResults.Ok(durum) : CariHatalari.CariBulunamadi.Problem();

    private static async Task<IResult> AvansMahsupAsync(AvansMahsubuIstegi istek, CariKapamaServisi kapama, CancellationToken cancellationToken)
    {
        if (istek.Tutar is <= 0)
        {
            return CariHatalari.KapamaTutariGecersiz.Problem();
        }

        var sonuc = await kapama.AvansMahsupEtAsync(istek, cancellationToken);
        return sonuc.BasariliMi ? TypedResults.Ok(sonuc.Deger) : sonuc.HttpSonucu();
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

    // Açık kalemlerin cari bazında gecikme dilimlerine dağılımı. Kapama bir kalemin tutarını aşamadığından dilimdeki açık tutar =
    // dilimdeki kalemlerin tutarı - o kalemlerin kapamaları; ikisi de kalem başına alt sorgu olmadan gruplanır.
    // Gecikme = rapor tarihi - vade (vade yoksa işlem tarihi); 0 ve altı vadesi gelmemiş sayılır.
    // Avanslar (fazla tahsilat/ödeme) vadesi gelen alacak/borç değildir: dilimlere girmez, ayrıca toplanır.
    // Özet kartları dövizli carileri de rapor tarihinin TCMB döviz alış kuruyla (VUK değerleme kuru) TL'ye çevirerek içerir.
    private static async Task<IResult> VadeAnaliziAsync(
        DateOnly? tarih, bool? alacaklar, string? arama, int? sayfa, int? boyut, CariDbContext db, IKurSorgusu kurSorgusu, TimeProvider zaman,
        CancellationToken cancellationToken)
    {
        var rapor = tarih ?? TurkiyeSaati.Bugun(zaman);
        var alacakMi = alacaklar ?? true;
        var (g30, g60, g90) = (rapor.AddDays(-30), rapor.AddDays(-60), rapor.AddDays(-90));

        var cariler = db.CariHesaplar.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(arama))
        {
            var desen = AramaDeseni.Icerir(arama);
            cariler = cariler.Where(c => EF.Functions.ILike(c.Unvan, desen) || EF.Functions.ILike(c.Kod, desen));
        }

        // Carinin bize borcu (alacağımız) borç yönlü kalemlerdir; borç kalemi kapamada BorcHareketId olarak yer alır.
        var kalemler = db.CariHareketler.AsNoTracking()
            .Where(h => h.IptalHareketId == null && h.IptalEdilenHareketId == null && (alacakMi
                ? h.Borc > 0 && h.Tur != CariHareketTuru.Odeme && h.Tur != CariHareketTuru.CekSenetCikisi
                : h.Alacak > 0 && h.Tur != CariHareketTuru.Tahsilat && h.Tur != CariHareketTuru.CekSenetGirisi))
            .Join(cariler, h => h.CariHesapId, c => c.Id, (h, c) => h);

        // Alacaklar ekranında carilerden alınan, borçlar ekranında carilere verilen ve henüz mahsup edilmemiş avanslar.
        var avanslar = await db.CariHareketler.AsNoTracking()
            .Where(h => h.IptalHareketId == null && h.IptalEdilenHareketId == null && (alacakMi
                ? h.Alacak > 0 && (h.Tur == CariHareketTuru.Tahsilat || h.Tur == CariHareketTuru.CekSenetGirisi)
                : h.Borc > 0 && (h.Tur == CariHareketTuru.Odeme || h.Tur == CariHareketTuru.CekSenetCikisi)))
            .Join(cariler, h => h.CariHesapId, c => c.Id, (h, c) => new
            {
                c.ParaBirimi,
                Acik = h.Borc + h.Alacak
                       - (db.CariKapamalari.Where(k => k.BorcHareketId == h.Id || k.AlacakHareketId == h.Id).Sum(k => (decimal?)k.Tutar) ?? 0m)
            })
            .Where(x => x.Acik > 0)
            .GroupBy(x => x.ParaBirimi)
            .Select(g => new { ParaBirimi = g.Key, Tutar = g.Sum(x => x.Acik) })
            .ToListAsync(cancellationToken);
        var tutarlar = kalemler.Select(h => new { h.CariHesapId, Vade = h.VadeTarihi ?? h.Tarih, Tutar = h.Borc + h.Alacak });
        Expression<Func<CariKapama, Guid>> kapamaKalemi = alacakMi ? k => k.BorcHareketId : k => k.AlacakHareketId;
        var kapananlar = db.CariKapamalari
            .Join(kalemler, kapamaKalemi, h => h.Id, (k, h) => new { h.CariHesapId, Vade = h.VadeTarihi ?? h.Tarih, k.Tutar });

        var brut = await Dilimle(tutarlar.Select(x => new DilimKalemi { CariHesapId = x.CariHesapId, Vade = x.Vade, Tutar = x.Tutar }));
        var kapanan = (await Dilimle(kapananlar.Select(x => new DilimKalemi { CariHesapId = x.CariHesapId, Vade = x.Vade, Tutar = x.Tutar })))
            .ToDictionary(d => d.CariHesapId);

        var acik = brut
            .Select(b => kapanan.TryGetValue(b.CariHesapId, out var k)
                ? (b.CariHesapId, Dilimler: new VadeDilimleri(b.Gelmemis - k.Gelmemis, b.Gun30 - k.Gun30, b.Gun60 - k.Gun60, b.Gun90 - k.Gun90, b.Ustu - k.Ustu))
                : (b.CariHesapId, Dilimler: new VadeDilimleri(b.Gelmemis, b.Gun30, b.Gun60, b.Gun90, b.Ustu)))
            .Where(x => x.Dilimler.Toplam > 0)
            .ToList();

        var bilgiler = await cariler
            .Where(c => acik.Select(a => a.CariHesapId).Contains(c.Id))
            .Select(c => new { c.Id, c.Kod, c.Unvan, c.ParaBirimi })
            .ToDictionaryAsync(c => c.Id, cancellationToken);
        var satirlar = acik
            .Where(a => bilgiler.ContainsKey(a.CariHesapId))
            .Select(a => (Bilgi: bilgiler[a.CariHesapId], a.Dilimler))
            .ToList();

        var kurlar = new Dictionary<string, decimal?> { [ParaBirimleri.Tl] = 1m };
        foreach (var pb in satirlar.Select(s => s.Bilgi.ParaBirimi).Concat(avanslar.Select(a => a.ParaBirimi)).Distinct().Where(pb => !kurlar.ContainsKey(pb)))
        {
            kurlar[pb] = (await kurSorgusu.IslemKuruAsync(pb, rapor, cancellationToken))?.DovizAlis;
        }

        var tlSatirlari = satirlar
            .Where(s => kurlar[s.Bilgi.ParaBirimi] is not null)
            .Select(s => (s.Dilimler, Kur: kurlar[s.Bilgi.ParaBirimi]!.Value))
            .ToList();
        decimal TlTopla(Func<VadeDilimleri, decimal> sec) => tlSatirlari.Sum(s => Para.TlKarsiligi(sec(s.Dilimler), s.Kur));
        var tl = new VadeDilimleri(
            TlTopla(d => d.VadesiGelmemis), TlTopla(d => d.Gun0Ile30), TlTopla(d => d.Gun31Ile60), TlTopla(d => d.Gun61Ile90), TlTopla(d => d.Gun90Ustu));
        var avansTl = avanslar.Where(a => kurlar[a.ParaBirimi] is not null).Sum(a => Para.TlKarsiligi(a.Tutar, kurlar[a.ParaBirimi]!.Value));

        var istek = new SayfalamaIstegi(sayfa ?? 1, boyut ?? SayfalamaIstegi.VarsayilanBoyut);
        var sayfadakiler = satirlar
            .OrderByDescending(s => s.Dilimler.VadesiGecen)
            .ThenByDescending(s => s.Dilimler.Toplam)
            .ThenBy(s => s.Bilgi.Unvan, StringComparer.Create(new CultureInfo("tr-TR"), false))
            .Skip((istek.GecerliSayfa - 1) * istek.GecerliBoyut)
            .Take(istek.GecerliBoyut)
            .ToList();

        // En eski açık kalemin vadesi yalnızca sayfadaki cariler için, kalem başına kapama toplamıyla bulunur.
        var sayfaIdleri = sayfadakiler.Select(s => s.Bilgi.Id).ToList();
        var enEskiler = await kalemler
            .Where(h => sayfaIdleri.Contains(h.CariHesapId)
                        && h.Borc + h.Alacak > (db.CariKapamalari
                            .Where(k => k.BorcHareketId == h.Id || k.AlacakHareketId == h.Id)
                            .Sum(k => (decimal?)k.Tutar) ?? 0m))
            .GroupBy(h => h.CariHesapId)
            .Select(g => new { CariHesapId = g.Key, EnEski = g.Min(h => h.VadeTarihi ?? h.Tarih) })
            .ToDictionaryAsync(x => x.CariHesapId, x => x.EnEski, cancellationToken);

        return TypedResults.Ok(new VadeAnalizi(
            rapor,
            alacakMi,
            tl,
            new SayfaliSonuc<VadeAnaliziSatiri>(
                sayfadakiler
                    .Select(s => new VadeAnaliziSatiri(s.Bilgi.Id, s.Bilgi.Kod, s.Bilgi.Unvan, s.Bilgi.ParaBirimi, s.Dilimler,
                        enEskiler.TryGetValue(s.Bilgi.Id, out var enEski) ? Math.Max(0, rapor.DayNumber - enEski.DayNumber) : 0))
                    .ToList(),
                istek.GecerliSayfa, istek.GecerliBoyut, satirlar.Count),
            avansTl,
            kurlar.Where(k => k.Value is null).Select(k => k.Key).ToList()));

        Task<List<DilimToplami>> Dilimle(IQueryable<DilimKalemi> sorgu) => sorgu
            .GroupBy(x => x.CariHesapId)
            .Select(g => new DilimToplami
            {
                CariHesapId = g.Key,
                Gelmemis = g.Sum(x => x.Vade >= rapor ? x.Tutar : 0m),
                Gun30 = g.Sum(x => x.Vade < rapor && x.Vade >= g30 ? x.Tutar : 0m),
                Gun60 = g.Sum(x => x.Vade < g30 && x.Vade >= g60 ? x.Tutar : 0m),
                Gun90 = g.Sum(x => x.Vade < g60 && x.Vade >= g90 ? x.Tutar : 0m),
                Ustu = g.Sum(x => x.Vade < g90 ? x.Tutar : 0m)
            })
            .ToListAsync(cancellationToken);
    }

    private sealed class DilimKalemi
    {
        public Guid CariHesapId { get; init; }
        public DateOnly Vade { get; init; }
        public decimal Tutar { get; init; }
    }

    private sealed class DilimToplami
    {
        public Guid CariHesapId { get; init; }
        public decimal Gelmemis { get; init; }
        public decimal Gun30 { get; init; }
        public decimal Gun60 { get; init; }
        public decimal Gun90 { get; init; }
        public decimal Ustu { get; init; }
    }
}
