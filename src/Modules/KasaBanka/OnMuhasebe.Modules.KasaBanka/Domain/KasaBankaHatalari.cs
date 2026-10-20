using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.KasaBanka.Domain;

internal static class KasaBankaHatalari
{
    public static readonly Hata HesapBulunamadi = Hata.Bulunamadi("hesap_bulunamadi", "Kasa/banka hesabı bulunamadı.");
    public static readonly Hata IslemBulunamadi = Hata.Bulunamadi("islem_bulunamadi", "İşlem bulunamadı.");
    public static readonly Hata KategoriBulunamadi = Hata.Bulunamadi("kategori_bulunamadi", "Gelir/gider kategorisi bulunamadı.");
    public static readonly Hata CariBulunamadi = Hata.Bulunamadi("cari_bulunamadi", "Cari hesap bulunamadı.");
    public static readonly Hata KodKullaniliyor = Hata.Cakisma("kod_kullaniliyor", "Bu hesap kodu başka bir hesapta kullanılıyor.");
    public static readonly Hata KategoriAdiKullaniliyor = Hata.Cakisma("kategori_adi_kullaniliyor", "Bu isimde bir kategori zaten var.");
    public static readonly Hata PasifHesap = Hata.IsKurali("pasif_hesap", "Pasif hesaba işlem yapılamaz.");
    public static readonly Hata PasifCari = Hata.IsKurali("pasif_cari", "Pasif cariye işlem yapılamaz.");
    public static readonly Hata HareketliHesapSilinemez = Hata.IsKurali("hareketli_hesap_silinemez", "Hareketi olan hesap silinemez; pasifleştirebilirsiniz.");
    public static readonly Hata HareketliHesapDegistirilemez = Hata.IsKurali("hareketli_hesap", "Hareketi olan hesabın türü ve para birimi değiştirilemez.");
    public static readonly Hata KullanilanKategoriSilinemez = Hata.IsKurali("kategori_kullaniliyor", "Bu kategoride kayıt var; pasifleştirebilirsiniz.");
    public static readonly Hata TutarPozitifOlmali = Hata.Dogrulama("tutar_gecersiz", "Tutar sıfırdan büyük olmalıdır.");
    public static readonly Hata KurGerekli = Hata.Dogrulama("kur_gerekli", "Bu tarih için TCMB kuru bulunamadı; kuru elle girin.");
    public static readonly Hata AyniHesap = Hata.Dogrulama("ayni_hesap", "Kaynak ve hedef hesap farklı olmalıdır.");
    public static readonly Hata FarkliDoviz = Hata.IsKurali("farkli_doviz", "Hesabın dövizi carinin dövizinden farklı. Cari kartında 'Farklı dövizde tahsilat/ödeme' açılmadıkça işlem yapılamaz.");
    public static readonly Hata AcilisZatenVar = Hata.IsKurali("acilis_zaten_var", "Bu hesabın açılış bakiyesi zaten girilmiş; önce mevcut açılışı iptal edin.");
    public static readonly Hata BicimGecersiz = Hata.Dogrulama("bicim_gecersiz", "'Ödendi' kaydında hesap, 'Tahakkuk' kaydında cari seçilmelidir.");
    public static readonly Hata KategoriTuruUyusmuyor = Hata.Dogrulama("kategori_turu", "Kategori türü (gelir/gider) işlem türüyle uyuşmuyor.");
    public static readonly Hata KaynakBelgedenIptal = Hata.IsKurali("kaynak_belgeden_iptal", "Bu işlem faturaya bağlı; faturadan iptal edilmelidir.");
    public static readonly Hata ZatenIptalEdilmis = Hata.IsKurali("zaten_iptal", "Bu işlem zaten iptal edilmiş.");
    public static readonly Hata CariLisansiYok = Hata.Yetki("cari_lisansi_yok", "Cari işlemi için Cari modülü lisansı gerekir.");
}
