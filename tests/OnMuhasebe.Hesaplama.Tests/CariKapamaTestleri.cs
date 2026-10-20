using OnMuhasebe.Modules.Cari.Domain;

namespace OnMuhasebe.Hesaplama.Tests;

public class CariKapamaTestleri
{
    private static readonly Guid _f1 = Guid.NewGuid(), _f2 = Guid.NewGuid(), _f3 = Guid.NewGuid(), _t1 = Guid.NewGuid(), _t2 = Guid.NewGuid();

    private static KapamaKalemi Borc(Guid id, int gun, decimal kalan) => new(id, true, new DateOnly(2026, 10, gun), new DateOnly(2026, 10, gun), kalan);

    private static KapamaKalemi Alacak(Guid id, int gun, decimal kalan) => new(id, false, new DateOnly(2026, 10, gun), new DateOnly(2026, 10, gun), kalan);

    [Fact]
    public void Tahsilat_en_eski_vadeli_faturadan_baslayarak_dagitilir()
    {
        var sonuc = CariKapamaHesaplayici.Fifo([Borc(_f2, 10, 300m), Borc(_f1, 5, 500m), Borc(_f3, 15, 200m), Alacak(_t1, 20, 700m)]);

        Assert.Equal([new KapamaEslesmesi(_f1, _t1, 500m), new KapamaEslesmesi(_f2, _t1, 200m)], sonuc);
    }

    [Fact]
    public void Avans_sonradan_gelen_faturayi_kapatir()
    {
        // 1 Ekim'de 1.000 avans alındı; 10 Ekim'deki 400'lük fatura avanstan kapanır, 600 avans açık kalır.
        var sonuc = CariKapamaHesaplayici.Fifo([Alacak(_t1, 1, 1000m), Borc(_f1, 10, 400m)]);

        Assert.Equal([new KapamaEslesmesi(_f1, _t1, 400m)], sonuc);
    }

    [Fact]
    public void Birden_cok_tahsilat_bir_faturayi_kismen_kismen_kapatir()
    {
        var sonuc = CariKapamaHesaplayici.Fifo([Borc(_f1, 1, 1000m), Alacak(_t1, 5, 250m), Alacak(_t2, 8, 300m)]);

        Assert.Equal([new KapamaEslesmesi(_f1, _t1, 250m), new KapamaEslesmesi(_f1, _t2, 300m)], sonuc);
        Assert.Equal(450m, CariKapamaHesaplayici.Dus([Borc(_f1, 1, 1000m)], sonuc).Single().Kalan);
    }

    [Fact]
    public void Kapanmis_kalemler_dagitima_katilmaz()
    {
        var sonuc = CariKapamaHesaplayici.Fifo([Borc(_f1, 1, 0m), Borc(_f2, 2, 100m), Alacak(_t1, 3, 100m)]);

        Assert.Equal([new KapamaEslesmesi(_f2, _t1, 100m)], sonuc);
    }

    [Fact]
    public void Oncelikli_kapama_once_hedef_faturayi_kapatir_kalani_disarida_birakir()
    {
        // Peşin tahsilat (1.200) kendi faturasını (1.000) kapatır; eski fatura (500) FIFO'ya kalır.
        var sonuc = CariKapamaHesaplayici.Oncelikli(Alacak(_t1, 20, 1200m), [Borc(_f2, 20, 1000m)]);

        Assert.Equal([new KapamaEslesmesi(_f2, _t1, 1000m)], sonuc);
        var kalanlar = CariKapamaHesaplayici.Dus([Borc(_f1, 1, 500m), Borc(_f2, 20, 1000m), Alacak(_t1, 20, 1200m)], sonuc);
        Assert.Equal([new KapamaEslesmesi(_f1, _t1, 200m)], CariKapamaHesaplayici.Fifo(kalanlar));
    }

    [Fact]
    public void Oncelikli_kapama_ayni_yondeki_kalemi_kapatmaz()
    {
        Assert.Empty(CariKapamaHesaplayici.Oncelikli(Alacak(_t1, 1, 100m), [Alacak(_t2, 1, 100m)]));
    }
}
