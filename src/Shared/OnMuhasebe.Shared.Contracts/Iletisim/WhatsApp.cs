namespace OnMuhasebe.Shared.Contracts.Iletisim;

// WhatsApp'ın resmi "tıkla-sohbet et" bağlantısı (wa.me). Mesaj kullanıcının kendi WhatsApp'ında hazır açılır, gönderen kendisidir;
// API ya da resmi olmayan kütüphane kullanılmaz.
public static class WhatsApp
{
    // wa.me numarayı ülke koduyla, yalnız rakam ister. Türkiye biçimleri (0532..., 532..., +90 532...) 90 ile tamamlanır;
    // + ile yazılmış yabancı numara olduğu gibi alınır. Tanınmayan numarada null: bağlantı kişi seçtirerek açılır.
    public static string? Numara(string? telefon)
    {
        if (string.IsNullOrWhiteSpace(telefon))
        {
            return null;
        }

        var rakamlar = new string(telefon.Where(char.IsAsciiDigit).ToArray());
        if (rakamlar.StartsWith("00", StringComparison.Ordinal))
        {
            rakamlar = rakamlar[2..];
        }

        return rakamlar.Length switch
        {
            10 when rakamlar[0] != '0' => "90" + rakamlar,
            11 when rakamlar[0] == '0' => "9" + rakamlar,
            12 when rakamlar.StartsWith("90", StringComparison.Ordinal) => rakamlar,
            >= 8 and <= 15 when telefon.TrimStart().StartsWith('+') || telefon.TrimStart().StartsWith("00", StringComparison.Ordinal) => rakamlar,
            _ => null
        };
    }

    public static string Baglanti(string? telefon, string metin) =>
        $"https://wa.me/{Numara(telefon)}?text={Uri.EscapeDataString(metin)}";
}
