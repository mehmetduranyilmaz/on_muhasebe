using System.IO.Compression;
using System.Text;
using ExcelDataReader;

namespace OnMuhasebe.Shared.Infrastructure.Excel;

public sealed class ExcelSiniriAsildiException(string message) : Exception(message);

// Excel hücreleri olduğu gibi: sayı double, tarih DateTime, metin string. Boş satırlar atlanır, SatirNo dosyadaki gerçek satırdır.
public sealed record ExcelSatiri(int SatirNo, object?[] Hucreler);

// .xlsx ve eski .xls dosyalarının ilk sayfasını okur.
public static class ExcelDosyasi
{
    public const int AzamiSutun = 100;
    public const int AzamiZipGirdisi = 200;
    public const long AzamiAcikBoyut = 50L * 1024 * 1024;

    public static bool ExcelMi(byte[] icerik) =>
        icerik.Length > 8
        && ((icerik[0] == 'P' && icerik[1] == 'K') || (icerik[0] == 0xD0 && icerik[1] == 0xCF && icerik[2] == 0x11 && icerik[3] == 0xE0));

    public static List<ExcelSatiri> Oku(byte[] icerik, int azamiSatir, string satirAsimMesaji)
    {
        if (icerik[0] == 'P')
        {
            ZipBombasiDenetle(icerik);
        }

        // .xls dosyaları Windows kod sayfalarıyla (1254) kodlanmış olabilir.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var akis = new MemoryStream(icerik);
        using var okuyucu = ExcelReaderFactory.CreateReader(akis);
        var satirlar = new List<ExcelSatiri>();
        var satirNo = 0;
        while (okuyucu.Read())
        {
            satirNo++;
            var hucreler = new object?[Math.Min(okuyucu.FieldCount, AzamiSutun)];
            for (var i = 0; i < hucreler.Length; i++)
            {
                hucreler[i] = okuyucu.GetValue(i);
            }

            if (hucreler.Any(h => h is not null && (h is not string metin || metin.Trim().Length > 0)))
            {
                satirlar.Add(new ExcelSatiri(satirNo, hucreler));
                if (satirlar.Count > azamiSatir)
                {
                    throw new ExcelSiniriAsildiException(satirAsimMesaji);
                }
            }
        }

        return satirlar;
    }

    // .xlsx bir zip arşividir. Girdiler gerçekten açılarak sayılır: başlıktaki boyut bilgisi sahte yazılabilir.
    private static void ZipBombasiDenetle(byte[] icerik)
    {
        using var zip = new ZipArchive(new MemoryStream(icerik), ZipArchiveMode.Read);
        if (zip.Entries.Count > AzamiZipGirdisi)
        {
            throw new ExcelSiniriAsildiException("Excel dosyasının yapısı beklenenden büyük; dosya okunamadı.");
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
                if (toplam > AzamiAcikBoyut)
                {
                    throw new ExcelSiniriAsildiException("Excel dosyasının açılmış içeriği çok büyük (en fazla 50 MB); dosya okunamadı.");
                }
            }
        }
    }
}
