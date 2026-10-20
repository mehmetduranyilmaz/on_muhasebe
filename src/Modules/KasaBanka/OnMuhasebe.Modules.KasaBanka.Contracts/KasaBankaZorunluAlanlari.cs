using OnMuhasebe.Shared.Contracts.Ayarlar;
using OnMuhasebe.Shared.Contracts.Moduller;

namespace OnMuhasebe.Modules.KasaBanka.Contracts;

// Firmanın kasa/banka işlemlerinde ve çek/senet bordrolarında zorunlu yapabileceği alanlar; ekran ve API aynı kodları kullanır.
// Banka ekstresinden aktarılan satırlar elle girilmediği için bu kurala tabi değildir.
public static class KasaBankaZorunluAlanlari
{
    private const string IslemFormu = "Kasa/banka işlemi";
    private const string CekSenetFormu = "Çek/senet bordrosu";

    // Tahsilat, ödeme ve gelir/gider.
    public const string IslemBelgeNo = "kasabanka.islem.belgeNo";

    // Tahsilat, ödeme, gelir/gider ve virman.
    public const string IslemAciklama = "kasabanka.islem.aciklama";
    public const string BordroAciklama = "kasabanka.bordro.aciklama";
    public const string SenetKesideYeri = "kasabanka.bordro.senetKesideYeri";
    public const string SenetKefil = "kasabanka.bordro.senetKefil";

    public static IReadOnlyList<ZorunluAlanTanimi> Tumu { get; } =
    [
        new(IslemBelgeNo, ModulKodlari.KasaBanka, IslemFormu, "Belge no (tahsilat, ödeme, gelir/gider)"),
        new(IslemAciklama, ModulKodlari.KasaBanka, IslemFormu, "Açıklama"),
        new(BordroAciklama, ModulKodlari.KasaBanka, CekSenetFormu, "Bordro açıklaması"),
        new(SenetKesideYeri, ModulKodlari.KasaBanka, CekSenetFormu, "Senette keşide yeri"),
        new(SenetKefil, ModulKodlari.KasaBanka, CekSenetFormu, "Senette kefil")
    ];
}
