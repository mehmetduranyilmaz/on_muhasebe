using OnMuhasebe.Modules.Fatura.Contracts;

namespace OnMuhasebe.Hesaplama.Tests;

public class FaturaRaporTestleri
{
    private static readonly Guid _kartA = Guid.NewGuid(), _kartB = Guid.NewGuid(), _cari1 = Guid.NewGuid(), _cari2 = Guid.NewGuid();

    private static KarlilikKalemi K(Guid? kart, Guid? cari, decimal satis, decimal? maliyet, decimal miktar = 1, string ad = "x") =>
        new(Guid.NewGuid(), "F1", new DateOnly(2026, 11, 1), cari, cari == _cari1 ? "Müşteri 1" : "Müşteri 2", kart, kart is null ? null : "K", ad, miktar, satis, maliyet);

    [Fact]
    public void Urun_kiriliminda_satis_maliyet_ve_iade_netlenir()
    {
        var satirlar = FaturaRaporHesaplayici.Karlilik(
        [
            K(_kartA, _cari1, 1000m, 600m, 10),
            K(_kartA, _cari2, 500m, 300m, 5),
            K(_kartA, _cari1, -100m, -60m, -1),
            K(_kartB, _cari2, 200m, 190m, 2)
        ], KarlilikKirilimi.Urun);

        var a = satirlar.Single(s => s.Id == _kartA);
        Assert.Equal((14m, 1400m, 840m, 560m, 40m), (a.Miktar, a.NetSatisTl, a.MaliyetTl, a.BrutKarTl, a.MarjYuzde));
        Assert.Equal(_kartA, satirlar[0].Id); // brüt kâra göre sıralı
    }

    [Fact]
    public void Maliyeti_izlenmeyen_satis_ayrica_gosterilir_ve_kara_tam_katilir()
    {
        var s = FaturaRaporHesaplayici.Karlilik([K(null, _cari1, 300m, null), K(_kartA, _cari1, 700m, 400m)], KarlilikKirilimi.Musteri).Single();

        Assert.Equal((1000m, 400m, 600m, 300m, 60m), (s.NetSatisTl, s.MaliyetTl, s.BrutKarTl, s.MaliyetsizSatisTl, s.MarjYuzde));
    }

    [Fact]
    public void Net_satis_sifir_ya_da_eksiyse_marj_yoktur()
    {
        Assert.Null(FaturaRaporHesaplayici.Marj(-50m, 0m));
        Assert.Null(FaturaRaporHesaplayici.Marj(10m, -100m));
        Assert.Equal(33.33m, FaturaRaporHesaplayici.Marj(1m, 3m));
    }

    private static KdvKalemi Kdv(FaturaTuru tur, decimal matrah, int oran = 20, int pay = 0, int payda = 0, string? kod = null)
    {
        var kdv = matrah * oran / 100m;
        return new KdvKalemi(tur, oran, matrah, kdv, kod, pay, payda, payda > 0 ? kdv * pay / payda : 0m);
    }

    [Fact]
    public void Kdv1_hesaplanan_ve_indirimler_beyanname_yapisinda_ayrilir()
    {
        var o = FaturaRaporHesaplayici.KdvOzeti(2026, 11,
        [
            Kdv(FaturaTuru.Satis, 1000m),                                  // 200 hesaplanan
            Kdv(FaturaTuru.Satis, 500m, 10),                               // 50 hesaplanan (%10)
            Kdv(FaturaTuru.Satis, 1000m, 20, 5, 10, "617"),                // KDV 200, tevkif 100, satıcı 100 beyan eder
            Kdv(FaturaTuru.AlistanIade, 100m),                             // 20 hesaplanan (diğer işlemler)
            Kdv(FaturaTuru.Alis, 2000m),                                   // 400 indirilecek
            Kdv(FaturaTuru.Alis, 1000m, 20, 9, 10, "602"),                 // KDV 200: 20 satıcıya, 180 KDV2 sorumlu sıfatıyla
            Kdv(FaturaTuru.SerbestMeslekMakbuzu, 500m),                    // 100 indirilecek
            Kdv(FaturaTuru.SatistanIade, 200m)                             // 40 indirim (KDVK 35)
        ], [], 8);

        Assert.Equal([new KdvOranSatiri(10, 500m, 50m), new KdvOranSatiri(20, 1000m, 200m)], o.TeslimVeHizmetler);
        Assert.Equal(100m, o.KismiTevkifatliSatislar.Single().BeyanEdilecekTl);
        Assert.Equal(20m, o.AlistanIadeler.Single().VergiTl);
        Assert.Equal(370m, o.HesaplananKdvTl);                             // 200 + 50 + 100 + 20
        Assert.Equal((520m, 180m, 40m, 740m), (o.IndirilecekKdvTl, o.SorumluSifatiylaOdenenKdvTl, o.SatistanIadeKdvTl, o.IndirimlerToplamiTl));
        Assert.Equal(-370m, o.FarkTl);                                     // sonraki döneme devreden
        Assert.Equal((180m, "602"), (o.Kdv2ToplamTl, o.Kdv2.Single().Kod));
    }

    [Fact]
    public void Muhtasar_tur_ve_orana_gore_toplanir_stopajsizlar_atilir()
    {
        var o = FaturaRaporHesaplayici.KdvOzeti(2026, 11, [],
        [
            new StopajKalemi(FaturaTuru.SerbestMeslekMakbuzu, 20m, 1000m, 200m),
            new StopajKalemi(FaturaTuru.SerbestMeslekMakbuzu, 20m, 500m, 100m),
            new StopajKalemi(FaturaTuru.GiderPusulasi, 2m, 1000m, 20m),
            new StopajKalemi(FaturaTuru.GiderPusulasi, 0m, 300m, 0m)
        ], 4);

        Assert.Equal(2, o.Muhtasar.Count);
        Assert.Equal(new StopajSatiri(FaturaTuru.SerbestMeslekMakbuzu, 20m, 1500m, 300m, 2), o.Muhtasar.Single(s => s.Tur == FaturaTuru.SerbestMeslekMakbuzu));
        Assert.Equal(320m, o.MuhtasarToplamTl);
    }
}
