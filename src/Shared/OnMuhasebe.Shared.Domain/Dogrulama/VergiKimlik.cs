namespace OnMuhasebe.Shared.Domain.Dogrulama;

// Gelir İdaresi'nin vergi kimlik numarası (10 hane) ve nüfus idaresinin T.C. kimlik numarası (11 hane) kontrol hanesi algoritmaları.
public static class VergiKimlik
{
    public static bool GecerliMi(string? numara) => numara?.Length switch
    {
        10 => VknGecerliMi(numara),
        11 => TcknGecerliMi(numara),
        _ => false
    };

    public static bool VknGecerliMi(string? vkn)
    {
        if (vkn is not { Length: 10 } || !vkn.All(char.IsAsciiDigit))
        {
            return false;
        }

        var toplam = 0;
        for (var i = 0; i < 9; i++)
        {
            var t = (vkn[i] - '0' + 9 - i) % 10;
            var p = t * (1 << (9 - i)) % 9;
            if (t != 0 && p == 0)
            {
                p = 9;
            }

            toplam += p;
        }

        return (10 - toplam % 10) % 10 == vkn[9] - '0';
    }

    public static bool TcknGecerliMi(string? tckn)
    {
        if (tckn is not { Length: 11 } || !tckn.All(char.IsAsciiDigit) || tckn[0] == '0')
        {
            return false;
        }

        var h = tckn.Select(c => c - '0').ToArray();
        var tekler = h[0] + h[2] + h[4] + h[6] + h[8];
        var ciftler = h[1] + h[3] + h[5] + h[7];
        var onuncu = ((tekler * 7 - ciftler) % 10 + 10) % 10;

        return h[9] == onuncu && h[10] == h.Take(10).Sum() % 10;
    }
}
