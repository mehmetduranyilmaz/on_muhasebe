using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Modules.Stok.Domain;

namespace OnMuhasebe.Hesaplama.Tests;

public class StokHareketTestleri
{
    private static readonly DateOnly _tarih = new(2026, 10, 20);
    private static readonly Guid _depo = Guid.NewGuid();

    private static StokKartiBilgileri Bilgiler(StokTuru tur = StokTuru.Urun, string temelBirim = "ADET") =>
        new("Su 0,5 L", tur, temelBirim, null, 20, null, null, null, "TRY", null, null, null);

    private static StokKarti Kart(StokTuru tur = StokTuru.Urun) =>
        StokKarti.Olustur("S00001", Bilgiler(tur), [new EkBirim("KOLI", 12m, "8690000000012")]).Deger;

    [Fact]
    public void Ek_birimle_giris_temel_birime_cevrilir()
    {
        // 2 koli × 12 = 24 adet; koli fiyatı 120 TL -> adet maliyeti 10 TL
        var h = StokHareket.Olustur(Kart(), _depo, StokHareketTuru.Acilis, _tarih, "KOLI", 2m, 120m, null, null).Deger;

        Assert.Equal((24m, 0m, 10m), (h.GirisMiktari, h.CikisMiktari, h.BirimFiyat));
        Assert.Equal(("KOLI", 2m), (h.GirilenBirim, h.GirilenMiktar));
    }

    [Fact]
    public void Cikis_hareketi_fiyat_tasimaz()
    {
        var h = StokHareket.Olustur(Kart(), _depo, StokHareketTuru.Fire, _tarih, "ADET", 3m, 999m, null, null).Deger;

        Assert.Equal((0m, 3m), (h.GirisMiktari, h.CikisMiktari));
        Assert.Null(h.BirimFiyat);
    }

    [Theory]
    [InlineData(StokHareketTuru.Acilis, "ADET", 2.5, 10.0, "kesirli_miktar")]
    [InlineData(StokHareketTuru.Acilis, "KG", 1.0, 10.0, "birim_tanimsiz")]
    [InlineData(StokHareketTuru.Acilis, "ADET", 0.0, 10.0, "miktar_gecersiz")]
    [InlineData(StokHareketTuru.SayimFazlasi, "ADET", 1.0, null, "maliyet_gerekli")]
    public void Gecersiz_hareket_reddedilir(StokHareketTuru tur, string birim, double miktar, double? fiyat, string hataKodu)
    {
        var sonuc = StokHareket.Olustur(Kart(), _depo, tur, _tarih, birim, (decimal)miktar, (decimal?)fiyat, null, null);

        Assert.Equal(hataKodu, sonuc.Hata!.Kod);
    }

    [Fact]
    public void Transfer_girisi_maliyet_istemez()
    {
        var sonuc = StokHareket.Olustur(Kart(), _depo, StokHareketTuru.TransferGiris, _tarih, "ADET", 5m, null, null, null, Guid.NewGuid());

        Assert.True(sonuc.BasariliMi);
        Assert.Equal(5m, sonuc.Deger.GirisMiktari);
    }

    [Fact]
    public void Hizmet_kartina_stok_hareketi_girilemez() =>
        Assert.Equal("hizmet_karti",
            StokHareket.Olustur(Kart(StokTuru.Hizmet), _depo, StokHareketTuru.Acilis, _tarih, "ADET", 1m, 1m, null, null).Hata!.Kod);

    [Fact]
    public void Iptal_giris_ve_cikisi_yer_degistirir()
    {
        var asil = StokHareket.Olustur(Kart(), _depo, StokHareketTuru.Acilis, _tarih, "ADET", 7m, 4m, null, "x").Deger;

        var ters = asil.IptalEt().Deger;

        Assert.Equal((0m, 7m), (ters.GirisMiktari, ters.CikisMiktari));
        Assert.Equal(asil.Id, ters.IptalEdilenHareketId);
        Assert.Equal("zaten_iptal", asil.IptalEt().Hata!.Kod);
    }

    [Theory]
    [InlineData("ADET", 2.0)] // temel birimle aynı ek birim
    [InlineData("KOLI", 0.0)] // sıfır katsayı
    [InlineData("YOK", 2.0)]  // tanımsız birim
    public void Gecersiz_ek_birim_reddedilir(string birim, double katsayi) =>
        Assert.Equal("ek_birim_gecersiz",
            StokKarti.Olustur("S1", Bilgiler(), [new EkBirim(birim, (decimal)katsayi, null)]).Hata!.Kod);

    [Fact]
    public void Hareketi_olan_kartin_temel_birimi_degismez()
    {
        var kart = Kart();

        Assert.Equal("temel_birim_degistirilemez", kart.Guncelle("S1", Bilgiler(temelBirim: "KG"), [], hareketVarMi: true).Hata!.Kod);
        Assert.True(kart.Guncelle("S1", Bilgiler(temelBirim: "KG"), [], hareketVarMi: false).BasariliMi);
        Assert.Equal(1m, kart.Katsayi("KG"));
        Assert.Null(kart.Katsayi("KOLI"));
    }
}
