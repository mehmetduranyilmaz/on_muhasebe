using MudBlazor;
using OnMuhasebe.Modules.KasaBanka.Contracts;

namespace OnMuhasebe.Web.KasaBanka;

// Ekrandaki işlem türleri; adres çubuğunda kasabanka/islem/{yol} olarak geçer.
public enum IslemTuru
{
    Tahsilat,
    Odeme,
    Virman,
    Gelir,
    Gider,
    Acilis
}

public static class KasaMetinleri
{
    public static IReadOnlyList<HesapTuru> HesapTurleri { get; } = Enum.GetValues<HesapTuru>();

    public static string HesapTuru(HesapTuru tur) => tur switch
    {
        Modules.KasaBanka.Contracts.HesapTuru.Kasa => "Kasa",
        Modules.KasaBanka.Contracts.HesapTuru.Banka => "Banka",
        Modules.KasaBanka.Contracts.HesapTuru.KrediKarti => "Kredi kartı",
        Modules.KasaBanka.Contracts.HesapTuru.Pos => "POS",
        _ => tur.ToString()
    };

    public static string HesapTuruAciklamasi(HesapTuru tur) => tur switch
    {
        Modules.KasaBanka.Contracts.HesapTuru.Kasa => "Nakit para",
        Modules.KasaBanka.Contracts.HesapTuru.Banka => "Vadesiz banka hesabı",
        Modules.KasaBanka.Contracts.HesapTuru.KrediKarti => "Firmanın kartı; harcamalar borç olarak birikir",
        Modules.KasaBanka.Contracts.HesapTuru.Pos => "Kartla tahsilatların biriktiği POS hesabı",
        _ => ""
    };

    public static string HesapIkonu(HesapTuru tur) => tur switch
    {
        Modules.KasaBanka.Contracts.HesapTuru.Kasa => Icons.Material.Filled.Payments,
        Modules.KasaBanka.Contracts.HesapTuru.Banka => Icons.Material.Filled.AccountBalance,
        Modules.KasaBanka.Contracts.HesapTuru.KrediKarti => Icons.Material.Filled.CreditCard,
        _ => Icons.Material.Filled.PointOfSale
    };

    // Hesap kartlarının degrade renkleri.
    public static (string Bas, string Son) HesapRenkleri(HesapTuru tur) => tur switch
    {
        Modules.KasaBanka.Contracts.HesapTuru.Kasa => ("#0F766E", "#14B8A6"),
        Modules.KasaBanka.Contracts.HesapTuru.Banka => ("#1E3A5F", "#3B82F6"),
        Modules.KasaBanka.Contracts.HesapTuru.KrediKarti => ("#4C1D95", "#8B5CF6"),
        _ => ("#9A3412", "#F59E0B")
    };

    public static string HareketTuru(KasaHareketTuru tur) => tur switch
    {
        KasaHareketTuru.Acilis => "Açılış",
        KasaHareketTuru.Tahsilat => "Tahsilat",
        KasaHareketTuru.Odeme => "Ödeme",
        KasaHareketTuru.VirmanGiris => "Virman girişi",
        KasaHareketTuru.VirmanCikis => "Virman çıkışı",
        KasaHareketTuru.Gelir => "Gelir",
        KasaHareketTuru.Gider => "Gider",
        _ => tur.ToString()
    };

    public static IReadOnlyList<IslemTuru> IslemTurleri { get; } = [IslemTuru.Tahsilat, IslemTuru.Odeme, IslemTuru.Virman, IslemTuru.Gelir, IslemTuru.Gider];

    public static string Islem(IslemTuru tur) => tur switch
    {
        IslemTuru.Tahsilat => "Tahsilat",
        IslemTuru.Odeme => "Ödeme",
        IslemTuru.Virman => "Virman",
        IslemTuru.Gelir => "Gelir",
        IslemTuru.Gider => "Gider",
        IslemTuru.Acilis => "Açılış bakiyesi",
        _ => tur.ToString()
    };

    public static string IslemAciklamasi(IslemTuru tur) => tur switch
    {
        IslemTuru.Tahsilat => "Cariden para alındı; cari alacaklanır",
        IslemTuru.Odeme => "Cariye para ödendi; cari borçlanır",
        IslemTuru.Virman => "Hesaplar arası aktarım, kart borcu ödeme, döviz bozdurma",
        IslemTuru.Gelir => "Faiz, kira geliri gibi satış dışı gelir",
        IslemTuru.Gider => "Kira, fatura, maaş gibi masraflar",
        IslemTuru.Acilis => "Programa geçerken hesaptaki tutar",
        _ => ""
    };

    public static string IslemIkonu(IslemTuru tur) => tur switch
    {
        IslemTuru.Tahsilat => Icons.Material.Filled.CallReceived,
        IslemTuru.Odeme => Icons.Material.Filled.CallMade,
        IslemTuru.Virman => Icons.Material.Filled.SwapHoriz,
        IslemTuru.Gelir => Icons.Material.Filled.TrendingUp,
        IslemTuru.Gider => Icons.Material.Filled.TrendingDown,
        _ => Icons.Material.Filled.FlagCircle
    };

    public static string IslemRengi(IslemTuru tur) => tur switch
    {
        IslemTuru.Tahsilat or IslemTuru.Gelir => "#16A34A",
        IslemTuru.Odeme or IslemTuru.Gider => "#DC2626",
        IslemTuru.Virman => "#3B82F6",
        _ => "#64748B"
    };

    public static string Yol(IslemTuru tur) => tur.ToString().ToLowerInvariant();

    public static IslemTuru? YoldanIslem(string? yol) =>
        Enum.GetValues<IslemTuru>().Cast<IslemTuru?>().FirstOrDefault(t => Yol(t!.Value) == yol);

    // Kasa/banka işlemi menüsünden ya da başka modülden (cari kartı, fatura) işlem formuna gidiş adresi.
    public static string IslemAdresi(IslemTuru tur, Guid? hesapId = null, Guid? cariId = null, decimal? tutar = null, string? donus = null)
    {
        string?[] parametreler =
        [
            hesapId is { } h ? $"hesap={h}" : null,
            cariId is { } c ? $"cari={c}" : null,
            tutar is { } t ? $"tutar={t.ToString(System.Globalization.CultureInfo.InvariantCulture)}" : null,
            donus is not null ? $"donus={Uri.EscapeDataString(donus)}" : null
        ];
        var sorgu = string.Join("&", parametreler.Where(p => p is not null));
        return $"kasabanka/islem/{Yol(tur)}{(sorgu.Length > 0 ? "?" + sorgu : "")}";
    }
}
