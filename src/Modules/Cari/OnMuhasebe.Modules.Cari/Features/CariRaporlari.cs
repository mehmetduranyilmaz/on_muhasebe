using System.Globalization;
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
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;

namespace OnMuhasebe.Modules.Cari.Features;

internal static class CariRaporlari
{
    public static void Map(RouteGroupBuilder grup)
    {
        grup.MapGet("/hesaplar/{cariId:guid}/ekstre", EkstreAsync);
        grup.MapGet("/raporlar/bakiye-listesi", BakiyeListesiAsync);
        grup.MapGet("/raporlar/ozet", OzetAsync);
        grup.MapGet("/raporlar/yaslandirma", YaslandirmaAsync);
    }

    // Açık kalemlerin vade yaşına göre dağılımı (bugün itibarıyla). Alacak: carinin açık borç kalemleri; Borç: açık alacak kalemleri.
    // Fazla tahsilat/ödeme (avans) yaşlandırmaya girmez, ayrıca toplanır. TL karşılığı güncel TCMB döviz alış kuruyladır.
    private static async Task<IResult> YaslandirmaAsync(
        CariDbContext db, CariKapamaServisi kapama, IKurSorgusu kurSorgusu, TimeProvider zaman,
        YaslandirmaYonu? yon, string? arama, CancellationToken cancellationToken)
    {
        var y = yon ?? YaslandirmaYonu.Alacak;
        var kalemYonu = y == YaslandirmaYonu.Alacak ? HareketYonu.Borc : HareketYonu.Alacak;
        var bugun = TurkiyeSaati.Bugun(zaman);
        var kalemler = (await kapama.FirmaAcikKalemleriAsync(bugun, cancellationToken)).Where(x => x.Kalem.Yon == kalemYonu).ToList();

        var cariler = await db.CariHesaplar.AsNoTracking()
            .IgnoreQueryFilters([ModulDbContext.SilinmeFiltresi])
            .Select(c => new { c.Id, c.Kod, c.Unvan, c.ParaBirimi })
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var kurlar = new Dictionary<string, decimal> { [ParaBirimleri.Tl] = 1m };
        foreach (var pb in cariler.Values.Select(c => c.ParaBirimi).Distinct().Where(pb => pb != ParaBirimleri.Tl))
        {
            if (await kurSorgusu.IslemKuruAsync(pb, bugun, cancellationToken) is { } kur)
            {
                kurlar[pb] = kur.DovizAlis;
            }
        }

        decimal Tl(decimal tutar, string pb) => kurlar.TryGetValue(pb, out var kur) ? Para.TlKarsiligi(tutar, kur) : 0m;

        var avansTl = kalemler.Where(x => CariHareketTurleri.AvansMi(x.Kalem.Tur, x.Kalem.Yon) && cariler.ContainsKey(x.CariHesapId))
            .Sum(x => Tl(x.Kalem.Kalan, cariler[x.CariHesapId].ParaBirimi));

        var desen = string.IsNullOrWhiteSpace(arama) ? null : arama.Trim();
        var satirlar = kalemler
            .Where(x => !CariHareketTurleri.AvansMi(x.Kalem.Tur, x.Kalem.Yon) && cariler.ContainsKey(x.CariHesapId))
            .GroupBy(x => x.CariHesapId)
            .Select(g =>
            {
                var c = cariler[g.Key];
                var veri = g.Select(x => (x.Kalem.Kalan, x.Kalem.GecikmeGun)).ToList();
                var d = YaslandirmaHesaplayici.Dilimle(veri);
                var toplam = d.Sum();
                return new YaslandirmaSatiri(c.Id, c.Kod, c.Unvan, c.ParaBirimi, d[0], d[1], d[2], d[3], d[4], toplam,
                    Tl(toplam, c.ParaBirimi), Tl(toplam - d[0], c.ParaBirimi), YaslandirmaHesaplayici.OrtalamaGecikme(veri),
                    g.Min(x => x.Kalem.VadeTarihi ?? x.Kalem.Tarih));
            })
            .Where(s => desen is null || s.Unvan.Contains(desen, StringComparison.CurrentCultureIgnoreCase)
                                      || s.CariKodu.Contains(desen, StringComparison.CurrentCultureIgnoreCase))
            .OrderByDescending(s => s.VadesiGecmisTl).ThenByDescending(s => s.ToplamTl)
            .ToList();

        decimal DilimTl(Func<YaslandirmaSatiri, decimal> sec) => satirlar.Sum(s => Tl(sec(s), s.ParaBirimi));
        var listedekiler = satirlar.Select(s => s.CariHesapId).ToHashSet();
        var tumKalemler = kalemler
            .Where(x => !CariHareketTurleri.AvansMi(x.Kalem.Tur, x.Kalem.Yon) && listedekiler.Contains(x.CariHesapId))
            .Select(x => (Tl(x.Kalem.Kalan, cariler[x.CariHesapId].ParaBirimi), x.Kalem.GecikmeGun))
            .ToList();

        return TypedResults.Ok(new YaslandirmaRaporu(
            bugun, y,
            DilimTl(s => s.VadesiGelmemis), DilimTl(s => s.Gun1_30), DilimTl(s => s.Gun31_60), DilimTl(s => s.Gun61_90), DilimTl(s => s.Gun90Ustu),
            satirlar.Sum(s => s.ToplamTl),
            YaslandirmaHesaplayici.OrtalamaGecikme(tumKalemler),
            avansTl,
            satirlar));
    }

    private const int OzetListeBoyutu = 5;

    private static async Task<IResult> OzetAsync(
        CariDbContext db, IKurSorgusu kurSorgusu, TimeProvider zaman, CancellationToken cancellationToken)
    {
        // Aktif cari sayısı KOBİ ölçeğinde (binler) küçük olduğu için değerleme bellekte yapılır.
        // Hareketler bir kez gruplanır; cari başına alt sorgu on binlerce caride yavaşlar.
        var toplamlar = db.CariHareketler
            .GroupBy(h => h.CariHesapId)
            .Select(g => new { CariHesapId = g.Key, Bakiye = g.Sum(h => h.Borc - h.Alacak), BakiyeTl = g.Sum(h => h.BorcTl - h.AlacakTl) });
        var cariler = await (
                from c in db.CariHesaplar.AsNoTracking().Where(c => c.AktifMi)
                join t in toplamlar on c.Id equals t.CariHesapId into tj
                from t in tj.DefaultIfEmpty()
                select new
                {
                    c.Id,
                    c.Unvan,
                    c.ParaBirimi,
                    c.RiskLimiti,
                    Bakiye = t == null ? 0m : t.Bakiye,
                    BakiyeTl = t == null ? 0m : t.BakiyeTl
                })
            .ToListAsync(cancellationToken);

        var bugun = TurkiyeSaati.Bugun(zaman);
        var kurlar = new Dictionary<string, decimal>();
        foreach (var pb in cariler.Select(c => c.ParaBirimi).Distinct().Where(pb => pb != ParaBirimleri.Tl))
        {
            if (await kurSorgusu.IslemKuruAsync(pb, bugun, cancellationToken) is { } kur)
            {
                kurlar[pb] = kur.DovizAlis;
            }
        }

        var degerli = cariler.Select(c => new
        {
            c.Id,
            c.Unvan,
            c.ParaBirimi,
            c.Bakiye,
            RiskAsildi = c.RiskLimiti is { } limit && c.Bakiye > limit,
            GuncelTl = c.ParaBirimi == ParaBirimleri.Tl ? c.Bakiye
                : kurlar.TryGetValue(c.ParaBirimi, out var kur) ? Para.TlKarsiligi(c.Bakiye, kur) : c.BakiyeTl
        }).ToList();

        var sonHareketler = await (
                from h in db.CariHareketler.AsNoTracking()
                join c in db.CariHesaplar on h.CariHesapId equals c.Id
                orderby h.Id descending
                select new SonCariHareketi(c.Id, c.Unvan, h.Tarih, h.Tur, h.ParaBirimi, h.Borc, h.Alacak, h.Aciklama))
            .Take(OzetListeBoyutu)
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new CariOzetRaporu(
            degerli.Count,
            degerli.Count(c => c.RiskAsildi),
            degerli.Where(c => c.GuncelTl > 0).Sum(c => c.GuncelTl),
            -degerli.Where(c => c.GuncelTl < 0).Sum(c => c.GuncelTl),
            degerli.Where(c => c.ParaBirimi != ParaBirimleri.Tl)
                .GroupBy(c => c.ParaBirimi)
                .Select(g => new DovizPozisyonu(g.Key, g.Sum(c => c.Bakiye), g.Sum(c => c.GuncelTl)))
                .OrderByDescending(p => Math.Abs(p.GuncelTl))
                .ToList(),
            degerli.Where(c => c.GuncelTl > 0)
                .OrderByDescending(c => c.GuncelTl)
                .Take(OzetListeBoyutu)
                .Select(c => new OzetCari(c.Id, c.Unvan, c.ParaBirimi, c.Bakiye, c.GuncelTl))
                .ToList(),
            sonHareketler));
    }

    private static async Task<IResult> EkstreAsync(
        Guid cariId,
        DateOnly? baslangic,
        DateOnly? bitis,
        CariDbContext db,
        TimeProvider zaman,
        CancellationToken cancellationToken)
    {
        var bugun = TurkiyeSaati.Bugun(zaman);
        var bas = baslangic ?? new DateOnly(bugun.Year, 1, 1);
        var son = bitis ?? bugun;
        if (bas > son)
        {
            return Hata.Dogrulama("tarih_araligi", "Başlangıç tarihi bitiş tarihinden sonra olamaz.").Problem();
        }

        var cari = await db.CariHesaplar.AsNoTracking()
            .Where(c => c.Id == cariId)
            .Select(c => new { c.Id, c.Kod, c.Unvan, c.ParaBirimi })
            .SingleOrDefaultAsync(cancellationToken);
        if (cari is null)
        {
            return CariHatalari.CariBulunamadi.Problem();
        }

        var hareketler = db.CariHareketler.AsNoTracking().Where(h => h.CariHesapId == cariId);
        var devir = await hareketler
            .Where(h => h.Tarih < bas)
            .GroupBy(_ => 1)
            .Select(g => new { Bakiye = g.Sum(h => h.Borc - h.Alacak), BakiyeTl = g.Sum(h => h.BorcTl - h.AlacakTl) })
            .SingleOrDefaultAsync(cancellationToken);

        var satirlar = await hareketler
            .Where(h => h.Tarih >= bas && h.Tarih <= son)
            .OrderBy(h => h.Tarih).ThenBy(h => h.Id)
            .Select(h => new EkstreSatiri(
                h.Id, h.Tarih, h.VadeTarihi, h.Tur, h.BelgeNo, h.Aciklama, h.Borc, h.Alacak, 0m, h.Kur, h.BorcTl, h.AlacakTl, 0m, h.KaynakBelgeId))
            .ToListAsync(cancellationToken);

        var hesap = CariHesaplamalari.Ekstre(devir?.Bakiye ?? 0m, devir?.BakiyeTl ?? 0m, satirlar);
        return TypedResults.Ok(new EkstreCevabi(
            cari.Id, cari.Kod, cari.Unvan, cari.ParaBirimi, bas, son, devir?.Bakiye ?? 0m, devir?.BakiyeTl ?? 0m,
            hesap.Satirlar, hesap.ToplamBorc, hesap.ToplamAlacak, hesap.SonBakiye,
            hesap.ToplamBorcTl, hesap.ToplamAlacakTl, hesap.SonBakiyeTl));
    }

    private static async Task<IResult> BakiyeListesiAsync(
        CariDbContext db,
        IKurSorgusu kurSorgusu,
        TimeProvider zaman,
        DateOnly? tarih,
        BakiyeDurumu? durum,
        CariTuru? tur,
        Guid? grupId,
        string? paraBirimi,
        string? arama,
        int? sayfa,
        int? boyut,
        CancellationToken cancellationToken)
    {
        var raporTarihi = tarih ?? TurkiyeSaati.Bugun(zaman);

        var cariler = db.CariHesaplar.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(arama))
        {
            var desen = AramaDeseni.Icerir(arama);
            cariler = cariler.Where(c => EF.Functions.ILike(c.Unvan, desen) || EF.Functions.ILike(c.Kod, desen));
        }

        if (tur is { } secilenTur)
        {
            cariler = cariler.Where(c => c.Tur == secilenTur || c.Tur == CariTuru.MusteriVeTedarikci);
        }

        if (grupId is not null)
        {
            cariler = cariler.Where(c => c.GrupId == grupId);
        }

        if (!string.IsNullOrWhiteSpace(paraBirimi))
        {
            cariler = cariler.Where(c => c.ParaBirimi == paraBirimi);
        }

        // İki biçim: carinin toplamları alt sorguyla (yalnızca sayfadaki 25 cari için hesaplanır; (firma_id, cari_hesap_id, tarih)
        // covering index'inden) ya da hareketler bir kez gruplanıp birleştirilerek (tüm cariler gerektiğinde: toplamlar, bakiye filtresi).
        var hareketler = db.CariHareketler.Where(h => h.Tarih <= raporTarihi);
        var tekTek = cariler.Select(c => new CariBakiye
        {
            Id = c.Id,
            Kod = c.Kod,
            Unvan = c.Unvan,
            ParaBirimi = c.ParaBirimi,
            Borc = hareketler.Where(h => h.CariHesapId == c.Id).Sum(h => h.Borc),
            Alacak = hareketler.Where(h => h.CariHesapId == c.Id).Sum(h => h.Alacak),
            BakiyeTl = hareketler.Where(h => h.CariHesapId == c.Id).Sum(h => h.BorcTl - h.AlacakTl)
        });
        var gruplu =
            from c in cariler
            join t in hareketler
                .GroupBy(h => h.CariHesapId)
                .Select(g => new { CariHesapId = g.Key, Borc = g.Sum(h => h.Borc), Alacak = g.Sum(h => h.Alacak), BakiyeTl = g.Sum(h => h.BorcTl - h.AlacakTl) })
                on c.Id equals t.CariHesapId
            select new CariBakiye
            {
                Id = c.Id,
                Kod = c.Kod,
                Unvan = c.Unvan,
                ParaBirimi = c.ParaBirimi,
                Borc = t.Borc,
                Alacak = t.Alacak,
                BakiyeTl = t.BakiyeTl
            };

        IQueryable<CariBakiye> Filtrele(IQueryable<CariBakiye> sorgu) => durum switch
        {
            BakiyeDurumu.Borclu => sorgu.Where(x => x.Borc - x.Alacak > 0),
            BakiyeDurumu.Alacakli => sorgu.Where(x => x.Borc - x.Alacak < 0),
            BakiyeDurumu.SifirHaric => sorgu.Where(x => x.Borc - x.Alacak != 0),
            _ => sorgu
        };

        // Gruplu biçim hareketi olmayan carileri içermez; sıfır bakiyeyi dışlayan filtrelerde ve toplamlarda sonuç aynıdır.
        // Filtresiz: sayfa tek tek, toplamlar gruplu sorgudan. Filtreli: gruplu satırlar bir kez okunur (cari başına küçük bir satır),
        // toplam ve sayfa bellekte; aynı ağır sorgu sayım, toplam ve sayfa için üç kez çalışmaz. Gruplu biçim hareketi olmayan carileri
        // içermez; sıfır bakiyeyi dışlayan filtrelerde ve toplamlarda sonuç aynıdır.
        var istek = new SayfalamaIstegi(sayfa ?? 1, boyut ?? SayfalamaIstegi.VarsayilanBoyut);
        List<ParaBirimiAraToplami> paraBirimiToplamlari;
        SayfaliSonuc<CariBakiye> sayfaSonucu;
        if (durum is null or BakiyeDurumu.Tumu)
        {
            paraBirimiToplamlari = await gruplu
                .GroupBy(x => x.ParaBirimi)
                .Select(g => new ParaBirimiAraToplami(g.Key, g.Sum(x => x.Borc), g.Sum(x => x.Alacak), g.Sum(x => x.BakiyeTl)))
                .ToListAsync(cancellationToken);
            sayfaSonucu = await tekTek.OrderBy(x => x.Unvan).SayfalaAsync(istek, cancellationToken);
        }
        else
        {
            var tumu = await Filtrele(gruplu).ToListAsync(cancellationToken);
            paraBirimiToplamlari = tumu
                .GroupBy(x => x.ParaBirimi)
                .Select(g => new ParaBirimiAraToplami(g.Key, g.Sum(x => x.Borc), g.Sum(x => x.Alacak), g.Sum(x => x.BakiyeTl)))
                .ToList();
            sayfaSonucu = new SayfaliSonuc<CariBakiye>(
                tumu.OrderBy(x => x.Unvan, StringComparer.Create(new CultureInfo("tr-TR"), false))
                    .Skip((istek.GecerliSayfa - 1) * istek.GecerliBoyut).Take(istek.GecerliBoyut).ToList(),
                istek.GecerliSayfa, istek.GecerliBoyut, tumu.Count);
        }

        paraBirimiToplamlari = paraBirimiToplamlari.OrderBy(x => x.ParaBirimi, StringComparer.Ordinal).ToList();

        // Kur farkı için rapor tarihinde geçerli kur; her para birimi için tek sorgu.
        var guncelKurlar = new Dictionary<string, decimal>();
        foreach (var pb in paraBirimiToplamlari.Select(x => x.ParaBirimi).Where(pb => pb != ParaBirimleri.Tl))
        {
            if (await kurSorgusu.IslemKuruAsync(pb, raporTarihi, cancellationToken) is { } kur)
            {
                guncelKurlar[pb] = kur.DovizAlis;
            }
        }

        var satirlar = sayfaSonucu.Kayitlar.Select(x =>
        {
            var bakiye = x.Borc - x.Alacak;
            var (guncelKur, guncelTl, kurFarki) = Degerle(x.ParaBirimi, bakiye, x.BakiyeTl, guncelKurlar);
            return new BakiyeSatiri(x.Id, x.Kod, x.Unvan, x.ParaBirimi, x.Borc, x.Alacak, bakiye, x.BakiyeTl, guncelKur, guncelTl, kurFarki);
        }).ToList();

        var toplamSatirlari = paraBirimiToplamlari.Select(x =>
        {
            var bakiye = x.Borc - x.Alacak;
            var (_, guncelTl, kurFarki) = Degerle(x.ParaBirimi, bakiye, x.BakiyeTl, guncelKurlar);
            return new ParaBirimiToplami(x.ParaBirimi, x.Borc, x.Alacak, bakiye, x.BakiyeTl, guncelTl, kurFarki);
        }).ToList();

        return TypedResults.Ok(new BakiyeListesiCevabi(
            raporTarihi,
            new SayfaliSonuc<BakiyeSatiri>(satirlar, sayfaSonucu.Sayfa, sayfaSonucu.Boyut, sayfaSonucu.ToplamKayit),
            toplamSatirlari));
    }

    private sealed record ParaBirimiAraToplami(string ParaBirimi, decimal Borc, decimal Alacak, decimal BakiyeTl);

    // Kayıt (record) yerine sınıf: kurucuyla oluşturulan nesne üzerinden sonradan filtre SQL'e çevrilemiyor.
    private sealed class CariBakiye
    {
        public Guid Id { get; init; }
        public string Kod { get; init; } = "";
        public string Unvan { get; init; } = "";
        public string ParaBirimi { get; init; } = "";
        public decimal Borc { get; init; }
        public decimal Alacak { get; init; }
        public decimal BakiyeTl { get; init; }
    }

    private static (decimal? GuncelKur, decimal? GuncelTl, decimal? KurFarki) Degerle(
        string paraBirimi, decimal bakiye, decimal bakiyeTl, Dictionary<string, decimal> guncelKurlar)
    {
        if (paraBirimi == ParaBirimleri.Tl || !guncelKurlar.TryGetValue(paraBirimi, out var kur))
        {
            return (null, null, null);
        }

        var (guncelTl, kurFarki) = CariHesaplamalari.KurFarki(bakiye, bakiyeTl, kur);
        return (kur, guncelTl, kurFarki);
    }
}
