using System.Globalization;
using System.Text;

namespace OnMuhasebe.Modules.KasaBanka.Domain.Ekstre;

// Ekstrenin çözülmüş hali. Tutar işaretlidir: artı hesaba giriş, eksi çıkış.
internal sealed record CozulmusSatir(DateOnly Tarih, string Aciklama, decimal Tutar, decimal? Bakiye, string? Referans);

internal sealed record Mt940Ekstresi(IReadOnlyList<CozulmusSatir> Satirlar, decimal? AcilisBakiyesi, decimal? KapanisBakiyesi);

internal static class CsvOkuyucu
{
    private static readonly char[] _ayraclar = [';', ',', '\t', '|'];

    // Kodlama: BOM'lu ya da geçerli UTF-8 ise UTF-8, değilse Türk bankalarının kullandığı Windows-1254.
    public static string Metin(byte[] icerik)
    {
        if (icerik.Length >= 3 && icerik[0] == 0xEF && icerik[1] == 0xBB && icerik[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(icerik, 3, icerik.Length - 3);
        }

        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(icerik);
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1254).GetString(icerik);
        }
    }

    public static List<string[]> Oku(byte[] icerik)
    {
        var satirlar = Metin(icerik).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
        var ayrac = AyracBul(satirlar.Take(20).ToList());
        return satirlar.Select(s => Bol(s, ayrac)).ToList();
    }

    // Ayraç: ilk satırlarda en çok ve en tutarlı geçen karakter (tırnak içindekiler sayılmaz).
    private static char AyracBul(List<string> ornek) =>
        _ayraclar
            .Select(a => (Ayrac: a, Sayilar: ornek.Select(s => Bol(s, a).Length).ToList()))
            .Where(x => x.Sayilar.Count > 0 && x.Sayilar.Max() > 1)
            .OrderByDescending(x => x.Sayilar.GroupBy(n => n).Max(g => g.Count()))
            .ThenByDescending(x => x.Sayilar.Max())
            .Select(x => x.Ayrac)
            .DefaultIfEmpty(';')
            .First();

    private static string[] Bol(string satir, char ayrac)
    {
        var alanlar = new List<string>();
        var alan = new StringBuilder();
        var tirnakta = false;
        for (var i = 0; i < satir.Length; i++)
        {
            var c = satir[i];
            if (c == '"')
            {
                if (tirnakta && i + 1 < satir.Length && satir[i + 1] == '"')
                {
                    alan.Append('"');
                    i++;
                }
                else
                {
                    tirnakta = !tirnakta;
                }
            }
            else if (c == ayrac && !tirnakta)
            {
                alanlar.Add(alan.ToString().Trim());
                alan.Clear();
            }
            else
            {
                alan.Append(c);
            }
        }

        alanlar.Add(alan.ToString().Trim());
        return alanlar.ToArray();
    }
}

// SWIFT MT940: :60F: açılış, :61: hareket, :86: açıklama, :62F: kapanış bakiyesi. Bir dosyada birden çok ekstre bloğu olabilir.
internal static partial class Mt940Okuyucu
{
    public static bool Mt940Mu(byte[] icerik)
    {
        var bas = CsvOkuyucu.Metin(icerik.Length > 4096 ? icerik[..4096] : icerik);
        return bas.Contains(":20:", StringComparison.Ordinal) && bas.Contains(":61:", StringComparison.Ordinal);
    }

    public static Mt940Ekstresi Oku(byte[] icerik)
    {
        var etiketler = Etiketler(CsvOkuyucu.Metin(icerik));
        var satirlar = new List<CozulmusSatir>();
        decimal? acilis = null, kapanis = null;
        for (var i = 0; i < etiketler.Count; i++)
        {
            var (etiket, deger) = etiketler[i];
            switch (etiket)
            {
                case "60F" or "60M":
                    acilis ??= Bakiye(deger);
                    break;
                case "62F" or "62M":
                    kapanis = Bakiye(deger);
                    break;
                case "61":
                    var aciklama = i + 1 < etiketler.Count && etiketler[i + 1].Etiket == "86" ? Temizle(etiketler[i + 1].Deger) : "";
                    if (Hareket(deger, aciklama) is { } hareket)
                    {
                        satirlar.Add(hareket);
                    }

                    break;
            }
        }

        return new Mt940Ekstresi(satirlar, acilis, kapanis);
    }

    private static List<(string Etiket, string Deger)> Etiketler(string metin)
    {
        var sonuc = new List<(string, string)>();
        string? etiket = null;
        var deger = new StringBuilder();
        foreach (var ham in metin.Replace("\r\n", "\n").Split('\n'))
        {
            var satir = ham.TrimEnd();
            if (satir.Length > 3 && satir[0] == ':' && satir.IndexOf(':', 1) is var son and > 1 and <= 4)
            {
                if (etiket is not null)
                {
                    sonuc.Add((etiket, deger.ToString()));
                }

                etiket = satir[1..son];
                deger.Clear().Append(satir[(son + 1)..]);
            }
            else if (etiket is not null && satir != "-" && !satir.StartsWith('{'))
            {
                deger.Append('\n').Append(satir);
            }
        }

        if (etiket is not null)
        {
            sonuc.Add((etiket, deger.ToString()));
        }

        return sonuc;
    }

    // :61:YYMMDD[MMDD]{C|D|RC|RD}[fon kodu]tutar(virgüllü)N[işlem tipi][müşteri referansı]//[banka referansı]
    private static CozulmusSatir? Hareket(string deger, string aciklama)
    {
        var s = deger.Split('\n')[0];
        if (s.Length < 10 || !DateTime.TryParseExact(s[..6], "yyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var tarih))
        {
            return null;
        }

        var i = 6;
        if (s.Length > 10 && s[6..10].All(char.IsAsciiDigit))
        {
            i = 10;
        }

        var ters = s[i] == 'R';
        i += ters ? 1 : 0;
        var alacak = s[i] == 'C';
        i++;
        if (i < s.Length && char.IsAsciiLetter(s[i]))
        {
            i++;
        }

        var tutarBas = i;
        while (i < s.Length && (char.IsAsciiDigit(s[i]) || s[i] == ','))
        {
            i++;
        }

        var tutar = decimal.Parse(s[tutarBas..i].Replace(',', '.'), CultureInfo.InvariantCulture);
        var referans = s.Length > i + 4 ? s[(i + 4)..].Split("//")[0].Trim() : null;
        var giris = alacak != ters;
        return new CozulmusSatir(DateOnly.FromDateTime(tarih), aciklama, giris ? tutar : -tutar,
            null, string.IsNullOrWhiteSpace(referans) || referans == "NONREF" ? null : referans);
    }

    // :60F:C261020TRY12345,67
    private static decimal? Bakiye(string deger)
    {
        var s = deger.Trim();
        if (s.Length < 11)
        {
            return null;
        }

        var tutar = decimal.Parse(s[10..].Replace(',', '.'), CultureInfo.InvariantCulture);
        return s[0] == 'D' ? -tutar : tutar;
    }

    // Türk bankaları :86: alanını ?20 ?21 gibi alt alanlara böler; bunlar boşlukla birleştirilir.
    private static string Temizle(string aciklama)
    {
        var tek = aciklama.Replace('\n', ' ');
        tek = AltAlan().Replace(tek, " ");
        return string.Join(' ', tek.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\?\d{2}")]
    private static partial System.Text.RegularExpressions.Regex AltAlan();
}
