using System.Globalization;
using System.IO.Compression;
using System.Text;
using ExcelDataReader;
using OnMuhasebe.Modules.KasaBanka.Domain.Ekstre;

namespace OnMuhasebe.Modules.KasaBanka.Infrastructure.Ekstre;

// .xlsx ve eski .xls dosyalarının ilk sayfasını metin tablosuna çevirir. Tarihler gg.aa.yyyy, sayılar en az iki ondalıklı
// değişmez kültürle yazılır; böylece tutar çözücü binlik/ondalık ayracını karıştırmaz.
internal static class ExcelOkuyucu
{
    public static bool ExcelMi(byte[] icerik) =>
        icerik.Length > 8
        && ((icerik[0] == 'P' && icerik[1] == 'K') || (icerik[0] == 0xD0 && icerik[1] == 0xCF && icerik[2] == 0x11 && icerik[3] == 0xE0));

    public static List<string[]> Oku(byte[] icerik)
    {
        if (icerik[0] == 'P')
        {
            ZipBombasiDenetle(icerik);
        }

        // .xls dosyaları Windows kod sayfalarıyla (1254) kodlanmış olabilir.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var akis = new MemoryStream(icerik);
        using var okuyucu = ExcelReaderFactory.CreateReader(akis);
        var satirlar = new List<string[]>();
        while (okuyucu.Read())
        {
            var hucreler = new string[Math.Min(okuyucu.FieldCount, EkstreSinirlari.AzamiSutun)];
            for (var i = 0; i < hucreler.Length; i++)
            {
                hucreler[i] = okuyucu.GetValue(i) switch
                {
                    null => "",
                    DateTime tarih => tarih.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                    double sayi => sayi.ToString("0.00######", CultureInfo.InvariantCulture),
                    var deger => Convert.ToString(deger, CultureInfo.InvariantCulture)?.Trim() ?? ""
                };
            }

            if (hucreler.Any(h => h.Length > 0))
            {
                satirlar.Add(hucreler);
                EkstreSinirlari.SatirSayisiniDenetle(satirlar.Count);
            }
        }

        return satirlar;
    }

    // .xlsx bir zip arşividir. Girdiler gerçekten açılarak sayılır: başlıktaki boyut bilgisi sahte yazılabilir.
    private static void ZipBombasiDenetle(byte[] icerik)
    {
        using var zip = new ZipArchive(new MemoryStream(icerik), ZipArchiveMode.Read);
        if (zip.Entries.Count > EkstreSinirlari.AzamiZipGirdisi)
        {
            throw new EkstreSiniriAsildiException("Excel dosyasının yapısı beklenenden büyük; dosya okunamadı.");
        }

        var tampon = new byte[81_920];
        long toplam = 0;
        foreach (var girdi in zip.Entries)
        {
            using var girdiAkisi = girdi.Open();
            int okunan;
            while ((okunan = girdiAkisi.Read(tampon)) > 0)
            {
                toplam += okunan;
                if (toplam > EkstreSinirlari.AzamiAcikBoyut)
                {
                    throw new EkstreSiniriAsildiException("Excel dosyasının açılmış içeriği çok büyük (en fazla 50 MB); dosya okunamadı.");
                }
            }
        }
    }
}
