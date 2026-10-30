using System.Globalization;

namespace OnMuhasebe.Shared.Contracts.Bankalar;

// Banka: TCMB/EFT kodu, görünen ad ve SWIFT (BIC). Kod ve SWIFT bilgi amaçlı taşınır; seçimde saklanan değer bankanın adıdır.
public sealed record Banka(string Kod, string Ad, string? Swift);

// Türkiye'de faaliyetteki mevduat ve katılım bankaları. Kaynak: TCMB Ödeme Sistemleri Katılımcıları listesi (EFT kodları) ve
// bankaların SWIFT/BIC kodları. Kapanan/birleşen bankalar listede yoktur. Liste bir öneri listesidir: seçimlerde listede
// olmayan banka serbestçe yazılabilir, bu yüzden salt mevduat/katılım bankaları tutulur (altyapı ve yatırım bankaları hariç).
public static class Bankalar
{
    public static IReadOnlyList<Banka> Tumu { get; } =
    [
        // Kamu mevduat bankaları
        new("0010", "T.C. Ziraat Bankası", "TCZBTR2A"),
        new("0012", "Türkiye Halk Bankası", "TRHBTR2A"),
        new("0015", "Türkiye Vakıflar Bankası (VakıfBank)", "TVBATR2A"),
        // Özel/yabancı mevduat bankaları
        new("0046", "Akbank", "AKBKTRIS"),
        new("0062", "Türkiye Garanti Bankası (Garanti BBVA)", "TGBATRIS"),
        new("0064", "Türkiye İş Bankası", "ISBKTRIS"),
        new("0067", "Yapı ve Kredi Bankası", "YAPITRIS"),
        new("0111", "QNB (QNB Finansbank)", "FNNBTRIS"),
        new("0134", "DenizBank", "DENITRIS"),
        new("0032", "Türk Ekonomi Bankası (TEB)", "TEBUTRIS"),
        new("0099", "ING Bank", "INGBTRIS"),
        new("0059", "Şekerbank", "SEKETR2A"),
        new("0103", "Fibabanka", "FBHLTRIS"),
        new("0135", "Anadolubank", "ANDLTRIS"),
        new("0146", "Odea Bank", "ODEATRIS"),
        new("0124", "Alternatif Bank (ABank)", "ALFBTRIS"),
        new("0125", "Burgan Bank", "TEKFTRIS"),
        new("0123", "HSBC Bank", "HSBCTRIX"),
        new("0109", "ICBC Turkey Bank", "ICBKTRIS"),
        new("0108", "Turkland Bank (T-Bank)", "TBNKTRIS"),
        new("0092", "Citibank", "CITITRIX"),
        new("0091", "Arap Türk Bankası (A&T Bank)", "ATUBTRIS"),
        new("0096", "Freedom Bank", "TUBATRIS"),
        new("0060", "Türk Ticaret Bankası", null),
        new("0157", "Enpara Bank", "ENASTRIS"),
        new("0158", "Colendi Bank", "CLNDTRIS"),
        // Katılım bankaları
        new("0209", "Ziraat Katılım Bankası", "ZKBATRIS"),
        new("0210", "Vakıf Katılım Bankası", "VAKFTRIS"),
        new("0205", "Kuveyt Türk Katılım Bankası", "KTEFTRIS"),
        new("0206", "Türkiye Finans Katılım Bankası", "AFKBTRIS"),
        new("0203", "Albaraka Türk Katılım Bankası", "BTFHTRIS"),
        new("0211", "Türkiye Emlak Katılım Bankası", "EMLATRIS"),
        new("0212", "Hayat Finans Katılım Bankası", "HTYBTRIS"),
        new("0214", "Dünya Katılım Bankası", "ADABTRIS"),
        new("0213", "T.O.M. Katılım Bankası", "TKNSTRIS"),
        new("0216", "İktisat Katılım Bankası", "IKKBTRIS"),
        new("0215", "Adil Katılım Bankası", "AKBMTRIS")
    ];

    private static readonly CultureInfo _tr = CultureInfo.GetCultureInfo("tr-TR");

    public static Banka? Bul(string? ad) =>
        string.IsNullOrWhiteSpace(ad) ? null : Tumu.FirstOrDefault(b => string.Equals(b.Ad, ad.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string? Swift(string? ad) => Bul(ad)?.Swift;

    // Ada veya SWIFT'e göre arama (Türkçe büyük/küçük harf duyarsız). Boş aramada tüm liste döner.
    public static IReadOnlyList<Banka> Ara(string? arama)
    {
        if (string.IsNullOrWhiteSpace(arama))
        {
            return Tumu;
        }

        var desen = arama.Trim().ToLower(_tr);
        return Tumu.Where(b => b.Ad.ToLower(_tr).Contains(desen, StringComparison.Ordinal)
            || (b.Swift is not null && b.Swift.Contains(desen, StringComparison.OrdinalIgnoreCase))).ToList();
    }
}
