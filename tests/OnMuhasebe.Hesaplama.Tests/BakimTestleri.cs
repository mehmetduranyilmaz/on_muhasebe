using OnMuhasebe.Modules.Bakim.Contracts;
using OnMuhasebe.Modules.Bakim.Domain;

namespace OnMuhasebe.Hesaplama.Tests;

public class BakimTestleri
{
    private static readonly DateOnly Bugun = new(2026, 11, 1);
    private static readonly DateTime Simdi = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);

    private static BakimPlaniKaydetIstegi Plan(int? gun = 30, decimal? sayac = null, DateOnly? sonraki = null) =>
        new("Yağlama", false, null, gun, sayac, sonraki, null, 7);

    private static IsEmriKaydetIstegi Emir(IsEmriTuru tur = IsEmriTuru.Ariza, bool durdu = true, IReadOnlyList<IsEmriParcaIstegi>? parcalar = null) =>
        new(Guid.NewGuid(), tur, null, "İş", null, Simdi, durdu, 100, 200, null, null, null, null, parcalar);

    [Fact]
    public void Periyotsuz_plan_olmaz() =>
        Assert.False(BakimPlani.Olustur(Guid.NewGuid(), Plan(null, null), Bugun, null).BasariliMi);

    [Fact]
    public void Ilk_bakim_bugunden_ve_guncel_sayactan_sayilir()
    {
        var plan = BakimPlani.Olustur(Guid.NewGuid(), Plan(30, 1000), Bugun, 500).Deger;

        Assert.Equal(Bugun.AddDays(30), plan.SonrakiTarih);
        Assert.Equal(1500, plan.SonrakiSayac);
    }

    [Fact]
    public void Elle_verilen_sonraki_tarih_gecerlidir() =>
        Assert.Equal(Bugun.AddDays(3), BakimPlani.Olustur(Guid.NewGuid(), Plan(30, null, Bugun.AddDays(3)), Bugun, null).Deger.SonrakiTarih);

    [Fact]
    public void Yalniz_sayac_periyodunda_tarih_tutulmaz() =>
        Assert.Null(BakimPlani.Olustur(Guid.NewGuid(), Plan(null, 100), Bugun, 0).Deger.SonrakiTarih);

    [Fact]
    public void Sonraki_bakim_yapildigi_gunden_ve_sayactan_hesaplanir()
    {
        var plan = BakimPlani.Olustur(Guid.NewGuid(), Plan(30, 1000), Bugun, 500).Deger;

        // Gecikmeli yapıldı: sonraki, planlanan günden değil yapılan günden sayılır.
        plan.Yapildi(Bugun.AddDays(40), 1700);

        Assert.Equal(Bugun.AddDays(70), plan.SonrakiTarih);
        Assert.Equal(2700, plan.SonrakiSayac);
        Assert.Equal(Bugun.AddDays(40), plan.SonYapilmaTarihi);
    }

    [Theory]
    [InlineData(8, PlanDurumu.Zamaninda)]
    [InlineData(7, PlanDurumu.Yaklasiyor)]
    [InlineData(0, PlanDurumu.Yaklasiyor)]
    [InlineData(-1, PlanDurumu.Gecikti)]
    public void Gune_gore_durum(int kalan, PlanDurumu beklenen) =>
        Assert.Equal(beklenen, BakimHesaplari.Durum(Bugun.AddDays(kalan), 7, null, null, null, Bugun));

    [Theory]
    [InlineData(800, PlanDurumu.Zamaninda)]
    [InlineData(900, PlanDurumu.Yaklasiyor)]
    [InlineData(1000, PlanDurumu.Gecikti)]
    [InlineData(1200, PlanDurumu.Gecikti)]
    public void Sayaca_gore_durum_periyodun_onda_biri_kala_uyarir(int sayac, PlanDurumu beklenen) =>
        Assert.Equal(beklenen, BakimHesaplari.Durum(null, 7, 1000, 1000, sayac, Bugun));

    [Fact]
    public void Gun_ve_sayactan_kotusu_gecerlidir()
    {
        Assert.Equal(PlanDurumu.Gecikti, BakimHesaplari.Durum(Bugun.AddDays(100), 7, 1000, 1000, 1500, Bugun));
        Assert.Equal(PlanDurumu.Gecikti, BakimHesaplari.Durum(Bugun.AddDays(-1), 7, 1000, 1000, 0, Bugun));
        Assert.Equal(PlanDurumu.Zamaninda, BakimHesaplari.Durum(null, 7, 1000, 1000, null, Bugun));
    }

    [Fact]
    public void Arizada_durus_bildirimden_sayilir()
    {
        var bitis = Simdi.AddHours(5.5);

        Assert.Equal(5.5m, BakimHesaplari.DurusSaati(true, IsEmriTuru.Ariza, Simdi, Simdi.AddHours(2), bitis));
    }

    [Fact]
    public void Planli_iste_durus_baslangictan_sayilir_baslangic_yoksa_bildirimden()
    {
        var bitis = Simdi.AddHours(5);

        Assert.Equal(3m, BakimHesaplari.DurusSaati(true, IsEmriTuru.PlanliBakim, Simdi, Simdi.AddHours(2), bitis));
        Assert.Equal(5m, BakimHesaplari.DurusSaati(true, IsEmriTuru.PlanliBakim, Simdi, null, bitis));
    }

    [Fact]
    public void Makine_durmadiysa_ya_da_is_bitmediyse_durus_yoktur()
    {
        Assert.Null(BakimHesaplari.DurusSaati(false, IsEmriTuru.Ariza, Simdi, null, Simdi.AddHours(1)));
        Assert.Null(BakimHesaplari.DurusSaati(true, IsEmriTuru.Ariza, Simdi, null, null));
    }

    [Fact]
    public void Is_emri_durum_akisi()
    {
        var emir = IsEmri.Olustur("IE00001", Emir(), Simdi);
        Assert.Equal(IsEmriDurumu.Acik, emir.Durum);

        Assert.True(emir.Baslat(Simdi.AddHours(1)).BasariliMi);
        Assert.False(emir.Baslat(Simdi.AddHours(1)).BasariliMi);

        Assert.True(emir.Kapat(new IsEmriKapatmaIstegi(Simdi.AddHours(3), 500, "R-1")).BasariliMi);
        Assert.Equal(IsEmriDurumu.Kapandi, emir.Durum);
        Assert.Equal("R-1", emir.RaporNo);
        Assert.Equal(3m, emir.DurusSaati);

        Assert.False(emir.Kapat(new IsEmriKapatmaIstegi(Simdi.AddHours(4), null, null)).BasariliMi);
        Assert.False(emir.Guncelle(Emir(), Simdi).BasariliMi);
        Assert.True(emir.IptalEt().BasariliMi);
        Assert.False(emir.IptalEt().BasariliMi);
    }

    [Fact]
    public void Bitis_bildirimden_ya_da_baslangictan_once_olamaz()
    {
        var emir = IsEmri.Olustur("IE00001", Emir(), Simdi);
        Assert.False(emir.Kapat(new IsEmriKapatmaIstegi(Simdi.AddHours(-1), null, null)).BasariliMi);

        emir.Baslat(Simdi.AddHours(2));
        Assert.False(emir.Kapat(new IsEmriKapatmaIstegi(Simdi.AddHours(1), null, null)).BasariliMi);
        Assert.Equal(IsEmriDurumu.Devam, emir.Durum);
    }

    [Fact]
    public void Dakika_hassasiyetindeki_bitis_saniyeli_baslangictan_once_kalabilir()
    {
        var emir = IsEmri.Olustur("IE00001", Emir(), Simdi);
        emir.Baslat(Simdi.AddSeconds(30));

        Assert.True(emir.Kapat(new IsEmriKapatmaIstegi(Simdi, null, null)).BasariliMi);
        Assert.Equal(Simdi.AddSeconds(30), emir.BitisZamani);
    }

    [Fact]
    public void Baslatilmadan_kapanan_iste_baslangic_bildirimdir()
    {
        var emir = IsEmri.Olustur("IE00001", Emir(IsEmriTuru.PlanliBakim), Simdi);
        emir.Kapat(new IsEmriKapatmaIstegi(Simdi.AddHours(2), null, null));

        Assert.Equal(Simdi, emir.BaslangicZamani);
        Assert.Equal(2m, emir.DurusSaati);
    }

    [Fact]
    public void Elle_parca_tutari_stok_kartlilari_saymaz()
    {
        var emir = IsEmri.Olustur("IE00001", Emir(parcalar:
        [
            new(null, null, "Filtre", "ADET", 2, 150.5m),
            new(Guid.NewGuid(), Guid.NewGuid(), "Yağ", "LT", 5, 999)
        ]), Simdi);

        Assert.Equal(301m, BakimHesaplari.ElleParcaTutari(emir.Parcalar));
        Assert.Null(emir.Parcalar[1].BirimFiyat);
    }

    [Fact]
    public void Hizmet_disi_ekipmanin_durumunu_is_emri_degistirmez()
    {
        var istek = new EkipmanKaydetIstegi(null, "Pres", EkipmanTuru.Makine, null, null, null, null, null, null, null, null, null, SayacBirimi.Saat, 100, EkipmanDurumu.HizmetDisi, null);
        var ekipman = Ekipman.Olustur("E00001", istek);

        ekipman.DurumAta(EkipmanDurumu.Arizali);
        Assert.Equal(EkipmanDurumu.HizmetDisi, ekipman.Durum);

        ekipman.SayacIlerlet(90);
        Assert.Equal(100, ekipman.Sayac);
        ekipman.SayacIlerlet(150);
        Assert.Equal(150, ekipman.Sayac);
    }

    [Fact]
    public void Sayacsiz_ekipmanda_sayac_tutulmaz()
    {
        var istek = new EkipmanKaydetIstegi(null, "Kap", EkipmanTuru.BasincliKap, null, null, null, null, null, null, null, null, null, SayacBirimi.Yok, 100, EkipmanDurumu.Calisiyor, null);
        var ekipman = Ekipman.Olustur("E00002", istek);

        Assert.Null(ekipman.Sayac);
        ekipman.SayacIlerlet(500);
        Assert.Null(ekipman.Sayac);
    }
}
