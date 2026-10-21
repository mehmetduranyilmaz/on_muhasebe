using OnMuhasebe.Modules.KasaBanka.Contracts;

namespace OnMuhasebe.Hesaplama.Tests;

public class SeriGirisTestleri
{
    [Fact]
    public void Aylik_vadeler_ay_sonunda_kaymaz()
    {
        var plan = SeriGiris.Plan(new DateOnly(2027, 1, 31), 4, VadeAraligi.Aylik, 1000, toplamiBol: false);

        Assert.Equal([new DateOnly(2027, 1, 31), new DateOnly(2027, 2, 28), new DateOnly(2027, 3, 31), new DateOnly(2027, 4, 30)], plan.Select(p => p.Vade));
        Assert.All(plan, p => Assert.Equal(1000m, p.Tutar));
    }

    [Fact]
    public void Haftalik_ve_iki_haftalik_vadeler()
    {
        Assert.Equal(new DateOnly(2026, 11, 15), SeriGiris.Plan(new DateOnly(2026, 11, 1), 3, VadeAraligi.Haftalik, 1, false)[2].Vade);
        Assert.Equal(new DateOnly(2026, 11, 29), SeriGiris.Plan(new DateOnly(2026, 11, 1), 3, VadeAraligi.IkiHaftalik, 1, false)[2].Vade);
    }

    [Fact]
    public void Toplam_bolunurken_kurus_farki_son_evraka_eklenir()
    {
        var plan = SeriGiris.Plan(new DateOnly(2026, 11, 1), 3, VadeAraligi.Aylik, 1000, toplamiBol: true);

        Assert.Equal([333.33m, 333.33m, 333.34m], plan.Select(p => p.Tutar));
        Assert.Equal(1000m, plan.Sum(p => p.Tutar));
    }
}
