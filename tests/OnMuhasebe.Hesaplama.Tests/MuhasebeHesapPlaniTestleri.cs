using OnMuhasebe.Modules.Muhasebe.Contracts;
using OnMuhasebe.Modules.Muhasebe.Domain;

namespace OnMuhasebe.Hesaplama.Tests;

public class MuhasebeHesapPlaniTestleri
{
    private static HesapPlani Plan(string ayirac = ".", string? altGrup = "01", Dictionary<HesapRolu, string>? kodlar = null) =>
        new(kodlar ?? [], ayirac, altGrup);

    [Fact]
    public void Varsayilanlar_tek_duzen_hesap_planidir()
    {
        Assert.Equal("120", Plan().Kod(HesapRolu.Alicilar));
        Assert.Equal("391", Plan().Kod(HesapRolu.HesaplananKdv));
        Assert.Equal("191", Plan().Kod(HesapRolu.IndirilecekKdv));
        Assert.All(HesapRolleri.Tumu, r => Assert.False(string.IsNullOrWhiteSpace(HesapRolleri.Varsayilan(r))));
    }

    [Fact]
    public void Cari_alt_hesabi_kod_numarasindan_uretilir()
    {
        Assert.Equal("120.01.00012", Plan().CariKodu("C00012", tedarikciMi: false));
        Assert.Equal("320.01.00012", Plan().CariKodu("C00012", tedarikciMi: true));
    }

    [Fact]
    public void Alt_grup_bossa_ve_ayirac_farkliysa()
    {
        Assert.Equal("120-00012", Plan("-", null).CariKodu("C00012", tedarikciMi: false));
        Assert.Equal("120 01 00012", Plan(" ", "01").CariKodu("C00012", tedarikciMi: false));
    }

    [Fact]
    public void Firmanin_ana_kodu_varsayilani_ezer() =>
        Assert.Equal("120.10.01.00007", Plan(kodlar: new() { [HesapRolu.Alicilar] = "120.10" }).CariKodu("C00007", tedarikciMi: false));

    [Fact]
    public void Rakamsiz_kart_kodu_oldugu_gibi_kullanilir() =>
        Assert.Equal("ANKARA", HesapPlani.Numara("ANKARA"));

    [Fact]
    public void Kasa_banka_ve_kategori_kodlari()
    {
        Assert.Equal("100.0001", Plan().HesapKoduOtomatik("KS0001", "Kasa"));
        Assert.Equal("102.0003", Plan().HesapKoduOtomatik("BN0003", "Banka"));
        Assert.Equal("309.0001", Plan().HesapKoduOtomatik("KK0001", "KrediKarti"));
        Assert.Equal("108.0002", Plan().HesapKoduOtomatik("PS0002", "Pos"));
        Assert.Equal("649", Plan().KategoriKodu("Gelir"));
        Assert.Equal("770", Plan().KategoriKodu("Gider"));
    }
}
