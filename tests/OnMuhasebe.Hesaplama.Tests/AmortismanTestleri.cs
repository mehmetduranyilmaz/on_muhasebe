using OnMuhasebe.Modules.KasaBanka.Contracts;

namespace OnMuhasebe.Hesaplama.Tests;

public class AmortismanTestleri
{
    [Fact]
    public void Normal_yontemde_esit_pay_ayrilir_son_yil_kalani_alir()
    {
        var plan = AmortismanHesaplayici.Plan(10_000m, new DateOnly(2024, 6, 15), 3, AmortismanYontemi.Normal, false);

        Assert.Equal([2024, 2025, 2026], plan.Select(p => p.Yil));
        Assert.Equal([3_333.33m, 3_333.33m, 3_333.34m], plan.Select(p => p.Tutar));
        Assert.Equal(0m, plan[^1].NetDeger);
    }

    [Fact]
    public void Azalan_bakiyelerde_oran_iki_kat_ve_en_cok_yuzde_elli()
    {
        Assert.Equal(0.4m, AmortismanHesaplayici.Oran(5, AmortismanYontemi.AzalanBakiyeler));
        Assert.Equal(0.5m, AmortismanHesaplayici.Oran(3, AmortismanYontemi.AzalanBakiyeler));

        var plan = AmortismanHesaplayici.Plan(10_000m, new DateOnly(2024, 1, 10), 5, AmortismanYontemi.AzalanBakiyeler, false);
        Assert.Equal([4_000m, 2_400m, 1_440m, 864m, 1_296m], plan.Select(p => p.Tutar));
        Assert.Equal(10_000m, plan.Sum(p => p.Tutar));
    }

    [Fact]
    public void Binek_otomobilde_ilk_yil_kist_kalan_omru_izleyen_yila_kalir()
    {
        // Eylülde alınan 600.000 TL'lik binek otomobil, 5 yıl: yıllık 120.000; ilk yıl 4 ay (Eylül dahil) = 40.000.
        var plan = AmortismanHesaplayici.Plan(600_000m, new DateOnly(2025, 9, 1), 5, AmortismanYontemi.Normal, true);

        Assert.Equal(6, plan.Count);
        Assert.Equal(40_000m, plan[0].Tutar);
        Assert.Equal(120_000m, plan[1].Tutar);
        Assert.Equal(80_000m, plan[^1].Tutar);
        Assert.Equal(2030, plan[^1].Yil);
    }

    [Fact]
    public void Elden_cikarilan_yilda_amortisman_ayrilmaz_ve_kar_zarar_net_degerden_bulunur()
    {
        var plan = AmortismanHesaplayici.Plan(10_000m, new DateOnly(2023, 3, 1), 5, AmortismanYontemi.Normal, false, new DateOnly(2025, 7, 1));

        Assert.Equal([2023, 2024], plan.Select(p => p.Yil));
        Assert.Equal(1_000m, AmortismanHesaplayici.ElCikarmaSonucu(7_000m, 10_000m, plan.Sum(p => p.Tutar)));
        Assert.Equal(-6_000m, AmortismanHesaplayici.ElCikarmaSonucu(0m, 10_000m, plan.Sum(p => p.Tutar)));
    }

    [Fact]
    public void Yil_ozeti_o_yilin_tutari_ve_yil_sonu_birikmisidir()
    {
        var plan = AmortismanHesaplayici.Plan(9_000m, new DateOnly(2024, 1, 1), 3, AmortismanYontemi.Normal, false);

        Assert.Equal((3_000m, 6_000m), AmortismanHesaplayici.Yil(plan, 2025));
        Assert.Equal((0m, 9_000m), AmortismanHesaplayici.Yil(plan, 2030));
    }
}
