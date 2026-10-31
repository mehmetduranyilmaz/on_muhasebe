using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Modules.Fatura.Infrastructure;
using OnMuhasebe.Shared.Domain;

namespace OnMuhasebe.Modules.Fatura.Features;

internal static class FaturaRaporlari
{
    private const int OzetListeBoyutu = 5;
    private const int GrafikAySayisi = 6;

    public static void Map(RouteGroupBuilder grup) => grup.MapGet("/raporlar/ozet", OzetAsync);

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
