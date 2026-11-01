using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Moduller;

namespace OnMuhasebe.Modules.Fatura.Contracts;

public static class FaturaYetkileri
{
    private const string Baslik = "Fatura";

    // Belge formları cari, stok, depo, fiyat listesi, kasa hesabı ve masraf merkezi seçer; bunları görmeden belge kesilemez.
    private static readonly IReadOnlyList<string> _belgeGerekenleri =
    [
        "cari.hesap.goruntule", "stok.kart.goruntule", "stok.tanim.goruntule", "stok.fiyat.goruntule",
        "kasabanka.hesap.goruntule", "kasabanka.tanim.goruntule"
    ];

    // Fatura, irsaliye ve siparişten; irsaliye siparişten aktarılarak kesilebilir.
    public static readonly YetkiKaynagi Belge = new("fatura.belge", ModulKodlari.Fatura, Baslik, "Faturalar",
        YetkiIslemleri.Belge, [.. _belgeGerekenleri, "fatura.irsaliye.goruntule", "fatura.teklif.goruntule"]);

    public static readonly YetkiKaynagi Irsaliye = new("fatura.irsaliye", ModulKodlari.Fatura, Baslik, "İrsaliyeler",
        YetkiIslemleri.Belge, [.. _belgeGerekenleri, "fatura.teklif.goruntule"]);

    public static readonly YetkiKaynagi Teklif = new("fatura.teklif", ModulKodlari.Fatura, Baslik, "Teklif ve siparişler",
        YetkiIslemleri.Belge, _belgeGerekenleri);

    public static readonly YetkiKaynagi Rapor = new("fatura.rapor", ModulKodlari.Fatura, Baslik, "Fatura raporları (KDV, Ba/Bs, masraf merkezi)",
        YetkiIslemleri.SadeceGoruntule, ["cari.hesap.goruntule"]);

    public static readonly YetkiKaynagi Tekrar = new("fatura.tekrar", ModulKodlari.Fatura, Baslik, "Tekrarlayan faturalar",
        YetkiIslemleri.Temel, [.. _belgeGerekenleri]);

    public static IReadOnlyList<YetkiKaynagi> Tumu { get; } = [Belge, Irsaliye, Teklif, Tekrar, Rapor];
}
