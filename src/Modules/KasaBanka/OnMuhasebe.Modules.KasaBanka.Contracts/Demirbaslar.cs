using System.Text.Json.Serialization;

namespace OnMuhasebe.Modules.KasaBanka.Contracts;

// Amortismana tabi iktisadi kıymet grupları; önerilen ömürler 333 Sıra No'lu VUK Genel Tebliği listesindeki yaygın değerlerdir
// (kıymetin listedeki tam karşılığı mali müşavirle teyit edilmeli, ömür kartta değiştirilebilir).
[JsonConverter(typeof(JsonStringEnumConverter<DemirbasGrubu>))]
public enum DemirbasGrubu
{
    Binalar,
    TesisMakineCihaz,
    Tasitlar,
    Demirbaslar,
    Bilgisayar,
    OzelMaliyetler,
    Haklar,
    Diger
}

[JsonConverter(typeof(JsonStringEnumConverter<AmortismanYontemi>))]
public enum AmortismanYontemi
{
    // Eşit paylar (VUK 315).
    Normal,
    // Azalan bakiyeler (VUK 316): normal oranın iki katı, en çok %50.
    AzalanBakiyeler
}

[JsonConverter(typeof(JsonStringEnumConverter<ElCikarmaTuru>))]
public enum ElCikarmaTuru
{
    Satis,
    Hurda
}

// Edinme bedeli KDV hariç TL'dir (indirilemeyen KDV ve edinmeye bağlı giderler bedele eklenir).
public sealed record DemirbasKaydetIstegi(
    string? SicilNo,
    string Ad,
    DemirbasGrubu Grup,
    bool BinekOtomobilMi,
    DateOnly EdinmeTarihi,
    decimal EdinmeBedeli,
    int FaydaliOmur,
    AmortismanYontemi Yontem,
    string? BelgeNo,
    string? Zimmetli,
    string? Konum,
    Guid? MasrafMerkeziId,
    string? Aciklama,
    uint Surum = 0);

public sealed record ElCikarmaIstegi(ElCikarmaTuru Tur, DateOnly Tarih, decimal Bedel, string? Aciklama);

public sealed record AmortismanYili(int Yil, decimal Oran, decimal Tutar, decimal Birikmis, decimal NetDeger);

public sealed record DemirbasOzeti(
    Guid Id,
    string SicilNo,
    string Ad,
    DemirbasGrubu Grup,
    DateOnly EdinmeTarihi,
    decimal EdinmeBedeli,
    int FaydaliOmur,
    AmortismanYontemi Yontem,
    string? Zimmetli,
    DateOnly? ElCikarmaTarihi,
    decimal YilAmortismani,
    decimal Birikmis,
    decimal NetDeger);

public sealed record DemirbasDetayi(
    Guid Id,
    string SicilNo,
    string Ad,
    DemirbasGrubu Grup,
    bool BinekOtomobilMi,
    DateOnly EdinmeTarihi,
    decimal EdinmeBedeli,
    int FaydaliOmur,
    AmortismanYontemi Yontem,
    string? BelgeNo,
    string? Zimmetli,
    string? Konum,
    Guid? MasrafMerkeziId,
    string? Aciklama,
    ElCikarmaTuru? ElCikarmaTuru,
    DateOnly? ElCikarmaTarihi,
    decimal? ElCikarmaBedeli,
    string? ElCikarmaAciklamasi,
    decimal? ElCikarmaKarZarari,
    IReadOnlyList<AmortismanYili> Plan,
    uint Surum);

// Seçilen yılın amortisman listesi (yıl sonu envanter ve beyanname eki).
public sealed record AmortismanListesi(int Yil, IReadOnlyList<DemirbasOzeti> Kalemler, decimal ToplamBedel, decimal ToplamYilAmortismani, decimal ToplamBirikmis, decimal ToplamNetDeger);

public static class AmortismanHesaplayici
{
    public const decimal AzalanBakiyelerAzamiOran = 0.5m;

    public static int OnerilenOmur(DemirbasGrubu grup) => grup switch
    {
        DemirbasGrubu.Binalar => 50,
        DemirbasGrubu.TesisMakineCihaz => 10,
        DemirbasGrubu.Tasitlar => 5,
        DemirbasGrubu.Demirbaslar => 5,
        DemirbasGrubu.Bilgisayar => 4,
        DemirbasGrubu.OzelMaliyetler => 5,
        DemirbasGrubu.Haklar => 15,
        _ => 5
    };

    public static decimal Oran(int omur, AmortismanYontemi yontem) =>
        yontem == AmortismanYontemi.Normal ? 1m / omur : Math.Min(2m / omur, AzalanBakiyelerAzamiOran);

    // Yıllık plan. Edinme yılında tam yıl amortisman ayrılır; binek otomobilde edinme yılı ay kıstıyla (edinme ayı dahil),
    // ayrılamayan kısım ömrü izleyen yıla kalır. Azalan bakiyede son yılda kalan değerin tamamı ayrılır.
    // Elden çıkarılan yıl ve sonrası için amortisman ayrılmaz.
    public static IReadOnlyList<AmortismanYili> Plan(
        decimal bedel, DateOnly edinme, int omur, AmortismanYontemi yontem, bool binekMi, DateOnly? elCikarma = null)
    {
        var plan = new List<AmortismanYili>();
        if (bedel <= 0 || omur <= 0)
        {
            return plan;
        }

        var oran = Oran(omur, yontem);
        var birikmis = 0m;
        var sonYil = edinme.Year + omur - 1 + (binekMi && edinme.Month > 1 ? 1 : 0);
        for (var yil = edinme.Year; yil <= sonYil; yil++)
        {
            if (elCikarma is { } c && yil >= c.Year)
            {
                break;
            }

            var kalan = bedel - birikmis;
            decimal tutar;
            if (yil == sonYil)
            {
                tutar = kalan;
            }
            else
            {
                var yillik = yontem == AmortismanYontemi.Normal ? bedel * oran : kalan * oran;
                if (binekMi && yil == edinme.Year)
                {
                    var kist = yillik * (13 - edinme.Month) / 12m;
                    yillik = kist;
                }

                tutar = Math.Min(Yuvarla(yillik), kalan);
            }

            birikmis += tutar;
            plan.Add(new AmortismanYili(yil, Math.Round(oran, 4), tutar, birikmis, bedel - birikmis));
        }

        return plan;
    }

    // Elden çıkarma kârı (+) / zararı (-): bedel − (edinme bedeli − birikmiş amortisman).
    public static decimal ElCikarmaSonucu(decimal satisBedeli, decimal edinmeBedeli, decimal birikmis) => Yuvarla(satisBedeli - (edinmeBedeli - birikmis));

    public static (decimal YilTutari, decimal Birikmis) Yil(IReadOnlyList<AmortismanYili> plan, int yil) =>
        (plan.FirstOrDefault(p => p.Yil == yil)?.Tutar ?? 0m, plan.Where(p => p.Yil <= yil).Sum(p => p.Tutar));

    private static decimal Yuvarla(decimal tutar) => Math.Round(tutar, 2, MidpointRounding.AwayFromZero);
}
