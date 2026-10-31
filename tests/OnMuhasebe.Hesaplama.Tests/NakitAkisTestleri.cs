using OnMuhasebe.Modules.KasaBanka.Contracts;

namespace OnMuhasebe.Hesaplama.Tests;

public class NakitAkisTestleri
{
    private static readonly DateOnly _carsamba = new(2026, 11, 4);

    private static NakitKalemi Kalem(DateOnly tarih, NakitKaynagi kaynak, decimal tl, bool gecikmis = false) =>
        new(tarih, kaynak, "", null, "TRY", tl, tl, gecikmis, NakitBaglantisi.Cari, Guid.Empty);

    [Fact]
    public void Haftalik_donemler_pazartesi_baslar_ilk_donem_bugunden_pazara_kadardir()
    {
        var d = NakitAkisHesaplayici.Donemler(_carsamba, NakitPeriyodu.Hafta, 3);

        Assert.Equal((new DateOnly(2026, 11, 4), new DateOnly(2026, 11, 8)), d[0]);
        Assert.Equal((new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 15)), d[1]);
        Assert.Equal((new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 22)), d[2]);
    }

    [Fact]
    public void Pazar_gunu_ilk_hafta_tek_gundur()
    {
        var d = NakitAkisHesaplayici.Donemler(new DateOnly(2026, 11, 8), NakitPeriyodu.Hafta, 2);

        Assert.Equal((new DateOnly(2026, 11, 8), new DateOnly(2026, 11, 8)), d[0]);
        Assert.Equal((new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 15)), d[1]);
    }

    [Fact]
    public void Aylik_donemler_ay_sonunda_biter_ve_yil_atlar()
    {
        var d = NakitAkisHesaplayici.Donemler(_carsamba, NakitPeriyodu.Ay, 3);

        Assert.Equal((new DateOnly(2026, 11, 4), new DateOnly(2026, 11, 30)), d[0]);
        Assert.Equal((new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)), d[1]);
        Assert.Equal((new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 31)), d[2]);
    }

    [Fact]
    public void Gunluk_donemler_tek_gunluktur()
    {
        var d = NakitAkisHesaplayici.Donemler(_carsamba, NakitPeriyodu.Gun, 2);

        Assert.Equal([(_carsamba, _carsamba), (_carsamba.AddDays(1), _carsamba.AddDays(1))], d);
    }

    [Fact]
    public void Gecikmis_borc_ilk_doneme_yazilir_gecikmis_alacak_sayilmaz_ufuk_disi_atilir()
    {
        var donemler = NakitAkisHesaplayici.Donemler(_carsamba, NakitPeriyodu.Hafta, 2);
        var kalemler = new[]
        {
            Kalem(new DateOnly(2026, 11, 10), NakitKaynagi.CariTahsilat, 500m),
            Kalem(new DateOnly(2026, 11, 6), NakitKaynagi.CekSenetOdeme, 300m),
            Kalem(new DateOnly(2026, 10, 20), NakitKaynagi.CariOdeme, 200m, gecikmis: true),
            Kalem(new DateOnly(2026, 10, 25), NakitKaynagi.CariTahsilat, 400m, gecikmis: true),
            Kalem(new DateOnly(2027, 1, 1), NakitKaynagi.CariTahsilat, 9999m)
        };

        var h = NakitAkisHesaplayici.Hesapla(1000m, kalemler, donemler, gecikmisAlacakDahil: false);

        Assert.Equal((0m, 500m, -500m, 500m), (h[0].Giris, h[0].Cikis, h[0].Net, h[0].Kumulatif));
        Assert.Equal((200m, 300m), (h[0].CariOdeme, h[0].CekSenetOdeme));
        Assert.Equal((500m, 0m, 1000m), (h[1].Giris, h[1].Cikis, h[1].Kumulatif));
    }

    [Fact]
    public void Istenirse_gecikmis_alacak_ilk_doneme_dahil_edilir()
    {
        var donemler = NakitAkisHesaplayici.Donemler(_carsamba, NakitPeriyodu.Hafta, 1);
        var h = NakitAkisHesaplayici.Hesapla(0m, [Kalem(new DateOnly(2026, 10, 25), NakitKaynagi.CariTahsilat, 400m, gecikmis: true)],
            donemler, gecikmisAlacakDahil: true);

        Assert.Equal((400m, 400m), (h[0].CariTahsilat, h[0].Kumulatif));
    }

    [Theory]
    [InlineData(15, "2026-11-15")]
    [InlineData(3, "2026-12-03")]
    [InlineData(4, "2026-11-04")]
    [InlineData(31, "2026-11-30")]
    public void Kredi_karti_bugunden_sonraki_ilk_son_odeme_gununde_odenir(int gun, string beklenen) =>
        Assert.Equal(DateOnly.Parse(beklenen), NakitAkisHesaplayici.SonrakiSonOdeme(_carsamba, gun));
}
