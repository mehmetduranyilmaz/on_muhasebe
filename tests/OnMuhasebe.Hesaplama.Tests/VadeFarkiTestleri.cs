using OnMuhasebe.Modules.Cari.Contracts;

namespace OnMuhasebe.Hesaplama.Tests;

public class VadeFarkiTestleri
{
    private static readonly DateOnly _vade = new(2026, 1, 1);
    private static readonly DateOnly _bitis = new(2026, 3, 2); // vadeden 60 gün sonra

    private static VadeFarkiGirdisi Kalem(decimal tutar, decimal kalan, params VadeFarkiOdemesi[] odemeler) =>
        new(Guid.NewGuid(), CariHareketTuru.SatisFaturasi, "F1", _vade, tutar, odemeler, kalan, null);

    [Fact]
    public void Acik_kalan_hesap_tarihine_kadar_basit_faizle_365_gun_esasli_faizlenir()
    {
        // 10.000 × %36 × 60/365 = 591,78
        var s = VadeFarkiHesaplayici.Hesapla([Kalem(10_000m, 10_000m)], 36m, null, _bitis, 0).Single();

        Assert.Equal((60, 591.78m, (DateOnly?)null), (s.Gun, s.Faiz, s.OdemeTarihi));
    }

    [Fact]
    public void Gec_odenen_kisim_odeme_tarihine_kadar_faizlenir_vaktinde_odenen_faizlenmez()
    {
        // 4.000 vadeden önce, 3.000 30 gün geç ödendi; 3.000 açık (60 gün)
        var satirlar = VadeFarkiHesaplayici.Hesapla(
            [Kalem(10_000m, 3_000m, new VadeFarkiOdemesi(_vade.AddDays(-5), 4_000m), new VadeFarkiOdemesi(_vade.AddDays(30), 3_000m))],
            36m, null, _bitis, 0);

        Assert.Equal(2, satirlar.Count);
        Assert.Equal((30, 88.77m), (satirlar[0].Gun, satirlar[0].Faiz));  // 3.000 × 0,36 × 30/365
        Assert.Equal((60, 177.53m), (satirlar[1].Gun, satirlar[1].Faiz)); // 3.000 × 0,36 × 60/365
    }

    [Fact]
    public void Tolerans_icindeki_gecikme_faizlenmez_asilirsa_vadeden_faizlenir()
    {
        var odeme = new VadeFarkiOdemesi(_vade.AddDays(5), 1_000m);

        Assert.Empty(VadeFarkiHesaplayici.Hesapla([Kalem(1_000m, 0m, odeme)], 36m, null, _bitis, 7));
        Assert.Equal(5, VadeFarkiHesaplayici.Hesapla([Kalem(1_000m, 0m, odeme)], 36m, null, _bitis, 3).Single().Gun);
    }

    [Fact]
    public void Baslangic_tarihinden_once_faiz_islemez_cift_faturalama_olmaz()
    {
        // Önceki vade farkı 31.01'e kadar faturalandı: faiz 31.01'den 02.03'e (30 gün)
        var s = VadeFarkiHesaplayici.Hesapla([Kalem(10_000m, 10_000m)], 36m, new DateOnly(2026, 1, 31), _bitis, 0).Single();

        Assert.Equal(30, s.Gun);
    }

    [Fact]
    public void Baslangictan_once_odenmis_kisim_bu_donemde_faizlenmez()
    {
        var satirlar = VadeFarkiHesaplayici.Hesapla(
            [Kalem(1_000m, 0m, new VadeFarkiOdemesi(new DateOnly(2026, 1, 20), 1_000m))], 36m, new DateOnly(2026, 1, 31), _bitis, 0);

        Assert.Empty(satirlar);
    }
}
