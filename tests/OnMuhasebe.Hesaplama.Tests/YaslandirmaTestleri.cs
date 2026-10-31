using OnMuhasebe.Modules.Cari.Contracts;

namespace OnMuhasebe.Hesaplama.Tests;

public class YaslandirmaTestleri
{
    [Theory]
    [InlineData(-10, YasDilimi.VadesiGelmemis)]
    [InlineData(0, YasDilimi.VadesiGelmemis)]
    [InlineData(1, YasDilimi.Gun1_30)]
    [InlineData(30, YasDilimi.Gun1_30)]
    [InlineData(31, YasDilimi.Gun31_60)]
    [InlineData(60, YasDilimi.Gun31_60)]
    [InlineData(61, YasDilimi.Gun61_90)]
    [InlineData(90, YasDilimi.Gun61_90)]
    [InlineData(91, YasDilimi.Gun90Ustu)]
    public void Gecikme_gunu_dogru_dilime_duser(int gun, YasDilimi beklenen) => Assert.Equal(beklenen, YaslandirmaHesaplayici.Dilim(gun));

    [Fact]
    public void Kalemler_dilimlere_toplanir()
    {
        var d = YaslandirmaHesaplayici.Dilimle([(100m, -3), (50m, 0), (200m, 15), (300m, 45), (400m, 75), (500m, 120), (25m, 91)]);

        Assert.Equal([150m, 200m, 300m, 400m, 525m], d);
    }

    [Fact]
    public void Ortalama_gecikme_yalnizca_vadesi_gecmis_kisimdan_tutar_agirlikli_hesaplanir()
    {
        // (100×10 + 300×50) / 400 = 40; vadesi gelmemiş 500 hesaba katılmaz
        Assert.Equal(40, YaslandirmaHesaplayici.OrtalamaGecikme([(100m, 10), (300m, 50), (500m, -5)]));
        Assert.Equal(0, YaslandirmaHesaplayici.OrtalamaGecikme([(500m, -5)]));
    }

    [Fact]
    public void Fazla_tahsilat_ve_fazla_odeme_avanstir()
    {
        Assert.True(CariHareketTurleri.AvansMi(CariHareketTuru.Tahsilat, HareketYonu.Alacak));
        Assert.True(CariHareketTurleri.AvansMi(CariHareketTuru.CekSenetGirisi, HareketYonu.Alacak));
        Assert.True(CariHareketTurleri.AvansMi(CariHareketTuru.Odeme, HareketYonu.Borc));
        Assert.False(CariHareketTurleri.AvansMi(CariHareketTuru.AlisFaturasi, HareketYonu.Alacak));
        Assert.False(CariHareketTurleri.AvansMi(CariHareketTuru.SatisFaturasi, HareketYonu.Borc));
        Assert.False(CariHareketTurleri.AvansMi(CariHareketTuru.KarsiliksizCekSenet, HareketYonu.Borc));
    }
}
