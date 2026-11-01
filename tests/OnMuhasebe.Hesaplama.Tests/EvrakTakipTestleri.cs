using OnMuhasebe.Modules.EvrakTakip.Contracts;
using OnMuhasebe.Modules.EvrakTakip.Domain;

namespace OnMuhasebe.Hesaplama.Tests;

public class EvrakTakipTestleri
{
    private static readonly DateOnly Bugun = new(2026, 11, 1);

    private static TakipEvrakiKaydetIstegi Istek(DateOnly bitis, DateOnly? baslangic = null, string? belgeNo = "P-1") =>
        new("Trafik Sigortası", TakipEvrakTuru.Sigorta, belgeNo, null, baslangic, bitis, 30, null, null, null);

    [Theory]
    [InlineData(31, TakipEvrakDurumu.Gecerli)]
    [InlineData(30, TakipEvrakDurumu.Yaklasiyor)]
    [InlineData(0, TakipEvrakDurumu.Yaklasiyor)]
    [InlineData(-1, TakipEvrakDurumu.SuresiDoldu)]
    public void Durum_bitise_kalan_gunden_bulunur(int kalan, TakipEvrakDurumu beklenen) =>
        Assert.Equal(beklenen, TakipEvrakTurleri.Durum(true, Bugun.AddDays(kalan), 30, Bugun));

    [Fact]
    public void Pasif_evrak_tarihine_bakilmadan_pasiftir() =>
        Assert.Equal(TakipEvrakDurumu.Pasif, TakipEvrakTurleri.Durum(false, Bugun.AddDays(-10), 30, Bugun));

    [Theory]
    [InlineData(31, null)]
    [InlineData(30, "yaklasiyor")]
    [InlineData(8, "yaklasiyor")]
    [InlineData(7, "son")]
    [InlineData(0, "son")]
    [InlineData(-1, "doldu")]
    [InlineData(-30, "doldu")]
    [InlineData(-31, null)]
    public void Hatirlatma_asamasi(int kalan, string? beklenen) =>
        Assert.Equal(beklenen, TakipHatirlatma.Asama(Bugun.AddDays(kalan), 30, Bugun));

    [Fact]
    public void Kisa_hatirlatmada_son_uyari_gunu_gecerlidir() =>
        Assert.Equal("son", TakipHatirlatma.Asama(Bugun.AddDays(3), 5, Bugun));

    [Fact]
    public void Bitis_baslangictan_once_olamaz() =>
        Assert.False(TakipEvraki.Olustur(Istek(Bugun, Bugun.AddDays(1))).BasariliMi);

    [Fact]
    public void Yenileme_eski_bilgileri_gecmise_yazar()
    {
        var evrak = TakipEvraki.Olustur(Istek(Bugun.AddDays(5))).Deger;

        var sonuc = evrak.Yenile(new TakipYenilemeIstegi(Bugun.AddDays(370), Bugun.AddDays(5), "P-2", " not "), "Ali");

        Assert.True(sonuc.BasariliMi);
        Assert.Equal(Bugun.AddDays(370), evrak.BitisTarihi);
        Assert.Equal("P-2", evrak.BelgeNo);
        var kayit = Assert.Single(evrak.Yenilemeler);
        Assert.Equal(Bugun.AddDays(5), kayit.EskiBitisTarihi);
        Assert.Equal("P-1", kayit.EskiBelgeNo);
        Assert.Equal("not", kayit.Not);
        Assert.Equal("Ali", kayit.Yapan);
    }

    [Fact]
    public void Yenilemede_belge_no_bossa_degismez()
    {
        var evrak = TakipEvraki.Olustur(Istek(Bugun.AddDays(5))).Deger;
        evrak.Yenile(new TakipYenilemeIstegi(Bugun.AddDays(370), null, " ", null), null);

        Assert.Equal("P-1", evrak.BelgeNo);
    }

    [Fact]
    public void Yeni_bitis_eskisinden_sonra_olmali()
    {
        var evrak = TakipEvraki.Olustur(Istek(Bugun.AddDays(5))).Deger;

        Assert.False(evrak.Yenile(new TakipYenilemeIstegi(Bugun.AddDays(5), null, null, null), null).BasariliMi);
        Assert.Empty(evrak.Yenilemeler);
    }

    [Fact]
    public void Pasif_evrak_yenilenemez()
    {
        var evrak = TakipEvraki.Olustur(Istek(Bugun.AddDays(5))).Deger;
        evrak.Pasiflestir();

        Assert.False(evrak.Yenile(new TakipYenilemeIstegi(Bugun.AddDays(100), null, null, null), null).BasariliMi);
    }
}
