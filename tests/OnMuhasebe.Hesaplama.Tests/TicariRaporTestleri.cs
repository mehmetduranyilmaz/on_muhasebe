using OnMuhasebe.Modules.Fatura.Contracts;

namespace OnMuhasebe.Hesaplama.Tests;

public sealed class TicariRaporTestleri
{
    private static readonly DateOnly _bas = new(2026, 10, 1);
    private static readonly DateOnly _son = new(2026, 11, 30);

    private static FaturaOzetKalemi Fatura(string cari, DateOnly tarih, decimal matrah, bool iade = false)
    {
        var isaret = iade ? -1m : 1m;
        return new FaturaOzetKalemi(Guid.NewGuid(), "F1", tarih, null, cari, iade, matrah * isaret, 0m, matrah * isaret, matrah * 0.2m * isaret, 0m,
            matrah * 1.2m * isaret);
    }

    [Fact]
    public void Satis_ozeti_iadeleri_duser_ve_cariye_gore_matrah_buyukten_kucuge_siralar()
    {
        var o = TicariRaporHesaplayici.FaturaOzeti(_bas, _son, TicariTaraf.Satis, FaturaOzetKirilimi.Cari,
        [
            Fatura("Alfa", new(2026, 10, 5), 1000m),
            Fatura("Alfa", new(2026, 10, 9), 200m, iade: true),
            Fatura("Beta", new(2026, 11, 2), 3000m)
        ]);

        Assert.Equal(["Beta", "Alfa"], o.Satirlar.Select(s => s.Anahtar));
        Assert.Equal((1, 1, 800m, 960m), (o.Satirlar[1].FaturaSayisi, o.Satirlar[1].IadeSayisi, o.Satirlar[1].MatrahTl, o.Satirlar[1].GenelToplamTl));
        Assert.Equal((2, 1, 3800m), (o.Toplam.FaturaSayisi, o.Toplam.IadeSayisi, o.Toplam.MatrahTl));
    }

    [Fact]
    public void Satis_ozeti_ay_kiriliminda_takvim_sirasiyla_turkce_ay_adiyla_doner()
    {
        var o = TicariRaporHesaplayici.FaturaOzeti(_bas, _son, TicariTaraf.Satis, FaturaOzetKirilimi.Ay,
            [Fatura("A", new(2026, 11, 2), 10m), Fatura("B", new(2026, 10, 30), 20m), Fatura("C", new(2026, 10, 1), 5m)]);

        Assert.Equal(["Ekim 2026", "Kasım 2026"], o.Satirlar.Select(s => s.Anahtar));
        Assert.Equal(25m, o.Satirlar[0].MatrahTl);
    }

    [Fact]
    public void Urun_toplamlari_karta_gore_birlesir_kartsizlari_adla_toplar_iadeyi_duser()
    {
        var kart = Guid.NewGuid();
        var r = TicariRaporHesaplayici.UrunToplamlari(_bas, _son,
        [
            new(kart, "S1", "Su", "ADET", TicariTaraf.Satis, 24m, 0m, 240m, 24m),      // 2 koli × 12
            new(kart, "S1", "Su", "ADET", TicariTaraf.Satis, -6m, 0m, -60m, -6m),      // iade
            new(kart, "S1", "Su", "ADET", TicariTaraf.Alis, 120m, 10m, 600m, 60m),
            new(null, null, "Nakliye  bedeli", "SAAT", TicariTaraf.Satis, 2m, 0m, 500m, 100m),
            new(null, null, "nakliye bedeli", "ADET", TicariTaraf.Satis, 1m, 0m, 100m, 20m)
        ]);

        Assert.Equal(2, r.Satirlar.Count);
        var nakliye = r.Satirlar[0];
        Assert.Equal((600m, (string?)null), (nakliye.SatisMatrahTl, nakliye.Birim));   // farklı birimler: miktar toplamı anlamsız
        var su = r.Satirlar[1];
        Assert.Equal((18m, 180m, 18m, 120m, 600m, 10m), (su.SatisMiktari, su.SatisMatrahTl, su.SatisKdvTl, su.AlisMiktari, su.AlisMatrahTl, su.AlisIskontoTl));
        Assert.Equal((780m, 600m), (r.SatisMatrahTl, r.AlisMatrahTl));
    }

    [Fact]
    public void Karsilama_yuzdesi_satir_matrahiyla_agirliklandirilir()
    {
        // 100 TL'lik satır tam, 900 TL'lik satır yarı karşılandı: (100 + 450) / 1000.
        Assert.Equal(55m, TicariRaporHesaplayici.KarsilamaYuzdesi([(10m, 10m, 100m), (2m, 1m, 900m)]));
        Assert.Equal(100m, TicariRaporHesaplayici.KarsilamaYuzdesi([(5m, 7m, 50m)]));       // fazla sevk %100'ü aşmaz
        Assert.Equal(50m, TicariRaporHesaplayici.KarsilamaYuzdesi([(4m, 2m, 0m)]));         // bedelsiz satırda miktara göre
    }

    [Theory]
    [InlineData(0, 0, SiparisOdemeDurumu.Faturalanmadi)]
    [InlineData(1000, 1000, SiparisOdemeDurumu.Odenmedi)]
    [InlineData(1000, 400, SiparisOdemeDurumu.KismenOdendi)]
    [InlineData(1000, 0.004, SiparisOdemeDurumu.Odendi)]
    public void Odeme_durumu_faturalanan_ve_kalana_gore(decimal faturalanan, decimal kalan, SiparisOdemeDurumu beklenen) =>
        Assert.Equal(beklenen, TicariRaporHesaplayici.OdemeDurumu(faturalanan, kalan));
}
