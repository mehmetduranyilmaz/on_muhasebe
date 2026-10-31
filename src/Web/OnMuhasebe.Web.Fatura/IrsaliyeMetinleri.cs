using MudBlazor;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Web.Tema;

namespace OnMuhasebe.Web.Fatura;

public static class IrsaliyeMetinleri
{
    public static IReadOnlyList<IrsaliyeTuru> Turler { get; } = Enum.GetValues<IrsaliyeTuru>();

    public static string Tur(IrsaliyeTuru tur) => tur switch
    {
        IrsaliyeTuru.Satis => "Satış İrsaliyesi",
        IrsaliyeTuru.Alis => "Alış İrsaliyesi",
        IrsaliyeTuru.SatistanIade => "Satıştan İade İrsaliyesi",
        IrsaliyeTuru.AlistanIade => "Alıştan İade İrsaliyesi",
        IrsaliyeTuru.DepoSevk => "Depolar Arası Sevk İrsaliyesi",
        _ => tur.ToString()
    };

    public static string KisaTur(IrsaliyeTuru tur) => tur switch
    {
        IrsaliyeTuru.Satis => "Satış",
        IrsaliyeTuru.Alis => "Alış",
        IrsaliyeTuru.SatistanIade => "Satıştan iade",
        IrsaliyeTuru.AlistanIade => "Alıştan iade",
        IrsaliyeTuru.DepoSevk => "Depo sevki",
        _ => tur.ToString()
    };

    public static string TurAciklamasi(IrsaliyeTuru tur) => tur switch
    {
        IrsaliyeTuru.Satis => "Müşteriye mal sevki; stok çıkar, fatura sonra kesilir",
        IrsaliyeTuru.Alis => "Tedarikçiden gelen mal; stok tahmini maliyetle girer",
        IrsaliyeTuru.SatistanIade => "Müşteriden geri gelen mal; stok girer",
        IrsaliyeTuru.AlistanIade => "Tedarikçiye geri gönderilen mal; stok çıkar",
        IrsaliyeTuru.DepoSevk => "Farklı adresteki depolar arasında mal taşıma",
        _ => ""
    };

    public static string Ikon(IrsaliyeTuru tur) => tur switch
    {
        IrsaliyeTuru.Satis => Icons.Material.Filled.LocalShipping,
        IrsaliyeTuru.Alis => Icons.Material.Filled.MoveToInbox,
        IrsaliyeTuru.SatistanIade => Icons.Material.Filled.AssignmentReturn,
        IrsaliyeTuru.AlistanIade => Icons.Material.Filled.KeyboardReturn,
        IrsaliyeTuru.DepoSevk => Icons.Material.Filled.SwapHoriz,
        _ => Icons.Material.Filled.LocalShipping
    };

    public static string Renk(IrsaliyeTuru tur) => tur switch
    {
        IrsaliyeTuru.Satis => "#14B8A6",
        IrsaliyeTuru.Alis => "#3B82F6",
        IrsaliyeTuru.SatistanIade => "#F59E0B",
        IrsaliyeTuru.AlistanIade => "#8B5CF6",
        IrsaliyeTuru.DepoSevk => "#64748B",
        _ => UygulamaTemasi.Turkuaz
    };

    public static string Yol(IrsaliyeTuru tur) => tur switch
    {
        IrsaliyeTuru.Satis => "satis",
        IrsaliyeTuru.Alis => "alis",
        IrsaliyeTuru.SatistanIade => "satistan-iade",
        IrsaliyeTuru.AlistanIade => "alistan-iade",
        IrsaliyeTuru.DepoSevk => "depo-sevk",
        _ => tur.ToString().ToLowerInvariant()
    };

    public static IrsaliyeTuru? YoldanTur(string? yol) => Turler.Cast<IrsaliyeTuru?>().FirstOrDefault(t => Yol(t!.Value) == yol);

    public static string Durum(IrsaliyeDurumu durum) => durum switch
    {
        IrsaliyeDurumu.Taslak => "Taslak",
        IrsaliyeDurumu.Onayli => "Onaylı",
        IrsaliyeDurumu.Iptal => "İptal",
        _ => durum.ToString()
    };

    public static Color DurumRengi(IrsaliyeDurumu durum) => durum switch
    {
        IrsaliyeDurumu.Taslak => Color.Warning,
        IrsaliyeDurumu.Onayli => Color.Success,
        _ => Color.Error
    };

    public static string Faturalama(FaturalamaDurumu durum) => durum switch
    {
        FaturalamaDurumu.Faturalanmadi => "Faturalanmadı",
        FaturalamaDurumu.KismenFaturalandi => "Kısmen faturalandı",
        FaturalamaDurumu.Faturalandi => "Faturalandı",
        _ => durum.ToString()
    };

    public static Color FaturalamaRengi(FaturalamaDurumu durum) => durum switch
    {
        FaturalamaDurumu.Faturalandi => Color.Success,
        FaturalamaDurumu.KismenFaturalandi => Color.Info,
        _ => Color.Default
    };
}
