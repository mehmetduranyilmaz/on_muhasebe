using System.Globalization;
using System.Text;

namespace OnMuhasebe.Modules.KasaBanka.Domain.Ekstre;

// Bankaların farklı biçimlerdeki tarih, tutar ve açıklama metinlerini ortak biçime çevirir.
internal static class EkstreMetni
{
    private static readonly CultureInfo _tr = CultureInfo.GetCultureInfo("tr-TR");

    private static readonly string[] _tarihBicimleri =
    [
        "dd.MM.yyyy", "d.M.yyyy", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "yyyy-MM-dd", "yyyyMMdd", "dd.MM.yy",
        "dd.MM.yyyy HH:mm", "dd.MM.yyyy HH:mm:ss", "dd/MM/yyyy HH:mm", "dd/MM/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss"
    ];

    // Karşılaştırma için: büyük harf, Türkçe karakterler ASCII karşılıklarına, fazla boşluklar tek boşluğa.
    public static string Normalize(string metin)
    {
        var sb = new StringBuilder(metin.Length);
        foreach (var c in metin.ToUpper(_tr))
        {
            sb.Append(c switch
            {
                'İ' or 'I' or 'Ì' => 'I',
                'Ş' => 'S',
                'Ğ' => 'G',
                'Ü' => 'U',
                'Ö' => 'O',
                'Ç' => 'C',
                _ when char.IsWhiteSpace(c) => ' ',
                _ => c
            });
        }

        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    // "1.234,56" "1,234.56" "1234.56" "-1.234,56" "1.234,56-" "(1.234,56)" "1.234,56 TL" biçimlerini okur.
    // Yalnız nokta varsa ve noktadan sonra tam 3 rakam geliyorsa binlik ayraç sayılır ("1.250" = 1250).
    public static decimal? Tutar(string? metin)
    {
        if (string.IsNullOrWhiteSpace(metin))
        {
            return null;
        }

        var s = new string(metin.Where(c => char.IsAsciiDigit(c) || c is '.' or ',' or '-' or '(' or ')').ToArray());
        var eksi = s.StartsWith('-') || s.EndsWith('-') || (s.StartsWith('(') && s.EndsWith(')'));
        s = s.Trim('-', '(', ')');
        if (s.Length == 0 || s.Contains('-'))
        {
            return null;
        }

        var sonNokta = s.LastIndexOf('.');
        var sonVirgul = s.LastIndexOf(',');
        string tam, kesir;
        if (sonNokta >= 0 && sonVirgul >= 0)
        {
            var ayrac = Math.Max(sonNokta, sonVirgul);
            (tam, kesir) = (s[..ayrac], s[(ayrac + 1)..]);
        }
        else if (sonVirgul >= 0)
        {
            (tam, kesir) = s.Count(c => c == ',') == 1 && s.Length - sonVirgul - 1 != 3 ? (s[..sonVirgul], s[(sonVirgul + 1)..]) : (s, "");
        }
        else if (sonNokta >= 0)
        {
            (tam, kesir) = s.Count(c => c == '.') == 1 && s.Length - sonNokta - 1 != 3 ? (s[..sonNokta], s[(sonNokta + 1)..]) : (s, "");
        }
        else
        {
            (tam, kesir) = (s, "");
        }

        tam = new string(tam.Where(char.IsAsciiDigit).ToArray());
        if (tam.Length == 0 && kesir.Length == 0)
        {
            return null;
        }

        // 16 haneden uzun tam kısım hatalı veridir (decimal taşmasın, makul tutar sınırının çok üstü).
        if (tam.Length > 16)
        {
            return null;
        }

        var deger = decimal.Parse($"{(tam.Length == 0 ? "0" : tam)}.{(kesir.Length == 0 ? "0" : kesir)}", CultureInfo.InvariantCulture);
        if (deger >= EkstreSinirlari.AzamiTutar)
        {
            return null;
        }

        return eksi ? -deger : deger;
    }

    public static DateOnly? Tarih(string? metin)
    {
        if (string.IsNullOrWhiteSpace(metin))
        {
            return null;
        }

        var s = metin.Trim();
        return DateTime.TryParseExact(s, _tarihBicimleri, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var tarih)
            ? DateOnly.FromDateTime(tarih)
            : null;
    }
}
