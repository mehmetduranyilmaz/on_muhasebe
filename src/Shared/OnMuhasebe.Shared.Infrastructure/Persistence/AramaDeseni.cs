namespace OnMuhasebe.Shared.Infrastructure.Persistence;

// ILIKE için "içerir" deseni. Kullanıcı girdisindeki % ve _ joker değil harfiyen aranır (PostgreSQL LIKE'ın varsayılan
// kaçış karakteri ters bölüdür): "%_%_%..." gibi girdiler pahalı taramaya dönüşmesin. Metin makul uzunlukta kırpılır.
public static class AramaDeseni
{
    public const int AzamiUzunluk = 100;

    public static string Icerir(string arama)
    {
        var metin = arama.Trim();
        if (metin.Length > AzamiUzunluk)
        {
            metin = metin[..AzamiUzunluk];
        }

        return "%" + metin.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal) + "%";
    }
}
