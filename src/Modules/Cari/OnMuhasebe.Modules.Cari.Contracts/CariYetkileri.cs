using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Moduller;

namespace OnMuhasebe.Modules.Cari.Contracts;

public static class CariYetkileri
{
    private const string Baslik = "Cari";

    public static readonly YetkiKaynagi Hesap = new("cari.hesap", ModulKodlari.Cari, Baslik, "Cari hesaplar",
        YetkiIslemleri.Temel, ["cari.grup.goruntule"]);

    public static readonly YetkiKaynagi Grup = new("cari.grup", ModulKodlari.Cari, Baslik, "Cari grupları",
        YetkiIslemleri.Temel, []);

    public static readonly YetkiKaynagi Hareket = new("cari.hareket", ModulKodlari.Cari, Baslik, "Borç/alacak dekontları",
        YetkiIslemleri.Hareket, ["cari.hesap.goruntule"]);

    public static readonly YetkiKaynagi Kapama = new("cari.kapama", ModulKodlari.Cari, Baslik, "Kapama ve avans mahsubu",
        [YetkiIslemleri.Goruntule, YetkiIslemleri.Duzenle], ["cari.hesap.goruntule"]);

    public static readonly YetkiKaynagi Rapor = new("cari.rapor", ModulKodlari.Cari, Baslik, "Cari raporları (ekstre, bakiye, vade analizi)",
        YetkiIslemleri.SadeceGoruntule, ["cari.hesap.goruntule"]);

    public static IReadOnlyList<YetkiKaynagi> Tumu { get; } = [Hesap, Grup, Hareket, Kapama, Rapor];
}
