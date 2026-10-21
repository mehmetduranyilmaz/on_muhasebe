using System.Globalization;

namespace OnMuhasebe.Modules.KasaBanka.Contracts;

// Çek/senet seri no: isteğe bağlı harf öneki + sayı ("ABC 1234567", "A-0045", "0001"). Karne ve sıralı giriş ardışık numarayı
// öneki ve hane sayısını koruyarak üretir. Karşılaştırmada boşluk ve harf büyüklüğü dikkate alınmaz.
public static class EvrakSeriNo
{
    public const int AzamiHane = 18;

    public static string Normalize(string seri) => new string(seri.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

    public static (string Onek, long Sayi, int Hane)? Ayir(string? seri)
    {
        if (string.IsNullOrWhiteSpace(seri))
        {
            return null;
        }

        var s = Normalize(seri);
        var i = s.Length;
        while (i > 0 && char.IsAsciiDigit(s[i - 1]))
        {
            i--;
        }

        var rakam = s[i..];
        return rakam.Length is 0 or > AzamiHane ? null : (s[..i], long.Parse(rakam, CultureInfo.InvariantCulture), rakam.Length);
    }

    public static string Olustur(string onek, long sayi, int hane) => onek + sayi.ToString(CultureInfo.InvariantCulture).PadLeft(hane, '0');

    // Başlangıçtan itibaren ardışık adet seri; sayı kısmı olmayan seride null.
    public static IReadOnlyList<string>? Ardisik(string baslangic, int adet) =>
        Ayir(baslangic) is { } a ? Enumerable.Range(0, adet).Select(i => Olustur(a.Onek, a.Sayi + i, a.Hane)).ToList() : null;
}
