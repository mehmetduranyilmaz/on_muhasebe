using OnMuhasebe.Modules.KasaBanka.Contracts;

namespace OnMuhasebe.Hesaplama.Tests;

public class KrediPlaniTestleri
{
    private static readonly DateOnly _ilk = new(2026, 12, 15);

    [Fact]
    public void Esit_taksitte_anapara_toplami_tutara_esittir_ve_faiz_kalan_anaparadan_hesaplanir()
    {
        // 100.000, aylık %3, 12 taksit: aylık taksit ≈ 10.046,21; ilk faiz 3.000
        var plan = KrediPlaniHesaplayici.EsitTaksit(100_000m, 3m, 12, _ilk, 5m);

        Assert.Equal(12, plan.Count);
        Assert.Equal(100_000m, plan.Sum(t => t.Anapara));
        Assert.Equal((3_000m, 150m), (plan[0].Faiz, plan[0].Vergi));
        Assert.Equal(10_046.21m, plan[0].Anapara + plan[0].Faiz);
        Assert.All(plan.Take(11), t => Assert.Equal(10_046.21m, t.Anapara + t.Faiz));
        Assert.Equal(new DateOnly(2027, 11, 15), plan[^1].Vade);
    }

    [Fact]
    public void Faizsiz_planda_anapara_esit_bolunur()
    {
        var plan = KrediPlaniHesaplayici.EsitTaksit(1_000m, 0m, 3, _ilk, 5m);

        Assert.Equal([333.33m, 333.33m, 333.34m], plan.Select(t => t.Anapara));
        Assert.All(plan, t => Assert.Equal((0m, 0m), (t.Faiz, t.Vergi)));
    }

    [Fact]
    public void Ay_sonu_vadeleri_ayin_son_gunune_kayar()
    {
        var plan = KrediPlaniHesaplayici.EsitTaksit(1_000m, 2m, 3, new DateOnly(2027, 1, 31), 0m);

        Assert.Equal([new DateOnly(2027, 1, 31), new DateOnly(2027, 2, 28), new DateOnly(2027, 3, 31)], plan.Select(t => t.Vade));
    }

    [Fact]
    public void Plan_dogrulamasi_anapara_toplamini_ve_vade_sirasini_denetler()
    {
        var kullandirim = new DateOnly(2026, 11, 15);
        var plan = KrediPlaniHesaplayici.EsitTaksit(1_000m, 2m, 2, _ilk, 5m);

        Assert.Null(KrediPlaniHesaplayici.Hata(1_000m, kullandirim, plan));
        Assert.NotNull(KrediPlaniHesaplayici.Hata(1_001m, kullandirim, plan));
        Assert.NotNull(KrediPlaniHesaplayici.Hata(1_000m, _ilk, plan));
        Assert.NotNull(KrediPlaniHesaplayici.Hata(1_000m, kullandirim, [plan[1] with { Sira = 1 }, plan[0] with { Sira = 2 }]));
        Assert.NotNull(KrediPlaniHesaplayici.Hata(1_000m, kullandirim, []));
    }
}
