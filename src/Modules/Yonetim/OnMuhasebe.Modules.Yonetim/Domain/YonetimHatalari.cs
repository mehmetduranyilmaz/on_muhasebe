using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Yonetim.Domain;

internal static class YonetimHatalari
{
    public static readonly Hata GirisBasarisiz = Hata.KimlikDogrulama("giris_basarisiz", "E-posta veya şifre hatalı.");
    public static readonly Hata HesapKilitli = Hata.KimlikDogrulama("hesap_kilitli", "Çok sayıda hatalı deneme yapıldı. 15 dakika sonra tekrar deneyin ya da şifrenizi sıfırlayın.");
    public static readonly Hata FirmaYok = Hata.KimlikDogrulama("firma_yok", "Hesabınız aktif bir firmaya bağlı değil.");
    public static readonly Hata OturumGecersiz = Hata.KimlikDogrulama("oturum_gecersiz", "Oturumunuzun süresi doldu. Lütfen tekrar giriş yapın.");
    public static readonly Hata FirmaUyesiDegil = Hata.Yetki("firma_uyesi_degil", "Bu firmaya erişiminiz yok.");
    public static readonly Hata EpostaKayitli = Hata.Cakisma("eposta_kayitli", "Bu e-posta adresiyle zaten bir hesap var.");
    public static readonly Hata ZatenFirmaUyesi = Hata.Cakisma("zaten_firma_uyesi", "Bu kullanıcı zaten firmanıza bağlı.");
    public static readonly Hata MevcutSifreHatali = Hata.Dogrulama("mevcut_sifre_hatali", "Mevcut şifreniz hatalı.");
    public static readonly Hata SifirlamaKoduGecersiz = Hata.IsKurali("sifirlama_kodu_gecersiz", "Şifre sıfırlama bağlantısı geçersiz ya da süresi dolmuş. Lütfen yeni bağlantı isteyin.");
    public static readonly Hata DogrulamaKoduGecersiz = Hata.IsKurali("dogrulama_kodu_gecersiz", "Doğrulama bağlantısı geçersiz ya da süresi dolmuş. Lütfen yeni bağlantı isteyin.");
    public static readonly Hata EpostaDogrulanmadi = Hata.KimlikDogrulama("eposta_dogrulanmadi", "E-posta adresiniz henüz doğrulanmadı. Gelen kutunuzdaki bağlantıya tıklayın.");
    public static readonly Hata DavetGecersiz = Hata.IsKurali("davet_gecersiz", "Davet bağlantısı geçersiz, iptal edilmiş ya da süresi dolmuş. Firma yöneticinizden yeni davet isteyin.");
    public static readonly Hata DavetBulunamadi = Hata.Bulunamadi("davet_bulunamadi", "Davet bulunamadı.");
    public static readonly Hata DavetCokSik = Hata.IsKurali("davet_cok_sik", "Davet az önce gönderildi; birkaç dakika sonra yeniden deneyin.");
    public static readonly Hata HesapBilgisiGerekli = Hata.Dogrulama("hesap_bilgisi_gerekli", "Ad, soyad ve şifre girin.");
    public static readonly Hata KendiniPasiflestiremez = Hata.IsKurali("kendini_pasiflestiremez", "Kendi hesabınızı pasifleştiremezsiniz.");
    public static readonly Hata KullaniciBulunamadi = Hata.Bulunamadi("kullanici_bulunamadi", "Kullanıcı bulunamadı.");
    public static readonly Hata FirmaBulunamadi = Hata.Bulunamadi("firma_bulunamadi", "Firma bulunamadı.");
    public static readonly Hata PaketBulunamadi = Hata.Bulunamadi("paket_bulunamadi", "Paket bulunamadı.");
    public static readonly Hata PaketKoduKayitli = Hata.Cakisma("paket_kodu_kayitli", "Bu kodla bir paket zaten var.");
    public static readonly Hata PaketSatistaDegil = Hata.IsKurali("paket_satista_degil", "Bu paket satışta değil.");
    public static readonly Hata SureSatilmiyor = Hata.IsKurali("sure_satilmiyor", "Paketin bu süre için ücreti tanımlı değil.");
    public static readonly Hata BekleyenBildirimVar = Hata.Cakisma("bekleyen_bildirim_var", "Onay bekleyen bir ödeme bildiriminiz var. Sonuçlanınca yenisini gönderebilirsiniz.");
    public static readonly Hata BildirimBulunamadi = Hata.Bulunamadi("bildirim_bulunamadi", "Ödeme bildirimi bulunamadı.");
    public static readonly Hata BildirimSonuclanmis = Hata.IsKurali("bildirim_sonuclanmis", "Bu bildirim zaten sonuçlandırılmış.");
    public static readonly Hata OdemeBilgisiYok = Hata.IsKurali("odeme_bilgisi_yok", "Ödeme hesabı henüz tanımlanmadı. Lütfen bizimle iletişime geçin.");
    public static readonly Hata RolBulunamadi = Hata.Bulunamadi("rol_bulunamadi", "Rol bulunamadı.");
    public static readonly Hata RolAdiKayitli = Hata.Cakisma("rol_adi_kayitli", "Bu adla bir rol zaten var.");
    public static readonly Hata YoneticiRoluDegistirilemez = Hata.IsKurali("yonetici_rolu_degistirilemez", "Yönetici rolü değiştirilemez ve silinemez; tüm yetkileri kendiliğinden kapsar.");
    public static readonly Hata RolKullaniliyor = Hata.IsKurali("rol_kullaniliyor", "Bu role bağlı kullanıcı ya da bekleyen davet var; önce onları başka role alın.");
    public static readonly Hata YoneticiAtamaYetkisiYok = Hata.Yetki("yonetici_atama_yetkisi_yok", "Yönetici rolünü yalnızca bir yönetici verebilir ya da alabilir.");
    public static readonly Hata KendiYetkisiniDegistiremez = Hata.IsKurali("kendi_yetkisini_degistiremez", "Kendi rolünüzü ve yetkilerinizi değiştiremezsiniz.");
    public static readonly Hata MerkezKapatilamaz = Hata.IsKurali("merkez_kapatilamaz", "Merkez işyeri kapatılamaz; önce başka bir işyerini merkez yapın.");
    public static readonly Hata IsyeriKoduKayitli = Hata.Cakisma("isyeri_kodu_kayitli", "Bu kodla bir işyeri zaten var.");
    public static Hata IsyeriLimiti(int limit) =>
        Hata.IsKurali("isyeri_limiti", $"Paketinizin işyeri limiti ({limit}) dolu. Bir işyerini kapatın ya da paketinizi yükseltin.");
    public static readonly Hata SonYonetici = Hata.IsKurali("son_yonetici", "Firmada en az bir aktif yönetici kalmalı.");
    public static Hata MaliMusavirYetkisi(string yetki) =>
        Hata.Dogrulama("mali_musavir_yetkisi", $"Mali müşavir rolü yalnız görüntüleme ve muhasebe aktarımı yetkisi taşıyabilir (\"{yetki}\").");
    public static Hata YetkiGecersiz(string yetki) => Hata.Dogrulama("yetki_gecersiz", $"\"{yetki}\" verilebilecek bir yetki değil.");
    public static Hata KullaniciLimiti(int limit) =>
        Hata.IsKurali("kullanici_limiti", $"Paketinizin kullanıcı limiti ({limit}) dolu. Bir kullanıcıyı pasifleştirin ya da paketinizi yükseltin.");
}
