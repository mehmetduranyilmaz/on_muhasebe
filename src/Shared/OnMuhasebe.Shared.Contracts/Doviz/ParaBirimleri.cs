namespace OnMuhasebe.Shared.Contracts.Doviz;

public sealed record ParaBirimi(string Kod, string Ad);

public static class ParaBirimleri
{
    public const string Tl = "TRY";

    // TCMB'nin günlük kur bülteninde yayımladığı para birimleri (XDR hariç: gerçek para değil).
    public static IReadOnlyList<ParaBirimi> Tumu { get; } =
    [
        new(Tl, "Türk Lirası"),
        new("USD", "ABD Doları"),
        new("EUR", "Euro"),
        new("GBP", "İngiliz Sterlini"),
        new("CHF", "İsviçre Frangı"),
        new("AUD", "Avustralya Doları"),
        new("CAD", "Kanada Doları"),
        new("DKK", "Danimarka Kronu"),
        new("SEK", "İsveç Kronu"),
        new("NOK", "Norveç Kronu"),
        new("JPY", "Japon Yeni"),
        new("KWD", "Kuveyt Dinarı"),
        new("SAR", "Suudi Arabistan Riyali"),
        new("QAR", "Katar Riyali"),
        new("AED", "BAE Dirhemi"),
        new("RON", "Rumen Leyi"),
        new("RUB", "Rus Rublesi"),
        new("CNY", "Çin Yuanı"),
        new("PKR", "Pakistan Rupisi"),
        new("KRW", "Güney Kore Wonu"),
        new("AZN", "Azerbaycan Manatı"),
        new("KZT", "Kazakistan Tengesi")
    ];

    public static bool GecerliMi(string? kod) => kod is not null && Tumu.Any(p => p.Kod == kod);

    public static string Ad(string kod) => Tumu.FirstOrDefault(p => p.Kod == kod)?.Ad ?? kod;
}
