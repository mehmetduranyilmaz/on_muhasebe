using OnMuhasebe.Modules.KasaBanka.Contracts;

namespace OnMuhasebe.Hesaplama.Tests;

public sealed class FinansalDurumTestleri
{
    private static readonly DateOnly _bugun = new(2026, 11, 7);

    [Fact]
    public void Vade_kovalari_sinir_gunlerini_dogru_kovaya_koyar()
    {
        var k = FinansalDurumHesaplayici.VadeKovalari(_bugun,
        [
            new(_bugun.AddDays(-1), true, 100m),   // dün: vadesi geçmiş
            new(_bugun, true, 10m),                // bugün: 7 gün içinde
            new(_bugun.AddDays(7), false, 20m),    // 7. gün: 7 gün içinde
            new(_bugun.AddDays(8), true, 30m),     // 8. gün
            new(_bugun.AddDays(30), false, 40m),
            new(_bugun.AddDays(31), true, 50m),
            new(_bugun.AddDays(91), false, 60m)
        ]);

        Assert.Equal(["Vadesi geçmiş", "7 gün içinde", "8-30 gün", "31-90 gün", "90 günden sonra"], k.Select(x => x.Ad));
        Assert.Equal((100m, 0m), (k[0].TahsilEdilecekTl, k[0].OdenecekTl));
        Assert.Equal((10m, 20m), (k[1].TahsilEdilecekTl, k[1].OdenecekTl));
        Assert.Equal((30m, 40m), (k[2].TahsilEdilecekTl, k[2].OdenecekTl));
        Assert.Equal((50m, 0m), (k[3].TahsilEdilecekTl, k[3].OdenecekTl));
        Assert.Equal((0m, 60m), (k[4].TahsilEdilecekTl, k[4].OdenecekTl));
    }

    [Fact]
    public void Mini_bilanco_varlik_ve_yukumlulukleri_toplar_net_pozisyonu_verir()
    {
        var b = FinansalDurumHesaplayici.Bilanco(kasaBanka: 50_000m, posBekleyen: 5_000m, cariAlacak: 120_000m, alinanEvrak: 30_000m,
            cariBorc: 80_000m, verilenEvrak: 40_000m, krediKarti: 7_500m, krediAnapara: 100_000m);

        Assert.Equal(205_000m, b.ToplamVarlikTl);
        Assert.Equal(227_500m, b.ToplamYukumlulukTl);
        Assert.Equal(-22_500m, b.NetTl);
    }

    [Fact]
    public void Aylik_akis_bos_aylari_sifirlar_ay_sonu_nakdi_birikimli_hesaplar()
    {
        var aylar = FinansalDurumHesaplayici.Aylar(_bugun, 3, 1_000m,
            new Dictionary<(int, int), (decimal, decimal)> { [(2026, 9)] = (500m, 200m), [(2026, 11)] = (100m, 400m) },
            new Dictionary<(int, int), decimal> { [(2026, 9)] = 300m, [(2026, 10)] = 50m, [(2026, 11)] = -300m });

        Assert.Equal([(2026, 9), (2026, 10), (2026, 11)], aylar.Select(a => (a.Yil, a.Ay)));
        Assert.Equal((300m, 1_300m), (aylar[0].NetTl, aylar[0].AySonuNakitTl));
        Assert.Equal((0m, 1_350m), (aylar[1].NetTl, aylar[1].AySonuNakitTl));   // akış yok ama virmanla bakiye değişmiş olabilir
        Assert.Equal((-300m, 1_050m), (aylar[2].NetTl, aylar[2].AySonuNakitTl));
    }
}
