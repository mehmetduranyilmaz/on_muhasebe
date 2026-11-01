using System.IO.Compression;
using System.Text;
using OnMuhasebe.Modules.Dosya.Domain;

namespace OnMuhasebe.Hesaplama.Tests;

public class DosyaTurleriTestleri
{
    private static MemoryStream Akis(params byte[] baytlar) => new(baytlar);

    private static MemoryStream Metin(string metin) => new(Encoding.UTF8.GetBytes(metin));

    private static MemoryStream Office()
    {
        var akis = new MemoryStream();
        using (var zip = new ZipArchive(akis, ZipArchiveMode.Create, leaveOpen: true))
        {
            zip.CreateEntry("[Content_Types].xml");
        }

        return akis;
    }

    [Fact]
    public void Gecerli_turler_kabul_edilir()
    {
        Assert.True(DosyaTurleri.GecerliMi("a.pdf", Metin("%PDF-1.7 x")));
        Assert.True(DosyaTurleri.GecerliMi("a.JPG", Akis(0xFF, 0xD8, 0xFF, 0xE0)));
        Assert.True(DosyaTurleri.GecerliMi("a.jpeg", Akis(0xFF, 0xD8, 0xFF, 0xE1)));
        Assert.True(DosyaTurleri.GecerliMi("a.png", Akis(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)));
        Assert.True(DosyaTurleri.GecerliMi("a.xls", Akis(0xD0, 0xCF, 0x11, 0xE0, 1)));
        Assert.True(DosyaTurleri.GecerliMi("a.doc", Akis(0xD0, 0xCF, 0x11, 0xE0, 1)));
        Assert.True(DosyaTurleri.GecerliMi("a.xlsx", Office()));
        Assert.True(DosyaTurleri.GecerliMi("a.docx", Office()));
        Assert.True(DosyaTurleri.GecerliMi("a.xml", Metin("  <?xml version=\"1.0\"?><a/>")));
        Assert.True(DosyaTurleri.GecerliMi("a.xml", new MemoryStream([.. Encoding.UTF8.Preamble, (byte)'<', (byte)'a'])));
    }

    [Theory]
    [InlineData("a.exe")]
    [InlineData("a.html")]
    [InlineData("a.svg")]
    [InlineData("a.js")]
    [InlineData("a")]
    [InlineData("a.pdf.exe")]
    public void Izin_verilmeyen_uzanti_reddedilir(string ad) => Assert.False(DosyaTurleri.GecerliMi(ad, Metin("%PDF-1.7")));

    [Theory]
    [InlineData("a.pdf", "MZ calisabilir")]
    [InlineData("a.png", "%PDF-1.7")]
    [InlineData("a.jpg", "metin")]
    [InlineData("a.xml", "duz metin")]
    [InlineData("a.xlsx", "duz metin")]
    [InlineData("a.doc", "duz metin")]
    public void Icerigi_uzantisiyla_uyusmayan_reddedilir(string ad, string icerik) => Assert.False(DosyaTurleri.GecerliMi(ad, Metin(icerik)));

    [Fact]
    public void Office_gorunumlu_siradan_zip_reddedilir()
    {
        var akis = new MemoryStream();
        using (var zip = new ZipArchive(akis, ZipArchiveMode.Create, leaveOpen: true))
        {
            zip.CreateEntry("zararli.exe");
        }

        Assert.False(DosyaTurleri.GecerliMi("a.xlsx", akis));
    }

    [Fact]
    public void Denetim_sonrasi_akis_basa_doner()
    {
        var akis = Metin("%PDF-1.7 x");
        DosyaTurleri.GecerliMi("a.pdf", akis);

        Assert.Equal(0, akis.Position);
    }

    [Theory]
    [InlineData("../../etc/passwd.pdf", "passwd.pdf")]
    [InlineData(@"C:\Users\ali\sozlesme.pdf", "sozlesme.pdf")]
    [InlineData("  rapor.pdf ", "rapor.pdf")]
    [InlineData("kot\u0001u.pdf", "kotu.pdf")]
    public void Dosya_adi_temizlenir(string ad, string beklenen) => Assert.Equal(beklenen, DosyaTurleri.AdTemizle(ad));

    [Fact]
    public void Uzun_ad_uzantisini_koruyarak_kisalir()
    {
        var sonuc = DosyaTurleri.AdTemizle(new string('a', 300) + ".pdf");

        Assert.Equal(200, sonuc.Length);
        Assert.EndsWith(".pdf", sonuc);
    }

    [Fact]
    public void Indirme_turu_uzantidan_gelir_bilinmeyen_ikili_verilir()
    {
        Assert.Equal("application/pdf", DosyaTurleri.IcerikTuru("a.PDF"));
        Assert.Equal("application/octet-stream", DosyaTurleri.IcerikTuru("a.html"));
    }
}
