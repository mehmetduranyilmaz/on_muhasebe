using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Moduller;

namespace OnMuhasebe.Modules.Stok.Contracts;

public static class StokYetkileri
{
    private const string Baslik = "Stok";

    public static readonly YetkiKaynagi Kart = new("stok.kart", ModulKodlari.Stok, Baslik, "Stok kartları",
        YetkiIslemleri.Temel, ["stok.tanim.goruntule"]);

    public static readonly YetkiKaynagi Tanim = new("stok.tanim", ModulKodlari.Stok, Baslik, "Depolar ve stok grupları",
        YetkiIslemleri.Temel, []);

    public static readonly YetkiKaynagi Hareket = new("stok.hareket", ModulKodlari.Stok, Baslik, "Stok hareketleri ve transfer",
        YetkiIslemleri.Hareket, ["stok.kart.goruntule"]);

    public static readonly YetkiKaynagi Fiyat = new("stok.fiyat", ModulKodlari.Stok, Baslik, "Fiyat listeleri",
        YetkiIslemleri.Temel, ["stok.kart.goruntule"]);

    public static readonly YetkiKaynagi Rapor = new("stok.rapor", ModulKodlari.Stok, Baslik, "Stok raporları",
        YetkiIslemleri.SadeceGoruntule, ["stok.kart.goruntule"]);

    // Özel yetki: birim maliyet, stok değeri, alış fiyatı ve kârlılık. Olmayana bu alanlar boş/sıfır döner.
    public static readonly YetkiKaynagi Maliyet = new("stok.maliyet", ModulKodlari.Stok, Baslik, "Maliyet, alış fiyatı ve kârlılık",
        YetkiIslemleri.SadeceGoruntule, []);

    public static IReadOnlyList<YetkiKaynagi> Tumu { get; } = [Kart, Tanim, Hareket, Fiyat, Rapor, Maliyet];
}
