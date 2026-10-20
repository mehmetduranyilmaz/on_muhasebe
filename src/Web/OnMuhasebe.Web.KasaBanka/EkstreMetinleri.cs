using System.Globalization;
using MudBlazor;
using OnMuhasebe.Modules.KasaBanka.Contracts;

namespace OnMuhasebe.Web.KasaBanka;

public static class EkstreMetinleri
{
    public const string KabulEdilenDosyalar = ".csv,.txt,.xlsx,.xls,.sta,.mt940,.940";
    public const long AzamiDosyaBoyutu = 5 * 1024 * 1024;

    private static readonly string[] _genelKelimeler =
        ["EFT", "HAVALE", "GELEN", "GIDEN", "GİDEN", "ODEME", "ÖDEME", "FAST", "VIRMAN", "VİRMAN", "TRANSFER", "HESAPTAN", "HESABA", "ISLEM", "İŞLEM"];

    public static string IslemTuru(EkstreIslemTuru tur) => tur switch
    {
        EkstreIslemTuru.Tahsilat => "Tahsilat",
        EkstreIslemTuru.Odeme => "Ödeme",
        EkstreIslemTuru.Gelir => "Gelir",
        EkstreIslemTuru.Gider => "Gider",
        EkstreIslemTuru.Virman => "Virman",
        _ => "Atla"
    };

    public static string IslemIkonu(EkstreIslemTuru tur) => tur switch
    {
        EkstreIslemTuru.Tahsilat => Icons.Material.Filled.CallReceived,
        EkstreIslemTuru.Odeme => Icons.Material.Filled.CallMade,
        EkstreIslemTuru.Gelir => Icons.Material.Filled.TrendingUp,
        EkstreIslemTuru.Gider => Icons.Material.Filled.TrendingDown,
        EkstreIslemTuru.Virman => Icons.Material.Filled.SwapHoriz,
        _ => Icons.Material.Filled.DoNotDisturbOn
    };

    public static string IslemRengi(EkstreIslemTuru tur) => tur switch
    {
        EkstreIslemTuru.Tahsilat or EkstreIslemTuru.Gelir => "#16A34A",
        EkstreIslemTuru.Odeme or EkstreIslemTuru.Gider => "#DC2626",
        EkstreIslemTuru.Virman => "#3B82F6",
        _ => "#64748B"
    };

    // Giriş satırı tahsilat/gelir/virman, çıkış satırı ödeme/gider/virman olabilir; API de aynı kuralı uygular.
    public static IReadOnlyList<EkstreIslemTuru> UygunTurler(bool? girisMi) => girisMi switch
    {
        true => [EkstreIslemTuru.Tahsilat, EkstreIslemTuru.Gelir, EkstreIslemTuru.Virman, EkstreIslemTuru.Atla],
        false => [EkstreIslemTuru.Odeme, EkstreIslemTuru.Gider, EkstreIslemTuru.Virman, EkstreIslemTuru.Atla],
        null => [EkstreIslemTuru.Virman, EkstreIslemTuru.Atla]
    };

    public static bool CariGerekliMi(EkstreIslemTuru tur) => tur is EkstreIslemTuru.Tahsilat or EkstreIslemTuru.Odeme;

    public static bool KategoriGerekliMi(EkstreIslemTuru tur) => tur is EkstreIslemTuru.Gelir or EkstreIslemTuru.Gider;

    // Aktarımda hata verecek eksik seçim; null ise satır hazırdır.
    public static string? Eksik(EkstreIslemTuru tur, Guid? cari, Guid? kategori, Guid? karsiHesap) => tur switch
    {
        _ when CariGerekliMi(tur) && cari is null => "Cari seçilmedi",
        _ when KategoriGerekliMi(tur) && kategori is null => "Kategori seçilmedi",
        EkstreIslemTuru.Virman when karsiHesap is null => "Karşı hesap seçilmedi",
        _ => null
    };

    public static string? Eksik(EkstreSatiriDetayi s) =>
        s.Durum is EkstreSatirDurumu.Bekliyor or EkstreSatirDurumu.Hatali ? Eksik(s.IslemTuru, s.CariHesapId, s.KategoriId, s.KarsiHesapId) : null;

    public static bool DuzenlenebilirMi(EkstreSatiriDetayi s) =>
        s.Durum is EkstreSatirDurumu.Bekliyor or EkstreSatirDurumu.Hatali or EkstreSatirDurumu.ZatenKayitli;

    public static (string Metin, Color Renk, string Ikon) Durum(EkstreSatirDurumu durum) => durum switch
    {
        EkstreSatirDurumu.Bekliyor => ("Aktarılacak", Color.Default, Icons.Material.Filled.Schedule),
        EkstreSatirDurumu.Aktarildi => ("Aktarıldı", Color.Success, Icons.Material.Filled.CheckCircle),
        EkstreSatirDurumu.Atlandi => ("Atlandı", Color.Default, Icons.Material.Filled.DoNotDisturbOn),
        EkstreSatirDurumu.ZatenKayitli => ("Kayıtlı hareketle eşleşti", Color.Info, Icons.Material.Filled.Link),
        EkstreSatirDurumu.DahaOnceAktarildi => ("Daha önce aktarıldı", Color.Default, Icons.Material.Filled.History),
        _ => ("Hatalı", Color.Error, Icons.Material.Filled.ErrorOutline)
    };

    public static (string Metin, Color Renk) AktarimDurumu(AktarimDurumu durum) => durum switch
    {
        Modules.KasaBanka.Contracts.AktarimDurumu.EslemeBekliyor => ("Kolon eşleme bekliyor", Color.Warning),
        Modules.KasaBanka.Contracts.AktarimDurumu.OnayBekliyor => ("Onay bekliyor", Color.Info),
        _ => ("Tamamlandı", Color.Success)
    };

    public static string Bicim(EkstreBicimi bicim) => bicim switch
    {
        EkstreBicimi.Csv => "CSV",
        EkstreBicimi.Excel => "Excel",
        _ => "MT940"
    };

    public static string Yon(KuralYonu yon) => yon switch
    {
        KuralYonu.Giris => "Giriş",
        KuralYonu.Cikis => "Çıkış",
        _ => "Her iki yön"
    };

    // Kolon harfi: 0 → A, 26 → AA.
    public static string KolonHarfi(int i) => i < 26 ? ((char)('A' + i)).ToString() : KolonHarfi(i / 26 - 1) + (char)('A' + i % 26);

    // "Kuralı hatırla" için açıklamadan ilk ayırt edici kelime: banka kalıp kelimeleri ve sayılar atlanır.
    // Kısa kelimeye, açıklamada hemen ardından gelen kelime eklenir (kural "içerir" ile eşleştiği için bitişik olmalı).
    public static string OnerilenKelime(string aciklama)
    {
        var kelimeler = aciklama.ToUpper(CultureInfo.GetCultureInfo("tr-TR")).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var i = Array.FindIndex(kelimeler, k => k.Length >= 3 && !k.Any(char.IsDigit) && !_genelKelimeler.Contains(k.Trim(':', '/', '-', ',', '.')));
        if (i < 0)
        {
            return "";
        }

        var kelime = kelimeler[i].Trim(':', '/', '-', ',', '.');
        return kelime.Length < 6 && i + 1 < kelimeler.Length && !kelimeler[i + 1].Any(char.IsDigit) ? $"{kelimeler[i]} {kelimeler[i + 1]}".Trim(':', ',', '.') : kelime;
    }
}
