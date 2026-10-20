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

    // Ana sayfa özeti: yalnızca onaylı faturalar; tutarlar TL ve KDV hariç, iadeler düşülmüş.
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
                Sayi = g.Count()
            })
            .ToListAsync(cancellationToken);

        decimal Tutar(int yil, int ay, FaturaTuru tur, bool kdvMi) =>
            aylik.Where(a => a.Year == yil && a.Month == ay && a.Tur == tur).Sum(a => kdvMi ? a.Kdv : a.Matrah);

        var sonAylar = Enumerable.Range(0, GrafikAySayisi)
            .Select(i => ilkAy.AddMonths(i))
            .Select(a => new AylikFaturaTutari(a.Year, a.Month,
                Para.Yuvarla(Tutar(a.Year, a.Month, FaturaTuru.Satis, false) - Tutar(a.Year, a.Month, FaturaTuru.SatistanIade, false)),
                Para.Yuvarla(Tutar(a.Year, a.Month, FaturaTuru.Alis, false) - Tutar(a.Year, a.Month, FaturaTuru.AlistanIade, false))))
            .ToList();

        var (y, m) = (ayBasi.Year, ayBasi.Month);
        var taslakSayisi = await db.Faturalar.CountAsync(f => f.Durum == FaturaDurumu.Taslak, cancellationToken);
        var sonFaturalar = await db.Faturalar.AsNoTracking()
            .Where(f => f.Durum != FaturaDurumu.Taslak)
            .OrderByDescending(f => f.Tarih).ThenByDescending(f => f.OlusturmaTarihi)
            .Take(OzetListeBoyutu)
            .Select(f => new FaturaOzeti(f.Id, f.Tur, f.SatisSekli, f.Durum, f.Numara, f.Tarih, f.VadeTarihi, f.CariHesapId, f.CariUnvan,
                f.ParaBirimi, f.GenelToplam, f.GenelToplamTl))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new FaturaOzetRaporu(
            sonAylar[^1].Satis,
            sonAylar[^1].Alis,
            Para.Yuvarla(Tutar(y, m, FaturaTuru.Satis, true) - Tutar(y, m, FaturaTuru.SatistanIade, true)),
            Para.Yuvarla(Tutar(y, m, FaturaTuru.Alis, true) - Tutar(y, m, FaturaTuru.AlistanIade, true)),
            aylik.Where(a => a.Year == y && a.Month == m).Sum(a => a.Sayi),
            taslakSayisi,
            sonAylar,
            sonFaturalar));
    }
}
