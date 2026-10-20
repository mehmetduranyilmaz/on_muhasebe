using OnMuhasebe.Shared.Domain.Dogrulama;

namespace OnMuhasebe.Hesaplama.Tests;

public class IbanTestleri
{
    [Theory]
    [InlineData("TR330006100519786457841326")]
    [InlineData("TR33 0006 1005 1978 6457 8413 26")]
    [InlineData("tr330006100519786457841326")]
    [InlineData("DE89370400440532013000")]
    public void Gecerli_iban_kabul_edilir(string iban) => Assert.True(Iban.GecerliMi(iban));

    [Theory]
    [InlineData("TR330006100519786457841327")]
    [InlineData("TR3300061005197864578413")]
    [InlineData("TR33000610051978645784132A")]
    [InlineData("1234")]
    [InlineData("")]
    [InlineData(null)]
    public void Gecersiz_iban_reddedilir(string? iban) => Assert.False(Iban.GecerliMi(iban));

    [Fact]
    public void Bosluklar_silinir_harfler_buyutulur() =>
        Assert.Equal("TR330006100519786457841326", Iban.Normalize(" tr33 0006 1005 1978 6457 8413 26 "));
}
