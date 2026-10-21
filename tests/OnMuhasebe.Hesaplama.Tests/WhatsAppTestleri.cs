using OnMuhasebe.Shared.Contracts.Iletisim;

namespace OnMuhasebe.Hesaplama.Tests;

public class WhatsAppTestleri
{
    [Theory]
    [InlineData("0532 123 45 67", "905321234567")]
    [InlineData("532 123 4567", "905321234567")]
    [InlineData("+90 (532) 123-45-67", "905321234567")]
    [InlineData("0090 532 123 45 67", "905321234567")]
    [InlineData("0212 555 00 00", "902125550000")]
    [InlineData("+49 151 2345 6789", "4915123456789")]
    public void Numara_ulke_koduyla_yalniz_rakam(string telefon, string beklenen) =>
        Assert.Equal(beklenen, WhatsApp.Numara(telefon));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("dahili 45")]
    public void Taninmayan_numara_bos(string? telefon) => Assert.Null(WhatsApp.Numara(telefon));

    [Fact]
    public void Baglanti_mesaji_adres_icin_kodlar()
    {
        Assert.Equal("https://wa.me/905321234567?text=Say%C4%B1n%20M%C3%BC%C5%9Fteri%2C%0Abakiye%3A%201.000%2C00%20TL%20%26%20vade",
            WhatsApp.Baglanti("0532 123 45 67", "Sayın Müşteri,\nbakiye: 1.000,00 TL & vade"));
        Assert.StartsWith("https://wa.me/?text=", WhatsApp.Baglanti(null, "x"));
    }
}
