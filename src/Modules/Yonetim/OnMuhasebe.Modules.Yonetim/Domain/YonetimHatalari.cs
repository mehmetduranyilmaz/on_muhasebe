using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Yonetim.Domain;

internal static class YonetimHatalari
{
    public static readonly Hata GirisBasarisiz = Hata.KimlikDogrulama("giris_basarisiz", "E-posta veya şifre hatalı.");
    public static readonly Hata HesapKilitli = Hata.KimlikDogrulama("hesap_kilitli", "Çok sayıda hatalı giriş yapıldı. Lütfen 15 dakika sonra tekrar deneyin.");
    public static readonly Hata FirmaYok = Hata.KimlikDogrulama("firma_yok", "Hesabınız aktif bir firmaya bağlı değil.");
    public static readonly Hata OturumGecersiz = Hata.KimlikDogrulama("oturum_gecersiz", "Oturumunuzun süresi doldu. Lütfen tekrar giriş yapın.");
    public static readonly Hata FirmaUyesiDegil = Hata.Yetki("firma_uyesi_degil", "Bu firmaya erişiminiz yok.");
    public static readonly Hata EpostaKayitli = Hata.Cakisma("eposta_kayitli", "Bu e-posta adresiyle zaten bir hesap var.");
    public static readonly Hata ZatenFirmaUyesi = Hata.Cakisma("zaten_firma_uyesi", "Bu kullanıcı zaten firmanıza bağlı.");
    public static readonly Hata SifreGerekli = Hata.Dogrulama("sifre_gerekli", "Yeni kullanıcı için şifre belirlenmelidir.");
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
    public static Hata KullaniciLimiti(int limit) =>
        Hata.IsKurali("kullanici_limiti", $"Paketinizin kullanıcı limiti ({limit}) dolu. Bir kullanıcıyı pasifleştirin ya da paketinizi yükseltin.");
}
