using System.Text.RegularExpressions;
using OnMuhasebe.Shared.Domain.Dogrulama;

namespace OnMuhasebe.Modules.KasaBanka.Domain.CekSenet;

internal sealed record KarekodCozumu(string? SeriNo, string? BankaKodu, string? SubeKodu, string? HesapNo, string? Iban, string? KimlikNo);

// Çek karekodu (Data Matrix, ISO/IEC 16022). Tebliğ içeriği tanımlar ama alanların dizilişi kamuya açık değildir; bu yüzden
// ayrıştırma esnektir: önce "anahtar=değer" ya da adres parametreleri, sonra IBAN, kontrol hanesi tutan VKN/TCKN, bilinen banka
// kodu ve onu izleyen şube/hesap numarası, son olarak çek seri numarası biçimi aranır. Emin olunamayan alan boş kalır.
internal static partial class CekKarekodu
{
    public static KarekodCozumu Coz(string ham)
    {
        var metin = ham.Trim();
        var anahtarlar = AnahtarDegerleri(metin);
        string? Anahtar(params string[] adlar) =>
            anahtarlar.FirstOrDefault(a => adlar.Any(ad => a.Anahtar.Contains(ad, StringComparison.Ordinal))).Deger;

        var parcalar = Parca().Matches(metin).Select(m => m.Value.ToUpperInvariant()).ToList();
        var kullanilan = new HashSet<string>();

        var iban = Anahtar("IBAN") is { } ai && Iban.GecerliMi(ai) ? Iban.Normalize(ai) : IbanAra(metin);
        var kimlik = Anahtar("VKN", "TCKN", "KIMLIK", "VERGI") is { } ak && VergiKimlik.GecerliMi(ak)
            ? ak
            : parcalar.FirstOrDefault(p => p.All(char.IsAsciiDigit) && VergiKimlik.GecerliMi(p));
        Kullan(kimlik);

        var bankaKodu = Anahtar("BANKA") is { } ab ? BankaKodu(ab) : iban is not null ? BankaKodu(iban[4..9]) : null;
        var bankaSirasi = -1;
        if (bankaKodu is null)
        {
            bankaSirasi = parcalar.FindIndex(p => p.Length is 3 or 4 or 5 && p.All(char.IsAsciiDigit) && BankaKodlari.Ad(BankaKodu(p)) is not null);
            bankaKodu = bankaSirasi >= 0 ? BankaKodu(parcalar[bankaSirasi]) : null;
            Kullan(bankaSirasi >= 0 ? parcalar[bankaSirasi] : null);
        }

        var sube = Anahtar("SUBE");
        if (sube is null && bankaSirasi >= 0 && Sonraki(bankaSirasi, p => p.Length is 3 or 4 or 5) is { } s)
        {
            sube = s;
            Kullan(s);
        }

        var hesap = Anahtar("HESAP");
        if (hesap is null && bankaSirasi >= 0 && Sonraki(bankaSirasi + 1, p => p.Length is >= 5 and <= 16) is { } h)
        {
            hesap = h;
            Kullan(h);
        }

        hesap ??= iban;
        var seri = Anahtar("SERI", "CEKNO", "CEK_NO")
                   ?? parcalar.FirstOrDefault(p => !kullanilan.Contains(p) && p.Length == 7 && p.All(char.IsAsciiDigit))
                   ?? parcalar.FirstOrDefault(p => !kullanilan.Contains(p) && HarfliSeri().IsMatch(p));

        return new KarekodCozumu(seri, bankaKodu, sube, hesap, iban, kimlik);

        void Kullan(string? parca)
        {
            if (parca is not null)
            {
                kullanilan.Add(parca);
            }
        }

        string? Sonraki(int sira, Func<string, bool> kosul) =>
            sira + 1 < parcalar.Count && parcalar[sira + 1].All(char.IsAsciiDigit) && !kullanilan.Contains(parcalar[sira + 1]) && kosul(parcalar[sira + 1])
                ? parcalar[sira + 1]
                : null;
    }

    // Banka kodu dört hanedir (EFT kodu); IBAN'da başına sıfır eklenmiş beş hane olarak geçer.
    private static string BankaKodu(string kod) => kod.TrimStart('0').PadLeft(4, '0');

    private static string? IbanAra(string metin) =>
        IbanDeseni().Matches(Iban.Normalize(metin)).Select(m => m.Value).FirstOrDefault(Iban.GecerliMi);

    // "seri=1234567", "VKN: 1234567890", adres parametresi "?seri=...&vkn=..." biçimleri.
    private static List<(string Anahtar, string Deger)> AnahtarDegerleri(string metin) =>
        AnahtarDeger().Matches(metin)
            .Select(m => (Turkcesiz(m.Groups["anahtar"].Value), m.Groups["deger"].Value.Trim().ToUpperInvariant()))
            .ToList();

    private static string Turkcesiz(string s) => s.ToUpperInvariant()
        .Replace('İ', 'I').Replace('Ş', 'S').Replace('Ç', 'C').Replace('Ğ', 'G').Replace('Ü', 'U').Replace('Ö', 'O').Replace("-", "_", StringComparison.Ordinal);

    [GeneratedRegex(@"[A-Za-z0-9]+")]
    private static partial Regex Parca();

    [GeneratedRegex(@"TR\d{24}")]
    private static partial Regex IbanDeseni();

    [GeneratedRegex(@"^[A-Z]{1,3}\d{5,8}$")]
    private static partial Regex HarfliSeri();

    [GeneratedRegex(@"(?<anahtar>[\p{L}_\-]{3,20})\s*[:=]\s*(?<deger>[A-Za-z0-9 ]{2,40}?)(?=[;&|,\r\n]|$)")]
    private static partial Regex AnahtarDeger();
}

// Türkiye'de faaliyet gösteren bankaların EFT kodları (çek ve IBAN'daki banka kodu).
internal static class BankaKodlari
{
    private static readonly Dictionary<string, string> _adlar = new()
    {
        ["0010"] = "T.C. Ziraat Bankası",
        ["0012"] = "Türkiye Halk Bankası",
        ["0015"] = "Türkiye Vakıflar Bankası",
        ["0032"] = "Türk Ekonomi Bankası",
        ["0046"] = "Akbank",
        ["0059"] = "Şekerbank",
        ["0062"] = "Türkiye Garanti Bankası",
        ["0064"] = "Türkiye İş Bankası",
        ["0067"] = "Yapı ve Kredi Bankası",
        ["0099"] = "ING Bank",
        ["0103"] = "Fibabanka",
        ["0108"] = "Turkland Bank",
        ["0109"] = "ICBC Turkey Bank",
        ["0111"] = "QNB Bank",
        ["0123"] = "HSBC Bank",
        ["0124"] = "Alternatifbank",
        ["0125"] = "Burgan Bank",
        ["0134"] = "Denizbank",
        ["0135"] = "Anadolubank",
        ["0143"] = "Aktif Yatırım Bankası",
        ["0146"] = "Odea Bank",
        ["0203"] = "Albaraka Türk Katılım Bankası",
        ["0205"] = "Kuveyt Türk Katılım Bankası",
        ["0206"] = "Türkiye Finans Katılım Bankası",
        ["0209"] = "Ziraat Katılım Bankası",
        ["0210"] = "Vakıf Katılım Bankası",
        ["0211"] = "Türkiye Emlak Katılım Bankası"
    };

    public static string? Ad(string? kod) => kod is not null && _adlar.TryGetValue(kod, out var ad) ? ad : null;
}
