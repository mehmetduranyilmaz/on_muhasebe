using System.IO.Compression;
using System.Text;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.KasaBanka.Domain.Ekstre;
using OnMuhasebe.Modules.KasaBanka.Infrastructure.Ekstre;

namespace OnMuhasebe.Hesaplama.Tests;

// Özel hazırlanmış ekstre dosyaları sunucuyu yormasın: zip bombası, satır/sütun sınırı, aşırı tutar, geçersiz kolon.
public class EkstreSinirTestleri
{
    [Fact]
    public void Zip_bombasi_acilmadan_reddedilir()
    {
        // 60 MB sıfır birkaç yüz KB'a sıkışır; açılmış boyut sınırı (50 MB) aşılınca okuma durur.
        using var akis = new MemoryStream();
        using (var zip = new ZipArchive(akis, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var girdi = zip.CreateEntry("xl/sharedStrings.xml", CompressionLevel.SmallestSize).Open();
            var sifirlar = new byte[1024 * 1024];
            for (var i = 0; i < 60; i++)
            {
                girdi.Write(sifirlar);
            }
        }

        var icerik = akis.ToArray();
        Assert.True(icerik.Length < 5 * 1024 * 1024);
        var hata = Assert.Throws<EkstreSiniriAsildiException>(() => ExcelOkuyucu.Oku(icerik));
        Assert.Contains("50 MB", hata.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Cok_girdili_zip_reddedilir()
    {
        using var akis = new MemoryStream();
        using (var zip = new ZipArchive(akis, ZipArchiveMode.Create, leaveOpen: true))
        {
            for (var i = 0; i <= EkstreSinirlari.AzamiZipGirdisi; i++)
            {
                zip.CreateEntry($"x{i}.xml");
            }
        }

        Assert.Throws<EkstreSiniriAsildiException>(() => ExcelOkuyucu.Oku(akis.ToArray()));
    }

    [Fact]
    public void Csv_satir_siniri_asilinca_durur()
    {
        var metin = new StringBuilder("Tarih;Aciklama;Tutar\n");
        for (var i = 0; i <= EkstreSinirlari.AzamiSatir; i++)
        {
            metin.Append("01.10.2026;a;1\n");
        }

        Assert.Throws<EkstreSiniriAsildiException>(() => CsvOkuyucu.Oku(Encoding.UTF8.GetBytes(metin.ToString())));
    }

    [Fact]
    public void Csv_sinir_icindeki_dosyayi_okur_ve_sutunlari_kirpar()
    {
        var genis = string.Join(';', Enumerable.Range(0, 500).Select(i => $"k{i}"));
        var tablo = CsvOkuyucu.Oku(Encoding.UTF8.GetBytes($"{genis}\n{genis}\n"));
        Assert.Equal(2, tablo.Count);
        Assert.Equal(EkstreSinirlari.AzamiSutun, tablo[0].Length);
    }

    [Fact]
    public void Mt940_satir_siniri_asilinca_durur()
    {
        var metin = new StringBuilder(":20:REF\n:25:TR123\n:60F:C261001TRY1000,00\n");
        for (var i = 0; i <= EkstreSinirlari.AzamiSatir; i++)
        {
            metin.Append(":61:2610011001C10,00NTRFNONREF\n:86:Havale\n");
        }

        Assert.Throws<EkstreSiniriAsildiException>(() => Mt940Okuyucu.Oku(Encoding.UTF8.GetBytes(metin.ToString())));
    }

    [Theory]
    [InlineData("99999999999999999999999999999,00")]
    [InlineData("1.000.000.000.000,00")]
    public void Asiri_tutar_okunmaz(string metin) => Assert.Null(EkstreMetni.Tutar(metin));

    [Fact]
    public void Sinir_altindaki_buyuk_tutar_okunur() => Assert.Equal(999_999_999_999.99m, EkstreMetni.Tutar("999.999.999.999,99"));

    [Fact]
    public void Negatif_kolon_numarasi_satiri_bozmaz()
    {
        string[][] tablo = [["Tarih", "Aciklama", "Tutar"], ["05.10.2026", "Havale", "100,00"]];
        var esleme = new KolonEslemesi(0, 0, 1, 2, null, null, -5, -3, false);
        var cozum = SatirCozucu.Coz(tablo, esleme);
        Assert.Single(cozum.Satirlar);
        Assert.Null(cozum.Satirlar[0].Bakiye);
    }
}
