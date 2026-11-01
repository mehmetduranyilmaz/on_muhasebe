using OnMuhasebe.Modules.Arac.Contracts;
using OnMuhasebe.Modules.Arac.Domain;

namespace OnMuhasebe.Hesaplama.Tests;

public class AracTestleri
{
    private static readonly DateOnly Bugun = new(2026, 11, 1);

    private static YukumlulukKaydetIstegi Yukumluluk(DateOnly bitis, DateOnly? baslangic = null) =>
        new(YukumlulukTuru.Kasko, null, baslangic, bitis, "P-1", "Sigortaci", 1000, 30);

    [Theory]
    [InlineData("34 abc 123", "34ABC123")]
    [InlineData("06-a-1234", "06A1234")]
    [InlineData(" 35 kz 99 ", "35KZ99")]
    [InlineData("34 ıj 12", "34IJ12")]
    public void Plaka_normallesir(string girdi, string beklenen) => Assert.Equal(beklenen, Plakalar.Normalize(girdi));

    [Theory]
    [InlineData("34ABC123", true)]
    [InlineData("01A1234", true)]
    [InlineData("81AB12", true)]
    [InlineData("00ABC123", false)]
    [InlineData("82ABC123", false)]
    [InlineData("34ABCD123", false)]
    [InlineData("34ABC12345", false)]
    [InlineData("ABC123", false)]
    [InlineData("", false)]
    public void Turk_plakasi_denetlenir(string plaka, bool gecerli) => Assert.Equal(gecerli, Plakalar.GecerliMi(plaka, AracTuru.Binek));

    [Fact]
    public void Is_makinesinde_tescil_no_serbesttir()
    {
        Assert.True(Plakalar.GecerliMi("TESCIL7788", AracTuru.IsMakinesi));
        Assert.False(Plakalar.GecerliMi("", AracTuru.IsMakinesi));
    }

    [Theory]
    [InlineData(31, SureDurumu.Gecerli)]
    [InlineData(30, SureDurumu.Yaklasiyor)]
    [InlineData(0, SureDurumu.Yaklasiyor)]
    [InlineData(-1, SureDurumu.SuresiDoldu)]
    public void Sure_durumu(int kalan, SureDurumu beklenen) => Assert.Equal(beklenen, AracHesaplari.Durum(Bugun.AddDays(kalan), 30, Bugun));

    [Fact]
    public void Yenileme_yeni_kayit_acar_eskisi_gecmise_gecer()
    {
        var eski = AracYukumlulugu.Olustur(Guid.NewGuid(), Yukumluluk(Bugun.AddDays(5))).Deger;

        var yeni = eski.Yenile(Yukumluluk(Bugun.AddDays(370)) with { Tur = YukumlulukTuru.Muayene });

        Assert.True(yeni.BasariliMi);
        Assert.True(eski.YenilendiMi);
        Assert.Equal(YukumlulukTuru.Kasko, yeni.Deger.Tur);
        Assert.Equal(eski.AracId, yeni.Deger.AracId);
        Assert.False(yeni.Deger.YenilendiMi);
    }

    [Fact]
    public void Yeni_bitis_eskisinden_sonra_olmali_ve_iki_kez_yenilenemez()
    {
        var kayit = AracYukumlulugu.Olustur(Guid.NewGuid(), Yukumluluk(Bugun.AddDays(5))).Deger;

        Assert.False(kayit.Yenile(Yukumluluk(Bugun.AddDays(5))).BasariliMi);
        Assert.False(kayit.YenilendiMi);

        kayit.Yenile(Yukumluluk(Bugun.AddDays(100)));
        Assert.False(kayit.Yenile(Yukumluluk(Bugun.AddDays(200))).BasariliMi);
    }

    [Fact]
    public void Bitis_baslangictan_once_olamaz() =>
        Assert.False(AracYukumlulugu.Olustur(Guid.NewGuid(), Yukumluluk(Bugun, Bugun.AddDays(1))).BasariliMi);

    [Fact]
    public void Mtv_son_odeme_varsayilani_ocak_ve_temmuz_sonudur()
    {
        Assert.Equal(new DateOnly(2026, 1, 31), AracMtv.Olustur(Guid.NewGuid(), new MtvKaydetIstegi(2026, 1, 100, null)).SonOdemeTarihi);
        Assert.Equal(new DateOnly(2026, 7, 31), AracMtv.Olustur(Guid.NewGuid(), new MtvKaydetIstegi(2026, 2, 100, null)).SonOdemeTarihi);
        Assert.Equal(new DateOnly(2026, 2, 28), AracMtv.Olustur(Guid.NewGuid(), new MtvKaydetIstegi(2026, 1, 100, new DateOnly(2026, 2, 28))).SonOdemeTarihi);
    }

    [Fact]
    public void Km_yalniz_ileri_gider()
    {
        var arac = AracKarti.Olustur(new AracKaydetIstegi("34ABC123", "M", "M", null, AracTuru.Binek, YakitTuru.Dizel, null, null, null, AracMulkiyeti.OzMal, null, null, null,
            1000, AracDurumu.Aktif, null, null, null));

        arac.KmIlerlet(1500);
        Assert.Equal(1500, arac.GuncelKm);
        arac.KmIlerlet(1200);
        arac.KmIlerlet(null);
        Assert.Equal(1500, arac.GuncelKm);
    }

    [Fact]
    public void Yakit_disinda_litre_tutulmaz()
    {
        var m = AracMasrafi.Olustur(Guid.NewGuid(), new MasrafKaydetIstegi(AracMasrafTuru.Otopark, Bugun, 50, null, 10, null, null, null, null));

        Assert.Null(m.Litre);
    }

    [Fact]
    public void Donem_km_en_buyuk_ve_kucuk_farkidir_tek_kayittan_cikmaz()
    {
        Assert.Equal(1000, AracHesaplari.DonemKm([10000, 11000, 10500]));
        Assert.Null(AracHesaplari.DonemKm([10000]));
        Assert.Null(AracHesaplari.DonemKm([]));
        Assert.Null(AracHesaplari.DonemKm([10000, 10000]));
    }

    [Fact]
    public void Tuketim_ilk_dolumu_saymaz()
    {
        // 10000'de dolum, sonra 500 km'de 30 L ve 500 km'de 40 L: 70 L / 1000 km.
        Assert.Equal(7m, AracHesaplari.Tuketim([(10000, 50m), (10500, 30m), (11000, 40m)]));
    }

    [Fact]
    public void Tuketim_kayit_sirasina_bakmaz_yetersiz_veride_bostur()
    {
        Assert.Equal(7m, AracHesaplari.Tuketim([(11000, 40m), (10000, 50m), (10500, 30m)]));
        Assert.Null(AracHesaplari.Tuketim([(10000, 50m)]));
        Assert.Null(AracHesaplari.Tuketim([(10000, 50m), (10000, 30m)]));
    }

    [Fact]
    public void Km_basina_maliyet_km_yoksa_bostur()
    {
        Assert.Equal(2.5m, AracHesaplari.KmBasina(2500, 1000));
        Assert.Null(AracHesaplari.KmBasina(2500, null));
        Assert.Null(AracHesaplari.KmBasina(2500, 0));
    }
}
