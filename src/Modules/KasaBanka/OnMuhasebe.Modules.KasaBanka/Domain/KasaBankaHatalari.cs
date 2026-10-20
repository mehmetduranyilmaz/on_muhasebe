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
    public static readonly Hata KasayaEkstreYok = Hata.IsKurali("kasaya_ekstre_yok", "Ekstre yalnızca banka, kredi kartı ve POS hesaplarına aktarılır.");
    public static readonly Hata DosyaOkunamadi = Hata.Dogrulama("dosya_okunamadi", "Dosya okunamadı. CSV, Excel (.xlsx/.xls) veya MT940 biçiminde bir ekstre yükleyin.");
    public static readonly Hata DosyaCokBuyuk = Hata.Dogrulama("dosya_cok_buyuk", "Ekstre dosyası en fazla 5 MB olabilir.");
    public static readonly Hata EkstreBos = Hata.Dogrulama("ekstre_bos", "Dosyada aktarılacak hareket bulunamadı.");
    public static readonly Hata AktarimBulunamadi = Hata.Bulunamadi("aktarim_bulunamadi", "Ekstre aktarımı veya satırı bulunamadı.");
    public static readonly Hata EslemeDegistirilemez = Hata.IsKurali("esleme_degistirilemez", "Satırları aktarılmış ya da MT940 biçimindeki aktarımın kolon eşlemesi değiştirilemez.");
    public static readonly Hata EslemeSatirVermedi = Hata.Dogrulama("esleme_satir_vermedi", "Bu kolon eşlemesiyle hiçbir satır okunamadı; tarih ve tutar kolonlarını kontrol edin.");
    public static readonly Hata SatirDegistirilemez = Hata.IsKurali("satir_degistirilemez", "Aktarılmış ya da atlanmış satır değiştirilemez.");
    public static readonly Hata AktarimHazirDegil = Hata.IsKurali("aktarim_hazir_degil", "Önce kolon eşlemesini tamamlayın; tamamlanmış aktarım tekrar aktarılamaz.");
    public static readonly Hata AktarimSilinemez = Hata.IsKurali("aktarim_silinemez", "Satırı aktarılmış aktarım silinemez.");
    public static readonly Hata SatirCariGerekli = Hata.Dogrulama("satir_cari_gerekli", "Tahsilat/ödeme için cari seçilmeli.");
    public static readonly Hata SatirKategoriGerekli = Hata.Dogrulama("satir_kategori_gerekli", "Gelir/gider için kategori seçilmeli.");
    public static readonly Hata SatirKarsiHesapGerekli = Hata.Dogrulama("satir_karsi_hesap_gerekli", "Virman için karşı hesap seçilmeli.");
    public static readonly Hata SatirYonuUyusmuyor = Hata.Dogrulama("satir_yonu", "İşlem türü satırın yönüyle uyuşmuyor (giriş: tahsilat/gelir/virman, çıkış: ödeme/gider/virman).");
    public static readonly Hata SatirVirmanDovizi = Hata.IsKurali("satir_virman_dovizi", "Ekstreden virman yalnızca aynı dövizdeki hesaplar arasında yapılır; farklı dövizli virmanı elle girin.");
    public static readonly Hata KuralBulunamadi = Hata.Bulunamadi("kural_bulunamadi", "Kural bulunamadı.");
}
