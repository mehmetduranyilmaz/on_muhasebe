using Microsoft.Extensions.Configuration;
using OnMuhasebe.Modules.Bildirim.Contracts;
using OnMuhasebe.Modules.Bildirim.Domain;
using OnMuhasebe.Modules.Bildirim.Infrastructure;
using OnMuhasebe.Shared.Contracts.Bildirimler;
using OnMuhasebe.Shared.Contracts.Kimlik;

namespace OnMuhasebe.Hesaplama.Tests;

public class BildirimTestleri
{
    [Fact]
    public void Tercihi_olmayan_yonetici_alir_kullanici_almaz()
    {
        Assert.True(AliciKurallari.Alir(Roller.Yonetici, AliciKurallari.Varsayilan(Roller.Yonetici), BildirimTuru.AlinanEvrakVadesi));
        Assert.False(AliciKurallari.Alir(Roller.Kullanici, AliciKurallari.Varsayilan(Roller.Kullanici), BildirimTuru.AlinanEvrakVadesi));
        Assert.Equal(EpostaSikligi.GunlukOzet, AliciKurallari.Varsayilan(Roller.Yonetici).Eposta);
    }

    [Fact]
    public void Kapatilan_tur_gelmez()
    {
        var tercih = new BildirimTercihleri(true, [BildirimTuru.RiskLimiti], EpostaSikligi.Kapali);

        Assert.False(AliciKurallari.Alir(Roller.Yonetici, tercih, BildirimTuru.RiskLimiti));
        Assert.True(AliciKurallari.Alir(Roller.Yonetici, tercih, BildirimTuru.GecikmisEvrak));
    }

    [Theory]
    [InlineData(BildirimTuru.AbonelikBitiyor)]
    [InlineData(BildirimTuru.OdemeBildirimiSonucu)]
    public void Abonelik_bildirimi_isteyen_kullaniciya_da_gitmez(BildirimTuru tur)
    {
        var acik = new BildirimTercihleri(true, [], EpostaSikligi.Aninda);

        Assert.False(AliciKurallari.Alir(Roller.Kullanici, acik, tur));
        Assert.True(AliciKurallari.Alir(Roller.Kullanici, acik, BildirimTuru.AlinanEvrakVadesi));
    }

    [Fact]
    public void Gunluk_ozet_sabah_sekizden_sonra_gunde_bir_kez()
    {
        var bugun = new DateOnly(2026, 10, 21);

        Assert.False(AliciKurallari.OzetZamaniMi(new DateTime(2026, 10, 21, 7, 59, 0), null));
        Assert.True(AliciKurallari.OzetZamaniMi(new DateTime(2026, 10, 21, 8, 0, 0), null));
        Assert.True(AliciKurallari.OzetZamaniMi(new DateTime(2026, 10, 21, 9, 0, 0), bugun.AddDays(-1)));
        Assert.False(AliciKurallari.OzetZamaniMi(new DateTime(2026, 10, 21, 18, 0, 0), bugun));
    }

    private static SifreKoruyucu Koruyucu(string? anahtar) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [SifreKoruyucu.AyarAdi] = anahtar }).Build());

    [Fact]
    public void Smtp_sifresi_sifrelenip_cozulur_ve_her_seferinde_farkli_saklanir()
    {
        var k = Koruyucu(Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray()));

        var bir = k.Sifrele("Gizli Şifre 123");
        var iki = k.Sifrele("Gizli Şifre 123");

        Assert.NotEqual(bir, iki);
        Assert.Equal("Gizli Şifre 123", k.Coz(bir));
        Assert.DoesNotContain("Gizli", bir, StringComparison.Ordinal);
    }

    [Fact]
    public void Anahtar_yoksa_ya_da_kisaysa_koruyucu_hazir_degil()
    {
        Assert.False(Koruyucu(null).HazirMi);
        Assert.False(Koruyucu(Convert.ToBase64String(new byte[16])).HazirMi);
        Assert.Null(Koruyucu(null).Coz("abc"));
    }

    [Fact]
    public void Eposta_icerigi_html_kodlanir_ve_baglanti_uygulama_adresine_gore_kurulur()
    {
        var b = Bildirim.Olustur(Guid.NewGuid(),
            new BildirimAdayi(BildirimTuru.RiskLimiti, "risk:1", "Risk <script>", "A & B", "cari/1", BildirimOnemi.Uyari), true);

        var html = EpostaPostacisi.Html("Ali <Veli>", [b], "https://app.ornek.com/");

        Assert.Contains("Ali &lt;Veli&gt;", html);
        Assert.Contains("Risk &lt;script&gt;", html);
        Assert.Contains("A &amp; B", html);
        Assert.Contains("href=\"https://app.ornek.com/cari/1\"", html);
        Assert.DoesNotContain("<script>", html);
    }
}
