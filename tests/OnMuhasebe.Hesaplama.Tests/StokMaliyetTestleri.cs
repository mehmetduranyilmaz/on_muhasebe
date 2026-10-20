using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Modules.Stok.Domain.Maliyet;

namespace OnMuhasebe.Hesaplama.Tests;

public class StokMaliyetTestleri
{
    private static MaliyetHareketi Giris(decimal miktar, decimal fiyat) => new(Guid.NewGuid(), miktar, 0m, fiyat);

    private static MaliyetHareketi Cikis(decimal miktar) => new(Guid.NewGuid(), 0m, miktar, null);

    private static MaliyetSonucu Hesapla(MaliyetYontemi yontem, params MaliyetHareketi[] hareketler) =>
        MaliyetHesaplayicilari.Getir(yontem).Hesapla(hareketler);

    [Fact]
    public void Agirlikli_ortalama_girislerin_ortalamasiyla_cikis_maliyetler()
    {
        // 10 @10 + 10 @20 -> ortalama 15; 5 çıkış @15; kalan 15 adet = 225 TL
        var cikis = Cikis(5);
        var sonuc = Hesapla(MaliyetYontemi.AgirlikliOrtalama, Giris(10, 10), Giris(10, 20), cikis);

        Assert.Equal(15m, sonuc.CikisBirimMaliyetleri[cikis.Id]);
        Assert.Equal((15m, 225m, 15m), (sonuc.Miktar, sonuc.Deger, sonuc.BirimMaliyet));
    }

    [Fact]
    public void Agirlikli_ortalama_cikistan_sonraki_giriste_yeniden_hesaplanir()
    {
        // 10 @10, 5 çıkış (kalan 5 @10 = 50), 5 @16 giriş -> 10 adet 130 TL, ortalama 13
        var sonuc = Hesapla(MaliyetYontemi.AgirlikliOrtalama, Giris(10, 10), Cikis(5), Giris(5, 16));

        Assert.Equal((10m, 130m, 13m), (sonuc.Miktar, sonuc.Deger, sonuc.BirimMaliyet));
    }

    [Fact]
    public void Fifo_en_eski_katmandan_dusurur()
    {
        // 10 @10, 10 @20; 5 çıkış -> @10; sonra 10 çıkış -> 5 @10 + 5 @20 = 150 -> birim 15; kalan 5 @20
        var cikis1 = Cikis(5);
        var cikis2 = Cikis(10);
        var sonuc = Hesapla(MaliyetYontemi.Fifo, Giris(10, 10), Giris(10, 20), cikis1, cikis2);

        Assert.Equal(10m, sonuc.CikisBirimMaliyetleri[cikis1.Id]);
        Assert.Equal(15m, sonuc.CikisBirimMaliyetleri[cikis2.Id]);
        Assert.Equal((5m, 100m, 20m), (sonuc.Miktar, sonuc.Deger, sonuc.BirimMaliyet));
    }

    [Fact]
    public void Son_alis_fiyati_tum_stogu_son_fiyattan_degerler()
    {
        var cikis = Cikis(5);
        var sonuc = Hesapla(MaliyetYontemi.SonAlisFiyati, Giris(10, 10), Giris(10, 20), cikis);

        Assert.Equal(20m, sonuc.CikisBirimMaliyetleri[cikis.Id]);
        Assert.Equal((15m, 300m), (sonuc.Miktar, sonuc.Deger));
    }

    [Fact]
    public void Ayni_hareketler_yonteme_gore_farkli_stok_degeri_verir()
    {
        MaliyetHareketi[] hareketler = [Giris(10, 10), Giris(10, 20), Cikis(5)];

        var degerler = Enum.GetValues<MaliyetYontemi>().Select(y => Hesapla(y, hareketler).Deger).ToList();

        // Ortalama 15×15=225, FIFO 5@10 + 10@20=250, son alış 15×20=300
        Assert.Equal([225m, 250m, 300m], degerler);
    }

    [Fact]
    public void Agirlikli_ortalama_eksi_bakiyeyi_gelen_girisle_kapatir()
    {
        // Stok yokken 5 çıkış (maliyet 0, bakiye -5), sonra 10 @8 giriş -> 5 adet, 40 TL, birim 8
        var cikis = Cikis(5);
        var sonuc = Hesapla(MaliyetYontemi.AgirlikliOrtalama, cikis, Giris(10, 8));

        Assert.Equal(0m, sonuc.CikisBirimMaliyetleri[cikis.Id]);
        Assert.Equal((5m, 40m, 8m), (sonuc.Miktar, sonuc.Deger, sonuc.BirimMaliyet));
    }

    [Fact]
    public void Fifo_eksi_bakiyeyi_gelen_girisle_kapatir()
    {
        // 2 @5 giriş, 5 çıkış (2 @5 + 3 eksi @5), 10 @9 giriş: 3'ü eksiyi kapatır, kalan 7 @9 = 63
        var sonuc = Hesapla(MaliyetYontemi.Fifo, Giris(2, 5), Cikis(5), Giris(10, 9));

        Assert.Equal((7m, 63m, 9m), (sonuc.Miktar, sonuc.Deger, sonuc.BirimMaliyet));
    }

    [Fact]
    public void Hareketsiz_kart_sifir_doner()
    {
        var sonuc = Hesapla(MaliyetYontemi.AgirlikliOrtalama);

        Assert.Equal((0m, 0m), (sonuc.Miktar, sonuc.Deger));
    }
}
