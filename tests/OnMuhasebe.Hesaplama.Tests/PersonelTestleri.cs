using OnMuhasebe.Modules.Personel.Contracts;
using OnMuhasebe.Modules.Personel.Domain;

namespace OnMuhasebe.Hesaplama.Tests;

public class PersonelTestleri
{
    private static readonly DateOnly Gun = new(2026, 3, 10);
    private static readonly DateTime Simdi = new(2026, 3, 11, 9, 0, 0, DateTimeKind.Utc);

    private static PersonelKaydetIstegi Istek(string? tc = null, string? iban = null, decimal? maas = 10000) =>
        new("Ayse", "Kaya", tc, new DateOnly(2026, 1, 1), "Muhasebeci", null, null, null, iban, maas, null, null, null);

    private static MaasSatiri Satir(decimal net = 10000, decimal avans = 0) => MaasSatiri.Olustur(Guid.NewGuid(), Guid.NewGuid(), null, net, avans);

    [Theory]
    [InlineData(10000, 500, 200, 1000, 9300)]
    [InlineData(10000, 0, 0, 0, 10000)]
    public void Odenecek_hesabi(decimal net, decimal prim, decimal kesinti, decimal avans, decimal beklenen) =>
        Assert.Equal(beklenen, MaasHesabi.Odenecek(net, prim, kesinti, avans));

    [Theory]
    [InlineData(3000, 3, 3000, 1000)]
    [InlineData(3000, 3, 500, 500)]
    [InlineData(1000, 3, 1000, 333.33)]
    [InlineData(1000, 1, 1000, 1000)]
    public void Taksit_son_taksitte_kalani_gecmez(decimal tutar, int taksit, decimal kalan, decimal beklenen) =>
        Assert.Equal(beklenen, MaasHesabi.Taksit(tutar, taksit, kalan));

    [Fact]
    public void Avans_kesintisi_en_eski_avanstan_dagitilir()
    {
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal([(a, 300m), (b, 200m)], MaasHesabi.Dagit(500, [(a, 300m), (b, 400m)]));
        Assert.Equal([(a, 100m)], MaasHesabi.Dagit(100, [(a, 300m), (b, 400m)]));
        Assert.Empty(MaasHesabi.Dagit(0, [(a, 300m)]));
    }

    [Theory]
    [InlineData("2026-03-20", null, true)]
    [InlineData("2026-04-01", null, false)]
    [InlineData("2026-01-01", "2026-03-01", true)]
    [InlineData("2026-01-01", "2026-02-28", false)]
    public void Donemde_calisan(string giris, string? cikis, bool beklenen) =>
        Assert.Equal(beklenen, MaasHesabi.DonemdeCalisiyorMu(DateOnly.Parse(giris), cikis is null ? null : DateOnly.Parse(cikis), 2026, 3));

    [Fact]
    public void Maskeleme_kimligi_gizler()
    {
        Assert.Equal("100******46", MaasHesabi.Maskele("10000000146"));
        Assert.Equal("****", MaasHesabi.Maskele("1234"));
        Assert.Null(MaasHesabi.Maskele(null));
    }

    [Fact]
    public void Gecersiz_kimlik_ve_iban_reddedilir()
    {
        Assert.Equal("tc_gecersiz", PersonelKarti.Olustur("PR1", Istek(tc: "12345678901"), null).Hata!.Kod);
        Assert.Equal("iban_gecersiz", PersonelKarti.Olustur("PR1", Istek(iban: "TR00"), null).Hata!.Kod);
        Assert.True(PersonelKarti.Olustur("PR1", Istek(tc: "10000000146", iban: "TR33 0006 1005 1978 6457 8413 26"), null).BasariliMi);
    }

    [Fact]
    public void Maas_yetkisi_olmayan_guncellemede_ozluk_bilgisi_degismez()
    {
        var p = PersonelKarti.Olustur("PR1", Istek(tc: "10000000146"), null).Deger;

        p.Guncelle(Istek(tc: "100******46", maas: 1) with { Gorev = "Mudur" }, null, false);

        Assert.Equal(("10000000146", 10000m, "Mudur"), (p.TcKimlik!, p.NetMaas, p.Gorev!));
    }

    [Fact]
    public void Cikis_giristen_once_olamaz() =>
        Assert.False(PersonelKarti.Olustur("PR1", Istek(), null).Deger.Cikis(new DateOnly(2025, 1, 1)).BasariliMi);

    [Fact]
    public void Satir_eksi_odenecek_kabul_etmez()
    {
        var s = Satir();

        Assert.Equal("eksi_odenecek", s.Guncelle(new MaasSatirIstegi(1000, 0, 800, 300, null)).Hata!.Kod);
        Assert.True(s.Guncelle(new MaasSatirIstegi(1000, 100, 100, 200, "prim")).BasariliMi);
        Assert.Equal(800m, s.Odenecek);
    }

    [Fact]
    public void Otomatik_avans_kesintisi_maasi_gecmez() =>
        Assert.Equal(5000m, Satir(5000, 8000).AvansKesintisi);

    [Fact]
    public void Odeme_kalani_asamaz_kismi_olur_iptalle_geri_doner()
    {
        var s = Satir(1000);

        Assert.True(s.OdemeEkle(400).BasariliMi);
        Assert.Equal(600m, s.Kalan);
        Assert.False(s.OdemeEkle(601).BasariliMi);
        Assert.False(s.OdemeEkle(0).BasariliMi);
        s.OdemeGeriAl(400);
        Assert.Equal(1000m, s.Kalan);
    }

    [Fact]
    public void Odemesi_olan_donem_geri_alinamaz()
    {
        var d = MaasDonemi.Olustur(2026, 3);
        var s = MaasSatiri.Olustur(d.Id, Guid.NewGuid(), null, 1000, 0);
        d.SatirEkle(s);
        Assert.True(d.Onayla().BasariliMi);
        Assert.False(d.Onayla().BasariliMi);
        s.OdemeEkle(100);

        Assert.Equal("donem_odemeli", d.GeriAl().Hata!.Kod);
        s.OdemeGeriAl(100);
        Assert.True(d.GeriAl().BasariliMi);
        Assert.Equal(MaasDonemiDurumu.Taslak, d.Durum);
    }

    [Fact]
    public void Mahsup_edilmis_avans_iptal_edilemez()
    {
        var a = Avans.Olustur(new AvansKaydetIstegi(Guid.NewGuid(), Gun, 1000, 2, null, null));
        a.Dus(500);
        Assert.Equal("avans_mahsuplu", a.IptalEt().Hata!.Kod);
        a.GeriEkle(500);
        Assert.True(a.IptalEt().BasariliMi);
        Assert.False(a.IptalEt().BasariliMi);
    }

    private static MasrafBeyani Masraf(Guid? beyanEden = null) =>
        MasrafBeyani.Olustur(Guid.NewGuid(), beyanEden, null, new MasrafKaydetIstegi(Guid.NewGuid(), Gun, MasrafKategorisi.Yemek, 250, "Musteri yemegi", null));

    [Fact]
    public void Kimse_kendi_masrafini_onaylayamaz()
    {
        var kisi = Guid.NewGuid();

        Assert.Equal("kendi_masrafi", Masraf(kisi).Karar(kisi, "A", null, true, null, Simdi).Hata!.Kod);
        Assert.Equal("kendi_masrafi", Masraf().Karar(kisi, "A", kisi, true, null, Simdi).Hata!.Kod);
    }

    [Fact]
    public void Masraf_red_gerekce_ister_duzeltilince_yeniden_onaya_doner()
    {
        var m = Masraf();
        var onayci = Guid.NewGuid();

        Assert.Equal("red_gerekcesi", m.Karar(onayci, "O", null, false, " ", Simdi).Hata!.Kod);
        Assert.True(m.Karar(onayci, "O", null, false, "Fis yok", Simdi).BasariliMi);
        Assert.Equal(MasrafDurumu.Reddedildi, m.Durum);
        Assert.True(m.Guncelle(new MasrafKaydetIstegi(m.PersonelId, Gun, MasrafKategorisi.Yol, 300, "Taksi", null)).BasariliMi);
        Assert.Equal((MasrafDurumu.Beyan, 300m, (string?)null), (m.Durum, m.Tutar, m.KararVeren));
    }

    [Fact]
    public void Masraf_yalniz_onayliyken_odenir_odenince_degismez()
    {
        var m = Masraf();
        Assert.False(m.Odendi().BasariliMi);

        m.Karar(Guid.NewGuid(), "O", null, true, null, Simdi);
        Assert.True(m.Odendi().BasariliMi);
        Assert.False(m.Guncelle(new MasrafKaydetIstegi(m.PersonelId, Gun, MasrafKategorisi.Yol, 1, "x", null)).BasariliMi);
        m.OdemeGeriAl();
        Assert.Equal(MasrafDurumu.Onaylandi, m.Durum);
    }

    [Fact]
    public void Zimmet_iadesi_ve_kaybi()
    {
        var z = Zimmet.Olustur(new ZimmetKaydetIstegi(Guid.NewGuid(), "Telefon", "S1", Gun, 5000, null));

        Assert.Equal("iade_tarihi", z.Kapat(new ZimmetIadeIstegi(Gun.AddDays(-1), false, null)).Hata!.Kod);
        Assert.True(z.Kapat(new ZimmetIadeIstegi(Gun.AddDays(5), true, "Calindi")).BasariliMi);
        Assert.Equal((ZimmetDurumu.Kayip, Gun.AddDays(5), "Calindi"), (z.Durum, z.IadeTarihi!.Value, z.Not!));
        Assert.False(z.Kapat(new ZimmetIadeIstegi(Gun.AddDays(6), false, null)).BasariliMi);
    }
}
