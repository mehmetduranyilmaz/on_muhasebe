using OnMuhasebe.Modules.Stok.Contracts;

namespace OnMuhasebe.Hesaplama.Tests;

public class FiyatListesiTestleri
{
    private static readonly Guid Cari = Guid.NewGuid();
    private static readonly Guid Grup = Guid.NewGuid();

    [Fact]
    public void Cariye_ozel_liste_gruptan_grup_genelden_once_gelir()
    {
        Assert.Equal(2, FiyatListesiKurallari.Oncelik([new(FiyatHedefTuru.Cari, Cari, "A"), new(FiyatHedefTuru.CariGrubu, Grup, "G")], Cari, Grup));
        Assert.Equal(1, FiyatListesiKurallari.Oncelik([new(FiyatHedefTuru.CariGrubu, Grup, "G")], Cari, Grup));
        Assert.Equal(0, FiyatListesiKurallari.Oncelik([], Cari, Grup));
    }

    [Fact]
    public void Baska_cariye_atanmis_liste_uygulanmaz()
    {
        Assert.Null(FiyatListesiKurallari.Oncelik([new(FiyatHedefTuru.Cari, Guid.NewGuid(), "B")], Cari, Grup));
        Assert.Null(FiyatListesiKurallari.Oncelik([new(FiyatHedefTuru.CariGrubu, Grup, "G")], null, null));
    }

    [Fact]
    public void Gecerlilik_tarih_araligi_ve_aktiflikle_belirlenir()
    {
        var gun = new DateOnly(2026, 10, 31);
        Assert.True(FiyatListesiKurallari.GecerliMi(true, null, null, gun));
        Assert.True(FiyatListesiKurallari.GecerliMi(true, gun, gun, gun));
        Assert.False(FiyatListesiKurallari.GecerliMi(true, gun.AddDays(1), null, gun));
        Assert.False(FiyatListesiKurallari.GecerliMi(true, null, gun.AddDays(-1), gun));
        Assert.False(FiyatListesiKurallari.GecerliMi(false, null, null, gun));
    }

    [Fact]
    public void Miktarin_ulastigi_en_yuksek_kademe_secilir()
    {
        FiyatKademesi[] kademeler = [new(0m, 10m, 0m), new(100m, 9m, 0m), new(500m, 8m, 2m)];

        Assert.Equal(10m, FiyatListesiKurallari.Kademe(kademeler, 99m)!.Fiyat);
        Assert.Equal(9m, FiyatListesiKurallari.Kademe(kademeler, 100m)!.Fiyat);
        Assert.Equal(2m, FiyatListesiKurallari.Kademe(kademeler, 1200m)!.Iskonto);
    }

    [Fact]
    public void Esige_ulasmayan_miktar_icin_liste_fiyat_vermez()
    {
        Assert.Null(FiyatListesiKurallari.Kademe([new FiyatKademesi(50m, 9m, 0m)], 10m));
    }
}
