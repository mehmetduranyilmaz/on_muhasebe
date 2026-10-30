using MudBlazor;
using OnMuhasebe.Modules.KasaBanka.Contracts;

namespace OnMuhasebe.Web.KasaBanka;

public static class TeminatMetinleri
{
    public static IReadOnlyList<TeminatTuru> Turler { get; } = Enum.GetValues<TeminatTuru>();

    public static string Tur(TeminatTuru tur) => tur switch
    {
        TeminatTuru.TeminatMektubu => "Teminat mektubu",
        TeminatTuru.Ipotek => "İpotek",
        TeminatTuru.Rehin => "Rehin",
        TeminatTuru.Kefalet => "Kefalet",
        TeminatTuru.NakitTeminat => "Nakit teminat",
        TeminatTuru.TeminatCeki => "Teminat çeki",
        TeminatTuru.TeminatSenedi => "Teminat senedi",
        _ => "Diğer"
    };

    public static string TurIkonu(TeminatTuru tur) => tur switch
    {
        TeminatTuru.TeminatMektubu => Icons.Material.Filled.Description,
        TeminatTuru.Ipotek => Icons.Material.Filled.HomeWork,
        TeminatTuru.Rehin => Icons.Material.Filled.Lock,
        TeminatTuru.Kefalet => Icons.Material.Filled.Handshake,
        TeminatTuru.NakitTeminat => Icons.Material.Filled.Payments,
        TeminatTuru.TeminatCeki => Icons.Material.Filled.RequestQuote,
        TeminatTuru.TeminatSenedi => Icons.Material.Filled.ReceiptLong,
        _ => Icons.Material.Filled.Shield
    };

    public static string Yon(TeminatYonu yon) => yon == TeminatYonu.Alinan ? "Alınan" : "Verilen";

    // Alınan: taraftan lehimize; Verilen: karşı tarafa.
    public static string TarafEtiketi(TeminatYonu yon) => yon == TeminatYonu.Alinan ? "Veren taraf" : "Lehtar / muhatap";

    public static string Durum(TeminatDurumu durum) => durum switch
    {
        TeminatDurumu.Aktif => "Aktif",
        TeminatDurumu.IadeEdildi => "İade edildi",
        _ => "İrat edildi"
    };

    public static string DurumRengi(TeminatDurumu durum) => durum switch
    {
        TeminatDurumu.Aktif => "#16A34A",
        TeminatDurumu.IadeEdildi => "#64748B",
        _ => "#DC2626"
    };
}
