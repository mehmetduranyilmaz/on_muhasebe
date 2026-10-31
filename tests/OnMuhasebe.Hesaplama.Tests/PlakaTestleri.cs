using OnMuhasebe.Modules.Fatura.Features;

namespace OnMuhasebe.Hesaplama.Tests;

public class PlakaTestleri
{
    [Theory]
    [InlineData("34ABC123")]
    [InlineData("34 abc 123")]
    [InlineData("06A1234")]
    [InlineData("81 Z 99")]
    [InlineData("01 AB 1234")]
    public void Gecerli_plakalar_kabul_edilir(string plaka) => Assert.True(IrsaliyeIslemleri.PlakaGecerliMi(plaka));

    [Theory]
    [InlineData("00ABC123")]  // il kodu 00 yok
    [InlineData("82ABC123")]  // il kodu 81'i aşamaz
    [InlineData("34ABCD123")] // en çok 3 harf
    [InlineData("34ABC1")]    // en az 2 rakam
    [InlineData("34ABC12345")] // en çok 4 rakam
    [InlineData("ABC123")]
    public void Gecersiz_plakalar_reddedilir(string plaka) => Assert.False(IrsaliyeIslemleri.PlakaGecerliMi(plaka));
}
