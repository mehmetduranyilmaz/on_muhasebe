namespace OnMuhasebe.Shared.Domain;

// Muhasebe tarihleri (işlem günü, kur bülteni) Türkiye saatine göre belirlenir; sunucu UTC'de çalışsa da.
public static class TurkiyeSaati
{
    private static readonly TimeZoneInfo _bolge = BolgeBul();

    public static DateOnly Bugun(TimeProvider zaman) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(zaman.GetUtcNow(), _bolge).DateTime);

    private static TimeZoneInfo BolgeBul()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        }
        catch (TimeZoneNotFoundException)
        {
            // Saat dilimi verisi olmayan konteynerler için: Türkiye 2016'dan beri sabit UTC+3.
            return TimeZoneInfo.CreateCustomTimeZone("Turkiye", TimeSpan.FromHours(3), "Türkiye", "Türkiye");
        }
    }
}
