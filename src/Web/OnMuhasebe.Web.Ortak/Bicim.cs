using System.Globalization;
using OnMuhasebe.Shared.Contracts.Doviz;

namespace OnMuhasebe.Web;

// Ekranlarda tutar, kur ve tarihlerin Türkçe gösterimi.
public static class Bicim
{
    private static readonly CultureInfo _tr = CultureInfo.GetCultureInfo("tr-TR");

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

    public static string Tarih(DateOnly tarih) => tarih.ToString("dd.MM.yyyy", _tr);

    public static string Tarih(DateOnly? tarih) => tarih is { } t ? Tarih(t) : "";

    // API sorgu parametreleri için.
    public static string Iso(DateOnly tarih) => tarih.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static DateOnly? Gun(DateTime? tarih) => tarih is { } t ? DateOnly.FromDateTime(t) : null;

    public static DateTime? Takvim(DateOnly? tarih) => tarih?.ToDateTime(TimeOnly.MinValue);
}
