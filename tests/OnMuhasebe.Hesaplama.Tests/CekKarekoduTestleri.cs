using OnMuhasebe.Modules.KasaBanka.Domain.CekSenet;
using OnMuhasebe.Shared.Domain.Dogrulama;

namespace OnMuhasebe.Hesaplama.Tests;

public class CekKarekoduTestleri
{
    private const string Vkn = "9876543217";
    private const string Tckn = "10000000146";
    private static readonly string IsBankasiIban = TrIban("00064", "0000112345678901");

    // TR + kontrol hanesi + 5 hane banka + 0 + 16 hane hesap; kontrol hanesi ISO 13616 mod 97 ile hesaplanır.
    private static string TrIban(string banka, string hesap)
    {
        var bban = banka + "0" + hesap;
        var kalan = (bban + "292700").Aggregate(0, (k, c) => (k * 10 + (c - '0')) % 97);
        return $"TR{98 - kalan:00}{bban}";
    }

    [Fact]
    public void Anahtar_deger_bicimi_okunur()
    {
        var c = CekKarekodu.Coz($"SERI:1234567;BANKA:0064;SUBE:1234;HESAP:0012345;VKN:{Vkn}");

        Assert.Equal(("1234567", "0064", "1234", "0012345", Vkn), (c.SeriNo, c.BankaKodu, c.SubeKodu, c.HesapNo, c.KimlikNo));
    }

    [Fact]
    public void Adres_parametreleri_okunur()
    {
        var c = CekKarekodu.Coz($"https://karekod.example/cek?seriNo=7654321&tckn={Tckn}&bankaKodu=46");

        Assert.Equal(("7654321", "0046", Tckn), (c.SeriNo, c.BankaKodu, c.KimlikNo));
    }

    [Fact]
    public void Ayracli_alanlarda_banka_kodundan_sonra_sube_ve_hesap_gelir()
    {
        var c = CekKarekodu.Coz($"0062|0335|6299431|1234567|{Vkn}");

        Assert.Equal(("0062", "0335", "6299431", "1234567", Vkn), (c.BankaKodu, c.SubeKodu, c.HesapNo, c.SeriNo, c.KimlikNo));
    }

    [Fact]
    public void Iban_banka_kodunu_ve_hesabi_verir()
    {
        var c = CekKarekodu.Coz($"{IsBankasiIban} 0000123 {Tckn}");

        Assert.Equal((IsBankasiIban, "0064", IsBankasiIban, Tckn), (c.Iban, c.BankaKodu, c.HesapNo, c.KimlikNo));
        Assert.Equal("0000123", c.SeriNo);
    }

    [Fact]
    public void Kontrol_hanesi_tutmayan_numara_kimlik_sayilmaz()
    {
        var gecersiz = new[] { "1234567891", "1234567892", "1234567893", "1234567894" }.First(v => !VergiKimlik.GecerliMi(v));
        var c = CekKarekodu.Coz($"SERI=ABC123456;VKN={gecersiz}");

        Assert.Null(c.KimlikNo);
        Assert.Equal("ABC123456", c.SeriNo);
    }

    [Fact]
    public void Taninmayan_icerikte_alanlar_bos_kalir()
    {
        var c = CekKarekodu.Coz("X1Y2Z3");

        Assert.Equal(new KarekodCozumu(null, null, null, null, null, null), c);
    }

    [Fact]
    public void Banka_adi_koddan_bulunur()
    {
        Assert.Equal("Türkiye İş Bankası", BankaKodlari.Ad("0064"));
        Assert.Null(BankaKodlari.Ad("9999"));
    }
}
