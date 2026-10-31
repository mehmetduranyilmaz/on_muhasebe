using OnMuhasebe.Modules.Cari.Contracts;

namespace OnMuhasebe.Hesaplama.Tests;

public class AvansTestleri
{
    [Fact]
    public void Fazla_tahsilat_ve_fazla_odeme_avanstir()
    {
        Assert.True(CariHareketTurleri.AvansMi(CariHareketTuru.Tahsilat, HareketYonu.Alacak));
        Assert.True(CariHareketTurleri.AvansMi(CariHareketTuru.CekSenetGirisi, HareketYonu.Alacak));
        Assert.True(CariHareketTurleri.AvansMi(CariHareketTuru.Odeme, HareketYonu.Borc));
        Assert.True(CariHareketTurleri.AvansMi(CariHareketTuru.CekSenetCikisi, HareketYonu.Borc));
        Assert.False(CariHareketTurleri.AvansMi(CariHareketTuru.AlisFaturasi, HareketYonu.Alacak));
        Assert.False(CariHareketTurleri.AvansMi(CariHareketTuru.SatisFaturasi, HareketYonu.Borc));
        Assert.False(CariHareketTurleri.AvansMi(CariHareketTuru.KarsiliksizCekSenet, HareketYonu.Borc));
    }
}
