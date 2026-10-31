using OnMuhasebe.Modules.KasaBanka.Contracts;

namespace OnMuhasebe.Web.KasaBanka;

public static class DemirbasMetinleri
{
    public static IReadOnlyList<DemirbasGrubu> Gruplar { get; } = Enum.GetValues<DemirbasGrubu>();

    public static string Grup(DemirbasGrubu grup) => grup switch
    {
        DemirbasGrubu.Binalar => "Binalar",
        DemirbasGrubu.TesisMakineCihaz => "Tesis, makine ve cihazlar",
        DemirbasGrubu.Tasitlar => "Taşıtlar",
        DemirbasGrubu.Demirbaslar => "Demirbaşlar (mobilya, büro)",
        DemirbasGrubu.Bilgisayar => "Bilgisayar ve çevre birimleri",
        DemirbasGrubu.OzelMaliyetler => "Özel maliyetler (kiralık yer)",
        DemirbasGrubu.Haklar => "Haklar (yazılım lisansı, marka)",
        _ => "Diğer"
    };

    public static string Yontem(AmortismanYontemi yontem) => yontem == AmortismanYontemi.Normal ? "Normal (eşit paylar)" : "Azalan bakiyeler";
}
