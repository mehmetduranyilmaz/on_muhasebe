using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Modules.Fatura.Infrastructure;
using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Results;

namespace OnMuhasebe.Modules.Fatura.Features;

internal static class FaturaRaporlari
{
    private const int OzetListeBoyutu = 5;
    private const int GrafikAySayisi = 6;

    public static void Map(RouteGroupBuilder grup)
    {
        grup.MapGet("/raporlar/ozet", OzetAsync);
        grup.MapGet("/raporlar/karlilik", KarlilikAsync);
        grup.MapGet("/raporlar/kdv", KdvAsync);
    }

    // Brüt kârlılık: onaylı satış ve satıştan iade faturaları. Net satış = satır matrahı (iskontolar düşülmüş, KDV hariç) × fatura kuru.
    // Maliyet stoktan (firmanın maliyet yöntemiyle): satır irsaliyeden aktarıldıysa irsaliyenin çıkışından, değilse faturanın kendi çıkışından;
    // satıştan iadede stoğa giriş maliyeti düşülür. Hizmet ve serbest satırın maliyeti izlenmez.
    private static async Task<IResult> KarlilikAsync(
        FaturaDbContext db, IStokSorgusu stokSorgusu, TimeProvider zaman,
        DateOnly? baslangic, DateOnly? bitis, KarlilikKirilimi? kirilim, CancellationToken cancellationToken)
    {
        var bugun = TurkiyeSaati.Bugun(zaman);
        var (bas, son) = (baslangic ?? new DateOnly(bugun.Year, bugun.Month, 1), bitis ?? bugun);
        if (son < bas)
        {
            return Hata.Dogrulama("tarih_araligi", "Bitiş tarihi başlangıçtan önce olamaz.").Problem();
        }

        var faturalar = await db.Faturalar.AsNoTracking().Include(f => f.Satirlar)
            .Where(f => f.Durum == FaturaDurumu.Onayli && (f.Tur == FaturaTuru.Satis || f.Tur == FaturaTuru.SatistanIade)
                        && f.Tarih >= bas && f.Tarih <= son)
            .ToListAsync(cancellationToken);

        var irsaliyeSatirIdleri = faturalar.SelectMany(f => f.Satirlar).Where(s => s.IrsaliyeSatiriId is not null).Select(s => s.IrsaliyeSatiriId!.Value).ToList();
        var irsaliyeleri = irsaliyeSatirIdleri.Count == 0 ? new Dictionary<Guid, Guid>() : await db.IrsaliyeSatirlari.AsNoTracking()
            .Where(s => irsaliyeSatirIdleri.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.IrsaliyeId, cancellationToken);

        var kaynaklar = faturalar.Select(f => f.Id).Concat(irsaliyeleri.Values).Distinct().ToList();
        var maliyetler = (await stokSorgusu.BelgeMaliyetleriAsync(kaynaklar, cancellationToken)).ToDictionary(m => (m.KaynakBelgeId, m.StokKartiId));
        var kartIdleri = faturalar.SelectMany(f => f.Satirlar).Where(s => s.StokKartiId is not null).Select(s => s.StokKartiId!.Value).Distinct().ToList();
        var kartlar = kartIdleri.Count == 0 ? new Dictionary<Guid, StokKartiBilgisi>()
            : (await stokSorgusu.KartlariGetirAsync(kartIdleri, cancellationToken)).ToDictionary(k => k.Id);

        var kalemler = new List<KarlilikKalemi>();
        foreach (var f in faturalar)
        {
            var isaret = f.Tur == FaturaTuru.SatistanIade ? -1m : 1m;
            foreach (var s in f.Satirlar)
            {
                var katsayi = s.StokKartiId is { } kartId && kartlar.TryGetValue(kartId, out var kart)
                    ? (s.Birim == kart.TemelBirim ? 1m : kart.EkBirimler.FirstOrDefault(b => b.BirimKodu == s.Birim)?.Katsayi ?? 1m)
                    : 1m;
                var temelMiktar = s.Miktar * katsayi;
                decimal? maliyet = null;
                if (s.StokluMu)
                {
                    var kaynak = s.IrsaliyeSatiriId is { } irsSatir && irsaliyeleri.TryGetValue(irsSatir, out var irsaliyeId) ? irsaliyeId : f.Id;
                    if (maliyetler.TryGetValue((kaynak, s.StokKartiId!.Value), out var m))
                    {
                        var birim = f.Tur == FaturaTuru.SatistanIade
                            ? (m.GirisMiktari == 0 ? 0m : m.GirisMaliyetiTl / m.GirisMiktari)
                            : m.CikisBirimMaliyeti;
                        maliyet = Para.Yuvarla(temelMiktar * birim) * isaret;
                    }
                }

                kalemler.Add(new KarlilikKalemi(f.Id, f.Numara, f.Tarih, f.CariHesapId, f.CariUnvan, s.StokKartiId, s.StokKodu, s.Ad,
                    temelMiktar * isaret, Para.Yuvarla(s.Matrah * f.Kur) * isaret, maliyet));
            }
        }

        var k = kirilim ?? KarlilikKirilimi.Urun;
        var netSatis = kalemler.Sum(x => x.NetSatisTl);
        var toplamMaliyet = kalemler.Sum(x => x.MaliyetTl ?? 0m);
        return TypedResults.Ok(new KarlilikRaporu(bas, son, k, netSatis, toplamMaliyet, netSatis - toplamMaliyet,
            FaturaRaporHesaplayici.Marj(netSatis - toplamMaliyet, netSatis), kalemler.Where(x => x.MaliyetTl is null).Sum(x => x.NetSatisTl),
            FaturaRaporHesaplayici.Karlilik(kalemler, k)));
    }

    // KDV beyan hazırlık özeti (aylık, fatura tarihine göre). Döviz faturaların TL karşılığı fatura kuruyladır.
    // Muhtasar için stopaj belge tarihine göre toplanır (hesaben ödeme); fiili ödeme tarihi farklıysa beyan dönemi değişebilir.
    private static async Task<IResult> KdvAsync(FaturaDbContext db, TimeProvider zaman, int? yil, int? ay, CancellationToken cancellationToken)
    {
        var bugun = TurkiyeSaati.Bugun(zaman);
        var (y, a) = (yil ?? bugun.Year, ay ?? bugun.Month);
        if (a is < 1 or > 12 || y is < 2000 or > 2100)
        {
            return Hata.Dogrulama("donem", "Geçersiz dönem.").Problem();
        }

        var bas = new DateOnly(y, a, 1);
        var son = bas.AddMonths(1).AddDays(-1);
        var faturalar = await db.Faturalar.AsNoTracking().Include(f => f.Satirlar)
            .Where(f => f.Durum == FaturaDurumu.Onayli && f.Tarih >= bas && f.Tarih <= son)
            .ToListAsync(cancellationToken);

        var kalemler = faturalar.SelectMany(f => f.Satirlar.Select(s => new KdvKalemi(f.Tur, s.KdvOrani,
                Para.Yuvarla(s.Matrah * f.Kur), Para.Yuvarla(s.KdvTutari * f.Kur), s.TevkifatKodu, s.TevkifatPay, s.TevkifatPayda,
                Para.Yuvarla(s.TevkifEdilenKdv * f.Kur))))
            .ToList();
        var stopajlar = faturalar.Where(f => f.StopajTutari > 0)
            .Select(f => new StopajKalemi(f.Tur, f.StopajOrani, Para.Yuvarla(f.Matrah * f.Kur), Para.Yuvarla(f.StopajTutari * f.Kur)))
            .ToList();

        return TypedResults.Ok(FaturaRaporHesaplayici.KdvOzeti(y, a, kalemler, stopajlar, faturalar.Count));
    }

    // Alış tarafı giderleri: alış faturası, gider pusulası ve serbest meslek makbuzu.
    private static readonly FaturaTuru[] _alisTurleri = [FaturaTuru.Alis, FaturaTuru.GiderPusulasi, FaturaTuru.SerbestMeslekMakbuzu];

    // Ana sayfa özeti: yalnızca onaylı faturalar; tutarlar TL ve KDV hariç, iadeler düşülmüş.
    // KDV1 mantığı: satıcı tevkif edilen KDV'yi hesaplanan KDV'ye katmaz (alıcı KDV2 ile öder); alıcı KDV'nin tamamını indirir.
    private static async Task<IResult> OzetAsync(FaturaDbContext db, TimeProvider zaman, CancellationToken cancellationToken)
    {
        var bugun = TurkiyeSaati.Bugun(zaman);
        var ayBasi = new DateOnly(bugun.Year, bugun.Month, 1);
        var ilkAy = ayBasi.AddMonths(-(GrafikAySayisi - 1));

        var aylik = await db.Faturalar.AsNoTracking()
            .Where(f => f.Durum == FaturaDurumu.Onayli && f.Tarih >= ilkAy)
            .GroupBy(f => new { f.Tarih.Year, f.Tarih.Month, f.Tur })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.Tur,
                Matrah = g.Sum(f => f.Matrah * f.Kur),
                Kdv = g.Sum(f => f.KdvToplami * f.Kur),
                Tevkif = g.Sum(f => f.TevkifEdilenKdvToplami * f.Kur),
                Sayi = g.Count()
            })
            .ToListAsync(cancellationToken);

        decimal Matrah(int yil, int ay, params FaturaTuru[] turler) =>
            aylik.Where(a => a.Year == yil && a.Month == ay && turler.Contains(a.Tur)).Sum(a => a.Matrah);

        decimal Kdv(int yil, int ay, params FaturaTuru[] turler) =>
            aylik.Where(a => a.Year == yil && a.Month == ay && turler.Contains(a.Tur)).Sum(a => a.Kdv);

        decimal SaticiKdvsi(int yil, int ay, FaturaTuru tur) =>
            aylik.Where(a => a.Year == yil && a.Month == ay && a.Tur == tur).Sum(a => a.Kdv - a.Tevkif);

        var sonAylar = Enumerable.Range(0, GrafikAySayisi)
            .Select(i => ilkAy.AddMonths(i))
            .Select(a => new AylikFaturaTutari(a.Year, a.Month,
                Para.Yuvarla(Matrah(a.Year, a.Month, FaturaTuru.Satis) - Matrah(a.Year, a.Month, FaturaTuru.SatistanIade)),
                Para.Yuvarla(Matrah(a.Year, a.Month, _alisTurleri) - Matrah(a.Year, a.Month, FaturaTuru.AlistanIade))))
            .ToList();

        var (y, m) = (ayBasi.Year, ayBasi.Month);
        var taslakSayisi = await db.Faturalar.CountAsync(f => f.Durum == FaturaDurumu.Taslak, cancellationToken);
        var sonFaturalar = await db.Faturalar.AsNoTracking()
            .Where(f => f.Durum != FaturaDurumu.Taslak)
            .OrderByDescending(f => f.Tarih).ThenByDescending(f => f.OlusturmaTarihi)
            .Take(OzetListeBoyutu)
            .Select(f => new FaturaOzeti(f.Id, f.Tur, f.SatisSekli, f.Durum, f.Numara, f.Tarih, f.VadeTarihi, f.CariHesapId, f.CariUnvan,
                f.ParaBirimi, f.GenelToplam, f.GenelToplamTl, null))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new FaturaOzetRaporu(
            sonAylar[^1].Satis,
            sonAylar[^1].Alis,
            Para.Yuvarla(SaticiKdvsi(y, m, FaturaTuru.Satis) - SaticiKdvsi(y, m, FaturaTuru.SatistanIade)),
            Para.Yuvarla(Kdv(y, m, _alisTurleri) - Kdv(y, m, FaturaTuru.AlistanIade)),
            aylik.Where(a => a.Year == y && a.Month == m).Sum(a => a.Sayi),
            taslakSayisi,
            sonAylar,
            sonFaturalar));
    }
}
