using System.Globalization;
using System.Xml.Linq;
using OnMuhasebe.Shared.Contracts.Doviz;

namespace OnMuhasebe.Modules.Doviz.Domain;

internal static class KurKurallari
{
    public const int AzamiGeriGun = 10;

    // Bir işlem için önceki iş gününün bülteni geçerlidir (TCMB kurları ilan edildiği gün 15:30'da
    // açıklanır, ertesi gün uygulanır). Hafta sonları atlanır; resmi tatiller bülten olmamasından anlaşılır.
    public static IEnumerable<DateOnly> AdayBultenTarihleri(DateOnly islemTarihi)
    {
        for (var gun = 1; gun <= AzamiGeriGun; gun++)
        {
            var tarih = islemTarihi.AddDays(-gun);
            if (tarih.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                yield return tarih;
            }
        }
    }

    // Kurlar 1 birime çevrilir (ör. JPY bültende 100 birim üzerinden yayımlanır).
    public static IReadOnlyList<TcmbKuru> BulteniOku(string xml) =>
        XDocument.Parse(xml).Root!
            .Elements("Currency")
            .Select(e => (Kod: (string?)e.Attribute("CurrencyCode") ?? "", Eleman: e))
            .Where(x => x.Kod != ParaBirimleri.Tl && ParaBirimleri.GecerliMi(x.Kod))
            .Select(x =>
            {
                var birim = Sayi(x.Eleman, "Unit") ?? 1m;
                return new TcmbKuru(
                    x.Kod,
                    Sayi(x.Eleman, "ForexBuying") / birim,
                    Sayi(x.Eleman, "ForexSelling") / birim,
                    Sayi(x.Eleman, "BanknoteBuying") / birim,
                    Sayi(x.Eleman, "BanknoteSelling") / birim);
            })
            .ToList();

    private static decimal? Sayi(XElement ust, string ad) =>
        decimal.TryParse((string?)ust.Element(ad), NumberStyles.Number, CultureInfo.InvariantCulture, out var deger) && deger > 0
            ? Math.Round(deger, 6)
            : null;
}
