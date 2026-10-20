using System.Globalization;
using System.Text;
using ExcelDataReader;

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
        // .xls dosyaları Windows kod sayfalarıyla (1254) kodlanmış olabilir.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var akis = new MemoryStream(icerik);
        using var okuyucu = ExcelReaderFactory.CreateReader(akis);
        var satirlar = new List<string[]>();
        while (okuyucu.Read())
        {
            var hucreler = new string[okuyucu.FieldCount];
            for (var i = 0; i < okuyucu.FieldCount; i++)
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
            }
        }

        return satirlar;
    }
}
