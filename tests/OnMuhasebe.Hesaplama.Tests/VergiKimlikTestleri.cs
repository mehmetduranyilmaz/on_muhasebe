using OnMuhasebe.Shared.Domain.Dogrulama;

namespace OnMuhasebe.Hesaplama.Tests;

public class VergiKimlikTestleri
{
    [Theory]
    [InlineData("1234567890")] // tüm ara değerler 0: kontrol hanesi 0
    [InlineData("9876543217")] // elle hesaplandı: ara değerler toplamı 33, kontrol hanesi 7
    public void Gecerli_vkn_kabul_edilir(string vkn) => Assert.True(VergiKimlik.VknGecerliMi(vkn));

    [Theory]
    [InlineData("9876543210")] // kontrol hanesi yanlış
    [InlineData("1234567891")]
    [InlineData("123456789")]
    [InlineData("12345678901")]
    [InlineData("12345abc90")]
    [InlineData("")]
    [InlineData(null)]
    public void Gecersiz_vkn_reddedilir(string? vkn) => Assert.False(VergiKimlik.VknGecerliMi(vkn));

    [Fact]
    public void Gecerli_tckn_kabul_edilir() =>
        // 10. hane: ((1+0+0+0+1)*7 - 0) mod 10 = 4; 11. hane: (1+1+4) mod 10 = 6
        Assert.True(VergiKimlik.TcknGecerliMi("10000000146"));

    [Theory]
    [InlineData("10000000147")] // 11. hane yanlış
    [InlineData("10000000156")] // 10. hane yanlış
    [InlineData("00000000146")] // sıfırla başlayamaz
    [InlineData("1000000014")]
    public void Gecersiz_tckn_reddedilir(string tckn) => Assert.False(VergiKimlik.TcknGecerliMi(tckn));

    [Theory]
    [InlineData("1234567890", true)]
    [InlineData("10000000146", true)]
    [InlineData("9876543210", false)]
    [InlineData("123", false)]
    public void Uzunluga_gore_dogru_algoritma_secilir(string numara, bool beklenen) =>
        Assert.Equal(beklenen, VergiKimlik.GecerliMi(numara));
}
