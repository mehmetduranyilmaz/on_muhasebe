namespace OnMuhasebe.Shared.Domain.Dogrulama;

// ISO 13616 IBAN kontrolü (mod 97). Türk IBAN'ı TR + 24 hanedir.
public static class Iban
{
    public static string Normalize(string iban) => new string(iban.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

    public static bool GecerliMi(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
        {
            return false;
        }

        var deger = Normalize(iban);
        if (deger.Length is < 15 or > 34 || !deger.All(char.IsAsciiLetterOrDigit) || !char.IsAsciiLetter(deger[0]) || !char.IsAsciiLetter(deger[1]))
        {
            return false;
        }

        if (deger.StartsWith("TR", StringComparison.Ordinal) && (deger.Length != 26 || !deger[2..].All(char.IsAsciiDigit)))
        {
            return false;
        }

        // İlk dört karakter sona alınır, harfler sayıya çevrilir (A=10 ... Z=35), kalan 97'ye bölümünden 1 olmalıdır.
        var kalan = 0;
        foreach (var c in deger[4..] + deger[..4])
        {
            var sayi = char.IsAsciiDigit(c) ? c - '0' : c - 'A' + 10;
            kalan = sayi >= 10 ? (kalan * 100 + sayi) % 97 : (kalan * 10 + sayi) % 97;
        }

        return kalan == 1;
    }
}
