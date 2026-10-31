using OnMuhasebe.Modules.Fatura.Contracts;

namespace OnMuhasebe.Hesaplama.Tests;

public class BaBsTestleri
{
    [Fact]
    public void Iadeler_karsi_formda_nihai_tuketici_ve_gider_pusulasi_disarida()
    {
        Assert.True(BaBsHesaplayici.BaMi(FaturaTuru.Alis, false));
        Assert.True(BaBsHesaplayici.BaMi(FaturaTuru.SatistanIade, false));
        Assert.True(BaBsHesaplayici.BaMi(FaturaTuru.SerbestMeslekMakbuzu, false));
        Assert.False(BaBsHesaplayici.BaMi(FaturaTuru.Satis, false));
        Assert.False(BaBsHesaplayici.BaMi(FaturaTuru.AlistanIade, false));
        Assert.Null(BaBsHesaplayici.BaMi(FaturaTuru.Satis, true));
        Assert.Null(BaBsHesaplayici.BaMi(FaturaTuru.GiderPusulasi, false));
    }

    [Fact]
    public void Ayni_vkn_tek_satir_sinir_altindakiler_atilir_tutar_kurussuz()
    {
        var cari1 = Guid.NewGuid();
        var cari2 = Guid.NewGuid();
        BaBsBelgesi[] belgeler =
        [
            new(false, "1234567890", cari1, "A Ltd.", 3_000.75m),
            new(false, "1234567890", cari2, "A Ltd. (şube)", 2_000.50m),
            new(false, "9999999999", Guid.NewGuid(), "B AŞ", 4_999.99m),
            new(true, "1234567890", cari1, "A Ltd.", 10_000m)
        ];

        var bs = BaBsHesaplayici.Ozet(belgeler, false, 5_000m);
        var satir = Assert.Single(bs);
        Assert.Equal(2, satir.BelgeSayisi);
        Assert.Equal(5_001m, satir.Tutar);

        Assert.Single(BaBsHesaplayici.Ozet(belgeler, true, 5_000m));
    }

    [Fact]
    public void Vergi_numarasi_olmayan_cari_ayri_satir_ve_eksik_isaretli()
    {
        var bs = BaBsHesaplayici.Ozet([new BaBsBelgesi(false, null, Guid.NewGuid(), "C", 6_000m)], false, 5_000m);
        Assert.True(Assert.Single(bs).VergiNoEksik);
    }
}
