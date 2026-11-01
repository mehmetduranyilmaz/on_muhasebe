using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Moduller;

namespace OnMuhasebe.Web;

// Adresin açılması için gereken yetki. Yetkisiz kullanıcıya sayfa hiç oluşturulmaz (gereksiz API çağrısı ve 403 olmaz).
public static class SayfaYetkileri
{
    private static readonly Dictionary<string, string> _yonetim = new()
    {
        ["kullanicilar"] = YonetimYetkileri.Kullanici.Yetki(YetkiIslemleri.Goruntule),
        ["roller"] = YonetimYetkileri.Kullanici.Yetki(YetkiIslemleri.Goruntule),
        ["abonelik"] = YonetimYetkileri.Abonelik.Yetki(YetkiIslemleri.Goruntule),
        ["ayarlar"] = YonetimYetkileri.Firma.Yetki(YetkiIslemleri.Duzenle)
    };

    // Ana sayfadaki modül özet panelleri modülün rapor yetkisini ister.
    public static IReadOnlyDictionary<string, string> OzetPaneli { get; } = new Dictionary<string, string>
    {
        [ModulKodlari.Cari] = "cari.rapor.goruntule",
        [ModulKodlari.Stok] = "stok.rapor.goruntule",
        [ModulKodlari.Fatura] = "fatura.rapor.goruntule",
        [ModulKodlari.KasaBanka] = "kasabanka.rapor.goruntule",
        [ModulKodlari.Muhasebe] = "muhasebe.aktarim.goruntule",
        [ModulKodlari.EvrakTakip] = "evraktakip.kayit.goruntule"
    };

    public static string? Gereken(string adres) =>
        ModulPaketleri.AdrestekiModul(adres)?.GorunumYetkisi
        ?? _yonetim.GetValueOrDefault(adres.TrimStart('/').Split('/', '?', '#')[0]);
}
