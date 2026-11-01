using System.Globalization;
using OnMuhasebe.Shared.Contracts.Birimler;
using OnMuhasebe.Shared.Contracts.Doviz;

namespace OnMuhasebe.Web;

// Ekranlarda tutar, kur ve tarihlerin Türkçe gösterimi.
public static class Bicim
{
    private static readonly CultureInfo _tr = CultureInfo.GetCultureInfo("tr-TR");

    public static CultureInfo Kultur => _tr;

    public static string Tutar(decimal tutar) => tutar.ToString("N2", _tr);

    public static string Tutar(decimal tutar, string paraBirimi) =>
        $"{Tutar(tutar)} {(paraBirimi == ParaBirimleri.Tl ? "TL" : paraBirimi)}";

    // Cari bakiyesi: pozitif borçlu (B), negatif alacaklı (A).
    public static string Bakiye(decimal bakiye, string paraBirimi) => bakiye switch
    {
        > 0 => $"{Tutar(bakiye, paraBirimi)} (B)",
        < 0 => $"{Tutar(-bakiye, paraBirimi)} (A)",
        _ => Tutar(0m, paraBirimi)
    };

    public static string Kur(decimal kur) => kur.ToString("0.######", _tr);

    // Miktar en fazla 4 ondalıkla, birim adıyla: "1.234,5 Kilogram".
    public static string Miktar(decimal miktar) => miktar.ToString("#,0.####", _tr);

    public static string Miktar(decimal miktar, string birim) => $"{Miktar(miktar)} {Birimler.Ad(birim)}";

    // 1024 tabanlı dosya boyutu: 512 KB, 1,5 MB, 2 GB.
    public static string Boyut(long bayt)
    {
        const double Kb = 1024, Mb = Kb * 1024, Gb = Mb * 1024;
        return bayt switch
        {
            < 1024 => $"{bayt} B",
            < 1024 * 1024 => $"{(bayt / Kb).ToString("0.#", _tr)} KB",
            < 1024 * 1024 * 1024 => $"{(bayt / Mb).ToString("0.#", _tr)} MB",
            _ => $"{(bayt / Gb).ToString("0.#", _tr)} GB"
        };
    }

    public static string Tarih(DateOnly tarih) => tarih.ToString("dd.MM.yyyy", _tr);

    public static string Tarih(DateOnly? tarih) => tarih is { } t ? Tarih(t) : "";

    // API sorgu parametreleri için.
    public static string Iso(DateOnly tarih) => tarih.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static DateOnly? Gun(DateTime? tarih) => tarih is { } t ? DateOnly.FromDateTime(t) : null;

    public static DateTime? Takvim(DateOnly? tarih) => tarih?.ToDateTime(TimeOnly.MinValue);

    // "TR330006..." -> "TR33 0006 ..." (okunur, kopyalanınca boşluksuz değer kullanılır).
    public static string Iban(string iban) => string.Join(' ', iban.Chunk(4).Select(c => new string(c)));
}
