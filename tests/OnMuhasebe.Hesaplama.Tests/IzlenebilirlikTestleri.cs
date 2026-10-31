using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Modules.Stok.Domain;

namespace OnMuhasebe.Hesaplama.Tests;

public class IzlenebilirlikTestleri
{
    private const char Gs = '\u001d';

    [Fact]
    public void Gs1_karekod_gtin_skt_lot_ve_seriyi_ayristirir()
    {
        var kod = Gs1Ayrastirici.Ayristir($"01086999999999991727123110LOT42{Gs}21SN0001");

        Assert.NotNull(kod);
        Assert.Equal("08699999999999", kod.Gtin);
        Assert.Equal(new DateOnly(2027, 12, 31), kod.SonKullanmaTarihi);
        Assert.Equal("LOT42", kod.LotNo);
        Assert.Equal("SN0001", kod.SeriNo);
    }

    [Fact]
    public void Parantezli_bicim_ve_gun_00_ay_sonu_olarak_okunur()
    {
        var kod = Gs1Ayrastirici.Ayristir("(01)08699999999999(21)ABC123(17)260200(10)L1");

        Assert.NotNull(kod);
        Assert.Equal(new DateOnly(2026, 2, 28), kod.SonKullanmaTarihi);
        Assert.Equal("ABC123", kod.SeriNo);
        Assert.Equal("L1", kod.LotNo);
    }

    [Fact]
    public void Duz_ean_gs1_sayilmaz_ve_gtin14_ean13_adayi_uretir()
    {
        Assert.Null(Gs1Ayrastirici.Ayristir("8699999999999"));
        Assert.Equal(["08699999999999", "8699999999999"], Gs1Ayrastirici.BarkodAdaylari("08699999999999"));
    }

    [Fact]
    public void Seri_listesi_satir_ve_virgulle_ayrilir_tekrarlar_atilir()
    {
        Assert.Equal(["A1", "A2", "A3"], IzlenebilirlikKurallari.SeriListesi(" A1\nA2, A3;A1 "));
    }

    [Fact]
    public void Skt_durumu_uyari_esigine_gore_belirlenir()
    {
        var bugun = new DateOnly(2026, 10, 31);
        Assert.Equal(SktDurumu.Gecmis, IzlenebilirlikKurallari.Durum(bugun.AddDays(-1), bugun));
        Assert.Equal(SktDurumu.Yaklasiyor, IzlenebilirlikKurallari.Durum(bugun.AddDays(30), bugun));
        Assert.Equal(SktDurumu.Gecerli, IzlenebilirlikKurallari.Durum(bugun.AddDays(31), bugun));
        Assert.Equal(SktDurumu.Yok, IzlenebilirlikKurallari.Durum(null, bugun));
    }

    [Fact]
    public void Lot_izlenen_urunde_lot_ve_skt_takibinde_giris_skt_zorunludur()
    {
        var kart = Kart(IzlemeTuru.Lot, sktTakibi: true);

        Assert.False(Hareket(kart, StokHareketTuru.Acilis, 5m, new Izlenebilirlik(null, null, null)).BasariliMi);
        Assert.False(Hareket(kart, StokHareketTuru.Acilis, 5m, new Izlenebilirlik("L1", null, null)).BasariliMi);
        var giris = Hareket(kart, StokHareketTuru.Acilis, 5m, new Izlenebilirlik(" l1 ", new DateOnly(2027, 1, 1), null));
        Assert.True(giris.BasariliMi);
        Assert.Equal("L1", giris.Deger.LotNo);

        // Çıkışta SKT istenmez; lotun tarihi sonradan işlenir.
        Assert.True(Hareket(kart, StokHareketTuru.Fire, 1m, new Izlenebilirlik("L1", null, null)).BasariliMi);
    }

    [Fact]
    public void Seri_izlenen_urunde_seri_sayisi_miktara_esit_olmali()
    {
        var kart = Kart(IzlemeTuru.Seri, sktTakibi: false);

        Assert.False(Hareket(kart, StokHareketTuru.Acilis, 2m, new Izlenebilirlik(null, null, ["S1"])).BasariliMi);
        Assert.False(Hareket(kart, StokHareketTuru.Acilis, 2m, new Izlenebilirlik(null, null, ["S1", "S1"])).BasariliMi);
        var giris = Hareket(kart, StokHareketTuru.Acilis, 2m, new Izlenebilirlik(null, null, ["S1", "S2"]));
        Assert.True(giris.BasariliMi);
        Assert.Equal(2, giris.Deger.Seriler.Count);

        var ters = giris.Deger.IptalEt();
        Assert.Equal(["S1", "S2"], ters.Deger.Seriler.Select(s => s.SeriNo));
    }

    [Fact]
    public void Izlenmeyen_urunde_lot_bilgisi_atilir_ve_hizmet_izlenemez()
    {
        var kart = Kart(IzlemeTuru.Yok, sktTakibi: false);
        var hareket = Hareket(kart, StokHareketTuru.Acilis, 1m, new Izlenebilirlik("L1", null, ["X"]));
        Assert.Null(hareket.Deger.LotNo);
        Assert.Empty(hareket.Deger.Seriler);

        Assert.False(StokKarti.Olustur("H1", Bilgiler(StokTuru.Hizmet, IzlemeTuru.Lot, false), []).BasariliMi);
        Assert.False(StokKarti.Olustur("U1", Bilgiler(StokTuru.Urun, IzlemeTuru.Yok, true), []).BasariliMi);
    }

    private static StokKarti Kart(IzlemeTuru izleme, bool sktTakibi) =>
        StokKarti.Olustur("K1", Bilgiler(StokTuru.Urun, izleme, sktTakibi), []).Deger;

    private static StokKartiBilgileri Bilgiler(StokTuru tur, IzlemeTuru izleme, bool sktTakibi) =>
        new("Ürün", tur, "ADET", null, 20, null, null, null, "TRY", null, null, null, izleme, sktTakibi);

    private static OnMuhasebe.Shared.Domain.Results.Sonuc<StokHareket> Hareket(StokKarti kart, StokHareketTuru tur, decimal miktar, Izlenebilirlik izleme) =>
        StokHareket.Olustur(kart, Guid.NewGuid(), tur, new DateOnly(2026, 10, 31), "ADET", miktar, 10m, null, null, izleme: izleme);
}
