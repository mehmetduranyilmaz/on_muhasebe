using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.Doviz.Contracts;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.KasaBanka.Infrastructure;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Infrastructure.Kimlik;

namespace OnMuhasebe.Modules.KasaBanka.Features;

// Ana sayfa finansal durumu: mini bilanço, vade kovaları ve son 12 ayın gerçekleşen nakit akışı.
internal static class FinansalDurumIslemleri
{
    private const int AySayisi = 12;

    // İç transferler ve açılış kaydı işletmeye giren/çıkan nakit değildir.
    private static readonly KasaHareketTuru[] _akisDisi = [KasaHareketTuru.Acilis, KasaHareketTuru.VirmanGiris, KasaHareketTuru.VirmanCikis];

    public static void Map(RouteGroupBuilder grup) => grup.MapGet("/raporlar/finansal-durum", RaporAsync).Yetki(KasaBankaYetkileri.Rapor);

    private static async Task<IResult> RaporAsync(
        KasaBankaDbContext db, ICariSorgusu cariSorgusu, IKurSorgusu kurSorgusu, TimeProvider zaman, CancellationToken cancellationToken)
    {
        var bugun = TurkiyeSaati.Bugun(zaman);

        var hesaplar = await db.Hesaplar.AsNoTracking()
            .Select(h => new { h.Tur, h.ParaBirimi, Bakiye = db.KasaHareketleri.Where(x => x.HesapId == h.Id).Sum(x => x.Giris - x.Cikis) })
            .Where(h => h.Bakiye != 0)
            .ToListAsync(cancellationToken);

        var evraklar = await db.Evraklar.AsNoTracking()
            .Where(e => (e.Yon == EvrakYonu.Alinan && (e.Durum == EvrakDurumu.Portfoyde || e.Durum == EvrakDurumu.Tahsilde))
                        || (e.Yon == EvrakYonu.Verilen && e.Durum == EvrakDurumu.Verildi))
            .Select(e => new { e.Yon, e.VadeTarihi, e.ParaBirimi, Kalan = e.Tutar - e.TahsilEdilen })
            .ToListAsync(cancellationToken);

        var krediAnaparalari = await (
                from t in db.KrediTaksitleri.AsNoTracking()
                join kr in db.Krediler on t.KrediId equals kr.Id
                where kr.Durum == KrediDurumu.Acik && t.OdemeIslemId == null
                group t.Anapara by kr.ParaBirimi into g
                select new { ParaBirimi = g.Key, Anapara = g.Sum() })
            .ToListAsync(cancellationToken);

        var cariKalemler = await cariSorgusu.AcikKalemlerAsync(cancellationToken);

        var kurlar = new Dictionary<string, decimal?> { [ParaBirimleri.Tl] = 1m };
        foreach (var pb in hesaplar.Select(h => h.ParaBirimi).Concat(evraklar.Select(e => e.ParaBirimi)).Concat(cariKalemler.Select(k => k.ParaBirimi))
                     .Concat(krediAnaparalari.Select(k => k.ParaBirimi)).Distinct().Where(pb => !kurlar.ContainsKey(pb)))
        {
            kurlar[pb] = (await kurSorgusu.IslemKuruAsync(pb, bugun, cancellationToken))?.DovizAlis;
        }

        decimal Tl(decimal tutar, string pb) => kurlar.GetValueOrDefault(pb) is { } kur ? Para.TlKarsiligi(tutar, kur) : 0m;

        var bilanco = FinansalDurumHesaplayici.Bilanco(
            hesaplar.Where(h => h.Tur is HesapTuru.Kasa or HesapTuru.Banka).Sum(h => Tl(h.Bakiye, h.ParaBirimi)),
            hesaplar.Where(h => h.Tur == HesapTuru.Pos && h.Bakiye > 0).Sum(h => Tl(h.Bakiye, h.ParaBirimi)),
            cariKalemler.Where(k => k.Yon == HareketYonu.Borc).Sum(k => Tl(k.Kalan, k.ParaBirimi)),
            evraklar.Where(e => e.Yon == EvrakYonu.Alinan).Sum(e => Tl(e.Kalan, e.ParaBirimi)),
            cariKalemler.Where(k => k.Yon == HareketYonu.Alacak).Sum(k => Tl(k.Kalan, k.ParaBirimi)),
            evraklar.Where(e => e.Yon == EvrakYonu.Verilen).Sum(e => Tl(e.Kalan, e.ParaBirimi)),
            -hesaplar.Where(h => h.Tur == HesapTuru.KrediKarti && h.Bakiye < 0).Sum(h => Tl(h.Bakiye, h.ParaBirimi)),
            krediAnaparalari.Sum(k => Tl(k.Anapara, k.ParaBirimi)));

        var kovalar = FinansalDurumHesaplayici.VadeKovalari(bugun,
            cariKalemler.Select(k => new VadeliKalem(k.Vade, k.Yon == HareketYonu.Borc, Tl(k.Kalan, k.ParaBirimi)))
                .Concat(evraklar.Select(e => new VadeliKalem(e.VadeTarihi, e.Yon == EvrakYonu.Alinan, Tl(e.Kalan, e.ParaBirimi)))));

        var ilkGun = new DateOnly(bugun.Year, bugun.Month, 1).AddMonths(-(AySayisi - 1));
        var nakitHesaplari = db.Hesaplar.Where(h => h.Tur == HesapTuru.Kasa || h.Tur == HesapTuru.Banka || h.Tur == HesapTuru.Pos).Select(h => h.Id);
        var akis = await db.KasaHareketleri.AsNoTracking()
            .Where(x => x.Tarih >= ilkGun && x.Tarih <= bugun && !_akisDisi.Contains(x.Tur))
            .GroupBy(x => new { x.Tarih.Year, x.Tarih.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Giris = g.Sum(x => x.GirisTl), Cikis = g.Sum(x => x.CikisTl) })
            .ToListAsync(cancellationToken);
        var oncekiNakit = await db.KasaHareketleri.AsNoTracking()
            .Where(x => x.Tarih < ilkGun && nakitHesaplari.Contains(x.HesapId))
            .SumAsync(x => x.GirisTl - x.CikisTl, cancellationToken);
        var degisim = await db.KasaHareketleri.AsNoTracking()
            .Where(x => x.Tarih >= ilkGun && x.Tarih <= bugun && nakitHesaplari.Contains(x.HesapId))
            .GroupBy(x => new { x.Tarih.Year, x.Tarih.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Net = g.Sum(x => x.GirisTl - x.CikisTl) })
            .ToListAsync(cancellationToken);

        var aylar = FinansalDurumHesaplayici.Aylar(bugun, AySayisi, oncekiNakit,
            akis.ToDictionary(a => (a.Year, a.Month), a => (a.Giris, a.Cikis)),
            degisim.ToDictionary(d => (d.Year, d.Month), d => d.Net));

        return TypedResults.Ok(new FinansalDurumRaporu(bugun, bilanco, kovalar, aylar, kurlar.Where(k => k.Value is null).Select(k => k.Key).ToList()));
    }
}
