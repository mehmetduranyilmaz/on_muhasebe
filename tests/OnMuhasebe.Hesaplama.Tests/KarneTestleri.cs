using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.KasaBanka.Domain.CekSenet;

namespace OnMuhasebe.Hesaplama.Tests;

public class KarneTestleri
{
    private static readonly DateOnly _tarih = new(2026, 10, 21);

    private static EvrakKarnesi Karne(string baslangic = "ABC 0001098", int yaprak = 5) =>
        EvrakKarnesi.Olustur(EvrakTuru.Cek, Guid.NewGuid(), baslangic, yaprak, _tarih, null).Deger;

    [Theory]
    [InlineData("ABC 1234567", "ABC", 1234567, 7)]
    [InlineData("a-0045", "A-", 45, 4)]
    [InlineData("0001", "", 1, 4)]
    public void Seri_onek_sayi_ve_haneye_ayrilir(string seri, string onek, long sayi, int hane) =>
        Assert.Equal((onek, sayi, hane), EvrakSeriNo.Ayir(seri));

    [Theory]
    [InlineData("ABC")]
    [InlineData("")]
    [InlineData("1234567890123456789")]
    public void Sayi_ile_bitmeyen_ya_da_cok_uzun_seri_ayrilmaz(string seri) => Assert.Null(EvrakSeriNo.Ayir(seri));

    [Fact]
    public void Ardisik_seriler_onek_ve_sifirlari_korur()
    {
        Assert.Equal(["X0098", "X0099", "X0100", "X0101"], EvrakSeriNo.Ardisik("x 0098", 4));
        Assert.Equal(["999", "1000"], EvrakSeriNo.Ardisik("999", 2));
    }

    [Fact]
    public void Karne_araligi_ve_bitis_serisi()
    {
        var k = Karne();

        Assert.Equal(["ABC0001098", "ABC0001099", "ABC0001100", "ABC0001101", "ABC0001102"], k.Seriler());
        Assert.True(k.Icerir("abc 0001102"));
        Assert.False(k.Icerir("ABC0001103"));
        Assert.False(k.Icerir("ABD0001100"));
    }

    [Fact]
    public void Siradaki_yaprak_kullanilan_ve_iptal_edilenleri_atlar()
    {
        var k = Karne();
        var kullanilan = new HashSet<string> { "ABC0001098" };
        Assert.True(k.YapragiIptalEt("ABC 0001099", "Yırtıldı", _tarih, kullanilan).BasariliMi);

        Assert.Equal("ABC0001100", k.Siradaki(kullanilan));
        Assert.Equal(3, k.Kalan(kullanilan));
    }

    [Fact]
    public void Kullanilan_iptal_edilmis_ya_da_aralik_disi_yaprak_iptal_edilemez()
    {
        var k = Karne();
        var kullanilan = new HashSet<string> { "ABC0001098" };
        k.YapragiIptalEt("ABC0001100", "Kayıp", _tarih, kullanilan);

        Assert.Equal("karne_yapragi_kullanildi", k.YapragiIptalEt("ABC0001098", "x", _tarih, kullanilan).Hata!.Kod);
        Assert.Equal("karne_yapragi_iptal", k.YapragiIptalEt("ABC0001100", "x", _tarih, kullanilan).Hata!.Kod);
        Assert.Equal("karne_seri_disinda", k.YapragiIptalEt("ABC0002000", "x", _tarih, kullanilan).Hata!.Kod);
        Assert.True(k.IptaliGeriAl("abc0001100").BasariliMi);
        Assert.False(k.IptalMi("ABC0001100"));
    }

    [Fact]
    public void Dolu_karnede_siradaki_yok()
    {
        var k = Karne(yaprak: 2);

        Assert.Null(k.Siradaki(new HashSet<string> { "ABC0001098", "ABC0001099" }));
    }

    [Fact]
    public void Ayni_hesap_ve_onekte_kesisen_aralik_cakisir()
    {
        var hesap = Guid.NewGuid();
        var a = EvrakKarnesi.Olustur(EvrakTuru.Cek, hesap, "ABC0001000", 25, _tarih, null).Deger;
        var kesisen = EvrakKarnesi.Olustur(EvrakTuru.Cek, hesap, "ABC0001024", 25, _tarih, null).Deger;
        var bitisik = EvrakKarnesi.Olustur(EvrakTuru.Cek, hesap, "ABC0001025", 25, _tarih, null).Deger;
        var baskaHesap = EvrakKarnesi.Olustur(EvrakTuru.Cek, Guid.NewGuid(), "ABC0001000", 25, _tarih, null).Deger;

        Assert.True(a.Cakisir(kesisen));
        Assert.False(a.Cakisir(bitisik));
        Assert.False(a.Cakisir(baskaHesap));
    }

    [Fact]
    public void Senet_defteri_bankaya_baglanmaz()
    {
        var defter = EvrakKarnesi.Olustur(EvrakTuru.Senet, Guid.NewGuid(), "1", 50, _tarih, "Senet defteri").Deger;

        Assert.Null(defter.KendiHesapId);
        Assert.Equal("50", defter.Seri(defter.BitisNo));
    }
}
