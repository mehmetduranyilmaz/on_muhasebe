using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.Cari.Domain;
using OnMuhasebe.Shared.Domain;

namespace OnMuhasebe.Hesaplama.Tests;

public class CariHareketTestleri
{
    private static readonly DateOnly _tarih = new(2026, 10, 20);

    internal static CariHesap Cari(string paraBirimi = "TRY") => CariHesap.Olustur("C00001", new CariBilgileri(
        "Test Ltd", CariTuru.Musteri, KisiTipi.Tuzel, null, null, null, null, null, null, null,
        paraBirimi, null, null, null, null, null, null, false, null));

    private static CariHareket Hareket(CariHesap cari, HareketYonu yon, decimal tutar, decimal kur) =>
        CariHareket.Olustur(cari, CariHareketTuru.Dekont, yon, _tarih, null, tutar, kur, null, "test").Deger;

    [Fact]
    public void Borc_hareketi_yalniz_borc_kolonlarini_doldurur()
    {
        var h = Hareket(Cari(), HareketYonu.Borc, 1500m, 1m);

        Assert.Equal((1500m, 0m, 1500m, 0m), (h.Borc, h.Alacak, h.BorcTl, h.AlacakTl));
    }

    [Fact]
    public void Dovizli_hareketin_tl_karsiligi_kurla_hesaplanip_kurusa_yuvarlanir()
    {
        // 33,33 USD x 49,4889 = 1649,465037 TL -> 1649,47
        var h = Hareket(Cari("USD"), HareketYonu.Alacak, 33.33m, 49.4889m);

        Assert.Equal(33.33m, h.Alacak);
        Assert.Equal(1649.47m, h.AlacakTl);
        Assert.Equal("USD", h.ParaBirimi);
    }

    [Fact]
    public void Yarim_kurus_yukari_yuvarlanir() =>
        // Bankacı yuvarlaması 0,12 verirdi; muhasebede 0,125 -> 0,13
        Assert.Equal(0.13m, Para.Yuvarla(0.125m));

    [Fact]
    public void Tutar_kurusa_yuvarlanarak_kaydedilir() =>
        Assert.Equal(10.01m, Hareket(Cari(), HareketYonu.Borc, 10.005m, 1m).Borc);

    [Theory]
    [InlineData(0, 1, "tutar_gecersiz")]
    [InlineData(-5, 1, "tutar_gecersiz")]
    [InlineData(100, 1.2, "tl_kuru")]
    public void Gecersiz_tl_hareketi_reddedilir(decimal tutar, decimal kur, string hataKodu)
    {
        var sonuc = CariHareket.Olustur(Cari(), CariHareketTuru.Dekont, HareketYonu.Borc, _tarih, null, tutar, kur, null, null);

        Assert.False(sonuc.BasariliMi);
        Assert.Equal(hataKodu, sonuc.Hata!.Kod);
    }

    [Fact]
    public void Dovizli_hareket_sifir_kurla_reddedilir() =>
        Assert.Equal("kur_gecersiz",
            CariHareket.Olustur(Cari("EUR"), CariHareketTuru.Dekont, HareketYonu.Borc, _tarih, null, 10m, 0m, null, null).Hata!.Kod);

    [Fact]
    public void Vade_islem_tarihinden_once_olamaz() =>
        Assert.Equal("vade_gecersiz",
            CariHareket.Olustur(Cari(), CariHareketTuru.Dekont, HareketYonu.Borc, _tarih, _tarih.AddDays(-1), 10m, 1m, null, null).Hata!.Kod);

    [Fact]
    public void Pasif_cariye_hareket_girilemez()
    {
        var cari = Cari();
        cari.Pasiflestir();

        Assert.Equal("pasif_cari",
            CariHareket.Olustur(cari, CariHareketTuru.Dekont, HareketYonu.Borc, _tarih, null, 10m, 1m, null, null).Hata!.Kod);
    }

    [Fact]
    public void Iptal_ayni_tarih_ve_kurla_ters_kayit_uretir()
    {
        var asil = Hareket(Cari("USD"), HareketYonu.Borc, 100m, 49.5m);

        var ters = asil.IptalEt().Deger;

        Assert.Equal((0m, 100m, 0m, 4950m), (ters.Borc, ters.Alacak, ters.BorcTl, ters.AlacakTl));
        Assert.Equal((asil.Tarih, asil.Kur), (ters.Tarih, ters.Kur));
        Assert.Equal(asil.Id, ters.IptalEdilenHareketId);
        Assert.Equal(ters.Id, asil.IptalHareketId);
        Assert.Equal(0m, asil.Borc - asil.Alacak + ters.Borc - ters.Alacak);
    }

    [Fact]
    public void Iptal_edilmis_hareket_ve_iptal_kaydi_tekrar_iptal_edilemez()
    {
        var asil = Hareket(Cari(), HareketYonu.Borc, 100m, 1m);
        var ters = asil.IptalEt().Deger;

        Assert.Equal("zaten_iptal", asil.IptalEt().Hata!.Kod);
        Assert.Equal("iptal_kaydi", ters.IptalEt().Hata!.Kod);
    }

    [Fact]
    public void Hareketi_olan_carinin_para_birimi_degistirilemez()
    {
        var cari = Cari("USD");
        var bilgiler = new CariBilgileri("Test Ltd", CariTuru.Musteri, KisiTipi.Tuzel, null, null, null, null, null, null,
            null, "EUR", null, null, null, null, null, null, false, null);

        Assert.Equal("para_birimi_degistirilemez", cari.Guncelle("C00001", bilgiler, hareketVarMi: true).Hata!.Kod);
        Assert.True(cari.Guncelle("C00001", bilgiler, hareketVarMi: false).BasariliMi);
        Assert.Equal("EUR", cari.ParaBirimi);
    }
}
