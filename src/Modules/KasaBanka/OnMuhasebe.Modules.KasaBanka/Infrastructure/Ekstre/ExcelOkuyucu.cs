using System.Globalization;
using OnMuhasebe.Modules.KasaBanka.Domain.Ekstre;
using OnMuhasebe.Shared.Infrastructure.Excel;

namespace OnMuhasebe.Modules.KasaBanka.Infrastructure.Ekstre;

// Ekstre dosyasının ilk sayfasını metin tablosuna çevirir. Tarihler gg.aa.yyyy, sayılar en az iki ondalıklı
// değişmez kültürle yazılır; böylece tutar çözücü binlik/ondalık ayracını karıştırmaz.
internal static class ExcelOkuyucu
{
    public static bool ExcelMi(byte[] icerik) => ExcelDosyasi.ExcelMi(icerik);

    public static List<string[]> Oku(byte[] icerik)
    {
        List<ExcelSatiri> satirlar;
        try
        {
            satirlar = ExcelDosyasi.Oku(icerik, EkstreSinirlari.AzamiSatir, EkstreSinirlari.SatirAsimMesaji);
        }
        catch (ExcelSiniriAsildiException hata)
        {
            throw new EkstreSiniriAsildiException(hata.Message);
        }

        return satirlar.Select(s => s.Hucreler.Select(h => h switch
        {
            null => "",
            DateTime tarih => tarih.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
            double sayi => sayi.ToString("0.00######", CultureInfo.InvariantCulture),
            var deger => Convert.ToString(deger, CultureInfo.InvariantCulture)?.Trim() ?? ""
        }).ToArray()).Where(h => h.Any(x => x.Length > 0)).ToList();
    }
}
