using MudBlazor;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Web.Tema;

namespace OnMuhasebe.Web.Fatura;

public static class FaturaMetinleri
{
    public static IReadOnlyList<FaturaTuru> Turler { get; } = Enum.GetValues<FaturaTuru>();

    public static string Tur(FaturaTuru tur) => tur switch
    {
        FaturaTuru.Satis => "Satış Faturası",
        FaturaTuru.Alis => "Alış Faturası",
        FaturaTuru.SatistanIade => "Satıştan İade",
        FaturaTuru.AlistanIade => "Alıştan İade",
        FaturaTuru.GiderPusulasi => "Gider Pusulası",
        FaturaTuru.SerbestMeslekMakbuzu => "Serbest Meslek Makbuzu",
        _ => tur.ToString()
    };

    public static string KisaTur(FaturaTuru tur) => tur switch
    {
        FaturaTuru.Satis => "Satış",
        FaturaTuru.Alis => "Alış",
        FaturaTuru.SatistanIade => "Satıştan iade",
        FaturaTuru.AlistanIade => "Alıştan iade",
        FaturaTuru.GiderPusulasi => "Gider pusulası",
        FaturaTuru.SerbestMeslekMakbuzu => "SMM",
        _ => tur.ToString()
    };

    public static string TurAciklamasi(FaturaTuru tur) => tur switch
    {
        FaturaTuru.Satis => "Müşteriye satış; cari borçlanır, stok çıkar",
        FaturaTuru.Alis => "Tedarikçiden alış; cari alacaklanır, stok girer",
        FaturaTuru.SatistanIade => "Müşteriden geri gelen mal; stok girer",
        FaturaTuru.AlistanIade => "Tedarikçiye geri gönderilen mal; stok çıkar",
        FaturaTuru.GiderPusulasi => "Mükellef olmayandan alım; KDV yok, stopaj kesilir",
        FaturaTuru.SerbestMeslekMakbuzu => "Serbest meslek ödemesi; KDV ve gelir vergisi stopajı",
        _ => ""
    };

    public static string Ikon(FaturaTuru tur) => tur switch
    {
        FaturaTuru.Satis => Icons.Material.Filled.PointOfSale,
        FaturaTuru.Alis => Icons.Material.Filled.ShoppingCart,
        FaturaTuru.SatistanIade => Icons.Material.Filled.AssignmentReturn,
        FaturaTuru.AlistanIade => Icons.Material.Filled.KeyboardReturn,
        FaturaTuru.GiderPusulasi => Icons.Material.Filled.ReceiptLong,
        FaturaTuru.SerbestMeslekMakbuzu => Icons.Material.Filled.WorkOutline,
        _ => Icons.Material.Filled.ReceiptLong
    };

    public static string Renk(FaturaTuru tur) => tur switch
    {
        FaturaTuru.Satis => "#14B8A6",
        FaturaTuru.Alis => "#3B82F6",
        FaturaTuru.SatistanIade => "#F59E0B",
        FaturaTuru.AlistanIade => "#8B5CF6",
        FaturaTuru.GiderPusulasi => "#EC4899",
        FaturaTuru.SerbestMeslekMakbuzu => "#0EA5E9",
        _ => UygulamaTemasi.Turkuaz
    };

    // Adres çubuğundaki tür adı: fatura/yeni/satis, fatura/yeni/gider-pusulasi...
    public static string Yol(FaturaTuru tur) => tur switch
    {
        FaturaTuru.Satis => "satis",
        FaturaTuru.Alis => "alis",
        FaturaTuru.SatistanIade => "satistan-iade",
        FaturaTuru.AlistanIade => "alistan-iade",
        FaturaTuru.GiderPusulasi => "gider-pusulasi",
        FaturaTuru.SerbestMeslekMakbuzu => "serbest-meslek-makbuzu",
        _ => tur.ToString().ToLowerInvariant()
    };

    public static FaturaTuru? YoldanTur(string? yol) => Turler.Cast<FaturaTuru?>().FirstOrDefault(t => Yol(t!.Value) == yol);

    public static string Durum(FaturaDurumu durum) => durum switch
    {
        FaturaDurumu.Taslak => "Taslak",
        FaturaDurumu.Onayli => "Onaylı",
        FaturaDurumu.Iptal => "İptal",
        _ => durum.ToString()
    };

    public static Color DurumRengi(FaturaDurumu durum) => durum switch
    {
        FaturaDurumu.Taslak => Color.Warning,
        FaturaDurumu.Onayli => Color.Success,
        _ => Color.Error
    };

    public static string SatisSekli(SatisSekli? sekil) => sekil switch
    {
        Modules.Fatura.Contracts.SatisSekli.Toptan => "Toptan",
        Modules.Fatura.Contracts.SatisSekli.Perakende => "Perakende",
        _ => ""
    };

    public static string KayitAkisi(KayitAkisi akis) => akis switch
    {
        Modules.Fatura.Contracts.KayitAkisi.DogrudanKayit => "Doğrudan kayıt",
        Modules.Fatura.Contracts.KayitAkisi.TaslakOnay => "Taslak ve onay",
        _ => akis.ToString()
    };

    public static string KayitAkisiAciklamasi(KayitAkisi akis) => akis switch
    {
        Modules.Fatura.Contracts.KayitAkisi.DogrudanKayit =>
            "Kaydedilen fatura hemen kesinleşir, cari ve stoğa işlenir. Düzeltme gerekirse fatura düzenlenir; hareketler kendiliğinden yenilenir.",
        Modules.Fatura.Contracts.KayitAkisi.TaslakOnay =>
            "Fatura önce taslak olarak kaydedilir ve serbestçe düzenlenir. Onaylanınca numara alır ve işlenir; onaylı fatura yalnızca iptal edilebilir.",
        _ => ""
    };

    // Faturanın alt kısmındaki "Yalnız ..." satırı: 1.234,56 TL -> "Bin İki Yüz Otuz Dört Türk Lirası Elli Altı Kuruş".
    public static string Yaziyla(decimal tutar, string paraBirimi)
    {
        tutar = Math.Round(Math.Abs(tutar), 2, MidpointRounding.AwayFromZero);
        var tam = (long)decimal.Truncate(tutar);
        var kurus = (int)((tutar - tam) * 100);
        var (ana, alt) = paraBirimi == ParaBirimleri.Tl ? ("Türk Lirası", "Kuruş") : (paraBirimi, "Cent");
        var metin = $"{SayiYaziyla(tam)} {ana}";
        return kurus > 0 ? $"{metin} {SayiYaziyla(kurus)} {alt}" : metin;
    }

    private static readonly string[] _birler = ["", "Bir", "İki", "Üç", "Dört", "Beş", "Altı", "Yedi", "Sekiz", "Dokuz"];
    private static readonly string[] _onlar = ["", "On", "Yirmi", "Otuz", "Kırk", "Elli", "Altmış", "Yetmiş", "Seksen", "Doksan"];
    private static readonly string[] _basamaklar = ["", "Bin", "Milyon", "Milyar", "Trilyon"];

    private static string SayiYaziyla(long sayi)
    {
        if (sayi == 0)
        {
            return "Sıfır";
        }

        var parcalar = new List<string>();
        for (var i = 0; sayi > 0; i++, sayi /= 1000)
        {
            var uclu = (int)(sayi % 1000);
            if (uclu == 0)
            {
                continue;
            }

            // Türkçede "bir bin" denmez: 1.000 = "Bin".
            var metin = i == 1 && uclu == 1 ? "" : UcHane(uclu);
            parcalar.Insert(0, $"{metin} {_basamaklar[i]}".Trim());
        }

        return string.Join(" ", parcalar);
    }

    private static string UcHane(int sayi)
    {
        var yuz = sayi / 100;
        var yuzMetni = yuz switch { 0 => "", 1 => "Yüz", _ => $"{_birler[yuz]} Yüz" };
        return $"{yuzMetni} {_onlar[sayi / 10 % 10]} {_birler[sayi % 10]}".Replace("  ", " ").Trim();
    }
}
