using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.Cari.Domain;

namespace OnMuhasebe.Hesaplama.Tests;

public class CariHesaplamalariTestleri
{
    private static EkstreSatiri Satir(decimal borc, decimal alacak, decimal kur = 1m) => new(
        Guid.NewGuid(), new DateOnly(2026, 10, 1), null, CariHareketTuru.Dekont, null, null,
        borc, alacak, 0m, kur, Math.Round(borc * kur, 2), Math.Round(alacak * kur, 2), 0m);

    [Fact]
    public void Ekstre_devirden_baslayip_yuruyen_bakiye_hesaplar()
    {
        var hesap = CariHesaplamalari.Ekstre(1000m, 1000m, [Satir(500m, 0m), Satir(0m, 1200m), Satir(300m, 0m)]);

        Assert.Equal([1500m, 300m, 600m], hesap.Satirlar.Select(s => s.Bakiye));
        Assert.Equal((800m, 1200m, 600m), (hesap.ToplamBorc, hesap.ToplamAlacak, hesap.SonBakiye));
    }

    [Fact]
    public void Ekstre_alacakli_bakiyeyi_negatif_gosterir()
    {
        var hesap = CariHesaplamalari.Ekstre(0m, 0m, [Satir(0m, 250m)]);

        Assert.Equal(-250m, hesap.SonBakiye);
    }

    [Fact]
    public void Hareketsiz_donemde_son_bakiye_devire_esittir()
    {
        var hesap = CariHesaplamalari.Ekstre(-75.5m, -3000m, []);

        Assert.Empty(hesap.Satirlar);
        Assert.Equal((-75.5m, -3000m), (hesap.SonBakiye, hesap.SonBakiyeTl));
    }

    [Fact]
    public void Dovizli_ekstre_doviz_ve_tl_bakiyelerini_ayri_yurutur()
    {
        // 100 USD @40 borç, 100 USD @50 alacak: döviz bakiye sıfır ama TL bakiye -1000 (kur farkı oluşmuş)
        var hesap = CariHesaplamalari.Ekstre(0m, 0m, [Satir(100m, 0m, 40m), Satir(0m, 100m, 50m)]);

        Assert.Equal([100m, 0m], hesap.Satirlar.Select(s => s.Bakiye));
        Assert.Equal([4000m, -1000m], hesap.Satirlar.Select(s => s.BakiyeTl));
    }

    [Fact]
    public void Kur_farki_guncel_kurla_degerlenmis_tl_ile_kayitli_tl_arasindaki_farktir()
    {
        // 1000 USD borçlu cari, TL bakiye 45.000 (ortalama 45). Güncel kur 49,4889 -> 49.488,90 TL, fark +4.488,90
        var (guncelTl, kurFarki) = CariHesaplamalari.KurFarki(1000m, 45000m, 49.4889m);

        Assert.Equal(49488.90m, guncelTl);
        Assert.Equal(4488.90m, kurFarki);
    }

    [Fact]
    public void Alacakli_dovizli_caride_kur_artisi_negatif_fark_verir()
    {
        var (_, kurFarki) = CariHesaplamalari.KurFarki(-1000m, -45000m, 50m);

        Assert.Equal(-5000m, kurFarki);
    }
}
