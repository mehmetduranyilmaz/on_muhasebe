using OnMuhasebe.Modules.Fatura.Contracts;

namespace OnMuhasebe.Hesaplama.Tests;

public class FaturaHesaplamaTestleri
{
    private static HesapSatiri Satir(decimal miktar, decimal fiyat, int kdv = 20, bool dahil = false, decimal i1 = 0, decimal i2 = 0, decimal i3 = 0,
        int tevkifatPay = 0, int tevkifatPayda = 0) =>
        new(miktar, fiyat, dahil, kdv, i1, i2, i3, tevkifatPay, tevkifatPayda);

    private static FaturaHesabi Hesapla(params HesapSatiri[] satirlar) =>
        FaturaHesaplayici.Hesapla(satirlar, GenelIskontoTuru.Yuzde, 0m);

    [Fact]
    public void Satir_iskontolari_zincirleme_uygulanir()
    {
        // 1000 × 0,90 × 0,95 × 0,98 = 837,90; KDV %20 = 167,58
        var s = Hesapla(Satir(1m, 1000m, i1: 10, i2: 5, i3: 2)).Satirlar[0];

        Assert.Equal((1000m, 162.10m, 837.90m, 167.58m, 1005.48m), (s.BrutTutar, s.IskontoTutari, s.Matrah, s.KdvTutari, s.Toplam));
    }

    [Fact]
    public void Kdv_dahil_fiyatta_toplam_raf_fiyatina_esit_kalir()
    {
        // 99,99 / 1,20 = 83,325 -> 83,33; KDV = 99,99 - 83,33 = 16,66 (matrah × %20 olsaydı 16,67 ve toplam 100,00 olurdu)
        var s = Hesapla(Satir(1m, 99.99m, dahil: true)).Satirlar[0];

        Assert.Equal((83.33m, 16.66m, 99.99m), (s.Matrah, s.KdvTutari, s.Toplam));
    }

    [Fact]
    public void Kdv_dahil_fiyatta_iskonto_dahil_tutar_uzerinden_uygulanir()
    {
        // 2 × 120 = 240 dahil; %10 iskonto -> 216 dahil -> matrah 180, KDV 36
        var s = Hesapla(Satir(2m, 120m, dahil: true, i1: 10)).Satirlar[0];

        Assert.Equal((200m, 20m, 180m, 36m, 216m), (s.BrutTutar, s.IskontoTutari, s.Matrah, s.KdvTutari, s.Toplam));
    }

    [Fact]
    public void Yuzde_genel_iskonto_satirlara_oransal_dagitilir_ve_kdv_dagilimi_cikar()
    {
        var h = FaturaHesaplayici.Hesapla([Satir(1m, 100m, kdv: 20), Satir(1m, 50m, kdv: 10)], GenelIskontoTuru.Yuzde, 10m);

        Assert.Equal([10m, 5m], h.Satirlar.Select(s => s.GenelIskontoPayi));
        Assert.Equal((15m, 135m, 22.5m, 157.5m), (h.GenelIskontoTutari, h.Matrah, h.KdvToplami, h.GenelToplam));
        Assert.Equal([new KdvDagilimi(10, 45m, 4.5m), new KdvDagilimi(20, 90m, 18m)], h.KdvDagilimi);
    }

    [Fact]
    public void Tutar_genel_iskontoda_kurus_farki_en_buyuk_satira_yazilir()
    {
        // 0,05 / 3 = 0,0167 -> her satır 0,02 (toplam 0,06); 0,01 fazla en büyük (eşitse ilk) satırdan düşülür
        var h = FaturaHesaplayici.Hesapla([Satir(1m, 1m), Satir(1m, 1m), Satir(1m, 1m)], GenelIskontoTuru.Tutar, 0.05m);

        Assert.Equal(0.05m, h.GenelIskontoTutari);
        Assert.Equal(0.05m, h.Satirlar.Sum(s => s.GenelIskontoPayi));
        Assert.Equal([0.01m, 0.02m, 0.02m], h.Satirlar.Select(s => s.GenelIskontoPayi));
    }

    [Fact]
    public void Genel_iskonto_net_toplami_asamaz()
    {
        var h = FaturaHesaplayici.Hesapla([Satir(1m, 100m)], GenelIskontoTuru.Tutar, 150m);

        Assert.Equal((100m, 0m, 0m, 0m), (h.GenelIskontoTutari, h.Matrah, h.KdvToplami, h.GenelToplam));
    }

    [Fact]
    public void Genel_iskontolu_kdv_dahil_satirda_kdv_matrahtan_hesaplanir()
    {
        // 120 dahil -> net 100; %10 genel iskonto -> matrah 90, KDV 18
        var h = FaturaHesaplayici.Hesapla([Satir(1m, 120m, dahil: true)], GenelIskontoTuru.Yuzde, 10m);

        Assert.Equal((90m, 18m, 108m), (h.Matrah, h.KdvToplami, h.GenelToplam));
    }

    [Fact]
    public void Yarim_kurus_yukari_yuvarlanir()
    {
        var s = Hesapla(Satir(1m, 0.125m, kdv: 0)).Satirlar[0];

        Assert.Equal((0.13m, 0m, 0.13m), (s.Matrah, s.KdvTutari, s.Toplam));
    }

    [Fact]
    public void Tevkifatsiz_faturada_odenecek_tutar_genel_toplama_esittir()
    {
        var h = Hesapla(Satir(1m, 1000m, kdv: 20));

        Assert.Equal((0m, 0m, 1200m, 1200m), (h.TevkifEdilenKdvToplami, h.StopajTutari, h.GenelToplam, h.OdenecekTutar));
    }

    [Fact]
    public void Satir_bazli_tevkifat_hesaplanan_kdvnin_payina_uygulanir()
    {
        // Matrah 1000, KDV %20 = 200, tevkifat 7/10 -> 140 tevkif; ödenecek 1200 - 140 = 1060
        var h = Hesapla(Satir(1m, 1000m, kdv: 20, tevkifatPay: 7, tevkifatPayda: 10));

        Assert.Equal(140m, h.Satirlar[0].TevkifEdilenKdv);
        Assert.Equal((140m, 1200m, 1060m), (h.TevkifEdilenKdvToplami, h.GenelToplam, h.OdenecekTutar));
    }

    [Fact]
    public void Karma_tevkifatta_her_satir_kendi_oraniyla_toplanir()
    {
        // S1: matrah 1000, KDV 200, 5/10 -> 100; S2: matrah 500, KDV 100, 9/10 -> 90; toplam tevkif 190
        var h = Hesapla(
            Satir(1m, 1000m, kdv: 20, tevkifatPay: 5, tevkifatPayda: 10),
            Satir(1m, 500m, kdv: 20, tevkifatPay: 9, tevkifatPayda: 10));

        Assert.Equal([100m, 90m], h.Satirlar.Select(s => s.TevkifEdilenKdv));
        Assert.Equal((190m, 1800m, 1610m), (h.TevkifEdilenKdvToplami, h.GenelToplam, h.OdenecekTutar));
    }

    [Fact]
    public void Stopaj_matrah_toplamina_uygulanir_ve_odenecegi_dusurur()
    {
        // Gider pusulası: KDV yok, matrah 1000, stopaj %10 = 100; ödenecek 1000 - 100 = 900
        var h = FaturaHesaplayici.Hesapla([Satir(1m, 1000m, kdv: 0)], GenelIskontoTuru.Yuzde, 0m, stopajOrani: 10m);

        Assert.Equal((100m, 1000m, 900m), (h.StopajTutari, h.GenelToplam, h.OdenecekTutar));
    }

    [Fact]
    public void Serbest_meslek_makbuzunda_stopaj_ve_tevkifat_birlikte_dusulur()
    {
        // Matrah 1000, KDV %20 = 200 (5/10 tevkifat -> 100), GV stopajı %20 = 200; ödenecek 1200 - 100 - 200 = 900
        var h = FaturaHesaplayici.Hesapla(
            [Satir(1m, 1000m, kdv: 20, tevkifatPay: 5, tevkifatPayda: 10)], GenelIskontoTuru.Yuzde, 0m, stopajOrani: 20m);

        Assert.Equal((100m, 200m, 1200m, 900m), (h.TevkifEdilenKdvToplami, h.StopajTutari, h.GenelToplam, h.OdenecekTutar));
    }

    [Fact]
    public void Toplamlar_satir_toplamlarina_esittir()
    {
        var h = FaturaHesaplayici.Hesapla(
            [Satir(3m, 33.333m, kdv: 1, i1: 7.5m), Satir(1.5m, 19.99m, kdv: 10, dahil: true), Satir(7m, 0.99m, kdv: 20, i1: 3, i2: 2)],
            GenelIskontoTuru.Tutar, 4.44m);

        Assert.Equal(h.BrutToplam - h.SatirIskontoToplami - h.GenelIskontoTutari, h.Matrah);
        Assert.Equal(h.Matrah + h.KdvToplami, h.GenelToplam);
        Assert.Equal(h.KdvToplami, h.KdvDagilimi.Sum(d => d.KdvTutari));
        Assert.Equal(4.44m, h.GenelIskontoTutari);
    }
}
