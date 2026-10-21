using System.Text.Json.Serialization;

namespace OnMuhasebe.Modules.KasaBanka.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<VadeAraligi>))]
public enum VadeAraligi
{
    Aylik,
    IkiHaftalik,
    Haftalik
}

// Ardışık numaralı çek/senet (ör. 12 aylık taksit senedi) için vade ve tutar planı.
public static class SeriGiris
{
    public const int AzamiAdet = 60;

    // Aylıkta her vade ilk vadeden ay eklenerek bulunur (31 Ocak -> 28 Şubat -> 31 Mart; gün kaymaz).
    // Toplam bölünürken kuruş farkı son evraka eklenir; böylece evrakların toplamı girilen toplamla aynı kalır.
    public static IReadOnlyList<(DateOnly Vade, decimal Tutar)> Plan(DateOnly ilkVade, int adet, VadeAraligi aralik, decimal tutar, bool toplamiBol)
    {
        var tek = toplamiBol ? Math.Round(tutar / adet, 2, MidpointRounding.AwayFromZero) : tutar;
        return Enumerable.Range(0, adet)
            .Select(i => (
                aralik switch
                {
                    VadeAraligi.IkiHaftalik => ilkVade.AddDays(14 * i),
                    VadeAraligi.Haftalik => ilkVade.AddDays(7 * i),
                    _ => ilkVade.AddMonths(i)
                },
                toplamiBol && i == adet - 1 ? tutar - tek * (adet - 1) : tek))
            .ToList();
    }
}
