using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Modules.Stok.Contracts;

namespace OnMuhasebe.Hesaplama.Tests;

public class SiparisKarsilamaTestleri
{
    [Fact]
    public void Hic_karsilanmayan_siparis_bekliyordur()
    {
        Assert.Equal(KarsilamaDurumu.Bekliyor, TeklifSiparisHesaplari.Karsilama([(10m, 0m), (5m, 0m)]));
    }

    [Fact]
    public void Bir_satiri_eksik_kalan_siparis_kismendir()
    {
        Assert.Equal(KarsilamaDurumu.Kismen, TeklifSiparisHesaplari.Karsilama([(10m, 10m), (5m, 2m)]));
    }

    [Fact]
    public void Fazla_teslim_de_tamamlanmis_sayilir()
    {
        Assert.Equal(KarsilamaDurumu.Tamamlandi, TeklifSiparisHesaplari.Karsilama([(10m, 10.5m), (5m, 5m)]));
    }

    [Fact]
    public void Siparis_yalnizca_satis_ve_alis_belgesiyle_karsilanir()
    {
        Assert.Equal(TeklifSiparisTuru.SatisSiparisi, TeklifSiparisTurleri.SiparisTuru(FaturaTuru.Satis));
        Assert.Equal(TeklifSiparisTuru.SatinalmaSiparisi, TeklifSiparisTurleri.SiparisTuru(FaturaTuru.Alis));
        Assert.Null(TeklifSiparisTurleri.SiparisTuru(FaturaTuru.SatistanIade));
        Assert.Null(TeklifSiparisTurleri.SiparisTuru(FaturaTuru.GiderPusulasi));
    }

    [Fact]
    public void Ek_birim_temel_birime_katsayiyla_cevrilir()
    {
        var kart = new StokKartiBilgisi(Guid.NewGuid(), "K1", "Kalem", StokTuru.Urun, "ADET", [new EkBirim("KOLI", 12m, null)], 20, true);

        Assert.Equal(1m, kart.Katsayi("ADET"));
        Assert.Equal(12m, kart.Katsayi("KOLI"));
        Assert.Equal(1m, kart.Katsayi("PAKET"));
    }
}
