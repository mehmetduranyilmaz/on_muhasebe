using OnMuhasebe.Shared.Contracts.Ayarlar;
using OnMuhasebe.Shared.Contracts.Moduller;

namespace OnMuhasebe.Modules.Stok.Contracts;

// Firmanın stok kartında zorunlu yapabileceği alanlar; ekran ve API aynı kodları kullanır.
public static class StokZorunluAlanlari
{
    private const string Form = "Stok kartı";

    public const string Barkod = "stok.kart.barkod";
    public const string Grup = "stok.kart.grup";
    public const string AlisFiyati = "stok.kart.alisFiyati";
    public const string SatisFiyati = "stok.kart.satisFiyati";
    public const string PerakendeFiyati = "stok.kart.perakendeFiyati";
    public const string KritikSeviye = "stok.kart.kritikSeviye";
    public const string Aciklama = "stok.kart.aciklama";

    public static IReadOnlyList<ZorunluAlanTanimi> Tumu { get; } =
    [
        new(Barkod, ModulKodlari.Stok, Form, "Barkod"),
        new(Grup, ModulKodlari.Stok, Form, "Grup"),
        new(AlisFiyati, ModulKodlari.Stok, Form, "Alış fiyatı"),
        new(SatisFiyati, ModulKodlari.Stok, Form, "Satış fiyatı"),
        new(PerakendeFiyati, ModulKodlari.Stok, Form, "Perakende fiyatı"),
        new(KritikSeviye, ModulKodlari.Stok, Form, "Kritik stok seviyesi"),
        new(Aciklama, ModulKodlari.Stok, Form, "Açıklama")
    ];
}
