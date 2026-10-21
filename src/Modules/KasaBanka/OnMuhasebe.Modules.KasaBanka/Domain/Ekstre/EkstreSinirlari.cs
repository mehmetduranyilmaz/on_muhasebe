namespace OnMuhasebe.Modules.KasaBanka.Domain.Ekstre;

// Ekstre dosyası sınırları: küçük ama özel hazırlanmış bir dosya (zip bombası, milyonlarca satır) sunucunun belleğini
// ve işlemcisini tüketip tüm firmaları etkilemesin. Gerçek bir banka ekstresi bu sınırların çok altındadır.
internal static class EkstreSinirlari
{
    public const int AzamiSatir = 5_000;
    public const int AzamiSutun = 100;
    public const int AzamiZipGirdisi = 200;
    public const long AzamiAcikBoyut = 50L * 1024 * 1024;

    // Tek satır tutarı; üstü hatalı veri sayılır (kasa/banka işlemindeki sınırla aynı).
    public const decimal AzamiTutar = 1_000_000_000_000m;

    public static void SatirSayisiniDenetle(int satirSayisi)
    {
        if (satirSayisi > AzamiSatir)
        {
            throw new EkstreSiniriAsildiException($"Ekstrede en fazla {AzamiSatir:N0} satır olabilir; dosyayı tarih aralığına göre bölün.");
        }
    }
}

internal sealed class EkstreSiniriAsildiException(string message) : Exception(message);
