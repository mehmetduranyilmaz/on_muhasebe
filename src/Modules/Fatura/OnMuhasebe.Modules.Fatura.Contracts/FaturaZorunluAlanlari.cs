using OnMuhasebe.Shared.Contracts.Ayarlar;
using OnMuhasebe.Shared.Contracts.Moduller;

namespace OnMuhasebe.Modules.Fatura.Contracts;

// Firmanın faturada zorunlu yapabileceği alanlar; ekran ve API aynı kodları kullanır.
public static class FaturaZorunluAlanlari
{
    private const string Form = "Fatura";

    public const string VadeTarihi = "fatura.belge.vadeTarihi";
    public const string Aciklama = "fatura.belge.aciklama";

    public static IReadOnlyList<ZorunluAlanTanimi> Tumu { get; } =
    [
        new(VadeTarihi, ModulKodlari.Fatura, Form, "Vade tarihi"),
        new(Aciklama, ModulKodlari.Fatura, Form, "Açıklama")
    ];
}
