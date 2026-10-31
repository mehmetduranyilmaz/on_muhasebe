using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Fatura.Domain;

internal static class IrsaliyeHatalari
{
    public static readonly Hata IrsaliyeBulunamadi = Hata.Bulunamadi("irsaliye_bulunamadi", "İrsaliye bulunamadı.");
    public static readonly Hata SatirGerekli = Hata.Dogrulama("irsaliye_satir_gerekli", "İrsaliyede en az bir satır olmalıdır.");
    public static readonly Hata UrunKartiGerekli = Hata.Dogrulama("irsaliye_urun_karti", "İrsaliyeye yalnızca ürün kartı eklenebilir; hizmet sevk edilmez.");
    public static readonly Hata CariGerekli = Hata.Dogrulama("irsaliye_cari_gerekli", "İrsaliye için cari seçilmelidir.");
    public static readonly Hata DepoSevkindeCariOlmaz = Hata.Dogrulama("depo_sevk_cari", "Depolar arası sevk irsaliyesinde cari seçilmez.");
    public static readonly Hata HedefDepoGerekli = Hata.Dogrulama("hedef_depo_gerekli", "Depolar arası sevkte malın gideceği depo seçilmelidir.");
    public static readonly Hata HedefDepoAyni = Hata.Dogrulama("hedef_depo_ayni", "Malın çıktığı depo ile gideceği depo aynı olamaz.");
    public static readonly Hata SevkTarihtenOnceOlamaz = Hata.Dogrulama("sevk_tarihi_gecersiz", "Fiili sevk tarihi irsaliye düzenleme tarihinden önce olamaz.");
    public static readonly Hata MaliyetGerekli = Hata.Dogrulama("irsaliye_maliyet_gerekli",
        "Alış irsaliyesinde mal stoğa maliyetiyle girer; her satıra (tahmini) birim fiyat girilmelidir. Fatura gelince maliyet güncellenir.");
    public static readonly Hata NumaraGerekli = Hata.Dogrulama("irsaliye_numara_gerekli", "Gelen irsaliyenin (alış, satıştan iade) numarası girilmelidir.");
    public static readonly Hata NumaraKullaniliyor = Hata.Cakisma("irsaliye_numara_kullaniliyor", "Bu cariye ait aynı numaralı bir irsaliye zaten kayıtlı.");
    public static readonly Hata TurDegistirilemez = Hata.IsKurali("irsaliye_tur_degistirilemez", "İrsaliyenin türü değiştirilemez.");
    public static readonly Hata IptalEdilmis = Hata.IsKurali("irsaliye_iptal", "İptal edilmiş irsaliye üzerinde işlem yapılamaz.");
    public static readonly Hata ZatenOnayli = Hata.IsKurali("irsaliye_zaten_onayli", "İrsaliye zaten onaylı.");
    public static readonly Hata TaslakIptalEdilemez = Hata.IsKurali("irsaliye_taslak_iptal", "Taslak irsaliye iptal edilmez; silebilirsiniz.");
    public static readonly Hata YalnizcaTaslakSilinir = Hata.IsKurali("irsaliye_yalnizca_taslak_silinir", "Yalnızca taslak irsaliye silinebilir; onaylı irsaliyeyi iptal edin.");
    public static readonly Hata OnayliDuzenlenemez = Hata.IsKurali("irsaliye_onayli_duzenlenemez", "Onaylı irsaliye düzenlenemez; iptal edip yeniden düzenleyebilirsiniz.");
    public static readonly Hata FaturalanmisDegistirilemez = Hata.IsKurali("irsaliye_faturalanmis",
        "Faturalanmış irsaliye düzenlenemez ve iptal edilemez; önce bağlı faturayı iptal edin.");
    public static readonly Hata FaturalanamazDurum = Hata.IsKurali("irsaliye_faturalanamaz", "Yalnızca onaylı alım/satım irsaliyesi faturalanabilir.");
    public static readonly Hata KalanAsildi = Hata.IsKurali("irsaliye_kalan_asildi", "Faturalanan miktar irsaliyenin faturalanmamış miktarını aşamaz.");
    public static readonly Hata SatirBulunamadi = Hata.Bulunamadi("irsaliye_satiri_bulunamadi", "İrsaliye satırı bulunamadı.");

    public static Hata TarihSirasi(DateOnly sinir, bool oncekiMi) => Hata.IsKurali("irsaliye_tarih_sirasi", oncekiMi
        ? $"Bu seride {sinir:dd.MM.yyyy} tarihli daha önce numaralanmış irsaliye var; irsaliye tarihi bundan önce olamaz."
        : $"Bu seride {sinir:dd.MM.yyyy} tarihli sonraki numaralı irsaliye var; irsaliye tarihi bundan sonra olamaz.");
}
