using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Cari.Domain;

internal static class CariHatalari
{
    public static readonly Hata CariBulunamadi = Hata.Bulunamadi("cari_bulunamadi", "Cari hesap bulunamadı.");
    public static readonly Hata HareketBulunamadi = Hata.Bulunamadi("hareket_bulunamadi", "Cari hareket bulunamadı.");
    public static readonly Hata GrupBulunamadi = Hata.Bulunamadi("grup_bulunamadi", "Cari grubu bulunamadı.");
    public static readonly Hata KodKullaniliyor = Hata.Cakisma("kod_kullaniliyor", "Bu cari kodu başka bir caride kullanılıyor.");
    public static readonly Hata GrupAdiKullaniliyor = Hata.Cakisma("grup_adi_kullaniliyor", "Bu isimde bir grup zaten var.");
    public static readonly Hata ParaBirimiDegistirilemez = Hata.IsKurali("para_birimi_degistirilemez", "Hareketi olan carinin para birimi değiştirilemez.");
    public static readonly Hata HareketliCariSilinemez = Hata.IsKurali("hareketli_cari_silinemez", "Hareketi olan cari silinemez; pasifleştirebilirsiniz.");
    public static readonly Hata GrupKullaniliyor = Hata.IsKurali("grup_kullaniliyor", "Bu gruba bağlı cariler var; önce carilerin grubunu değiştirin.");
    public static readonly Hata PasifCariyeHareket = Hata.IsKurali("pasif_cari", "Pasif cariye hareket girilemez.");
    public static readonly Hata TutarPozitifOlmali = Hata.Dogrulama("tutar_gecersiz", "Tutar sıfırdan büyük olmalıdır.");
    public static readonly Hata KurPozitifOlmali = Hata.Dogrulama("kur_gecersiz", "Kur sıfırdan büyük olmalıdır.");
    public static readonly Hata KurGerekli = Hata.Dogrulama("kur_gerekli", "Dövizli cari için kur girilmelidir.");
    public static readonly Hata TlKuruBirOlmali = Hata.Dogrulama("tl_kuru", "TL cari hareketinde kur 1 olmalıdır.");
    public static readonly Hata VadeTarihtenOnceOlamaz = Hata.Dogrulama("vade_gecersiz", "Vade tarihi işlem tarihinden önce olamaz.");
    public static readonly Hata AcilisZatenVar = Hata.IsKurali("acilis_zaten_var", "Bu carinin açılış bakiyesi zaten girilmiş; önce mevcut açılışı iptal edin.");
    public static readonly Hata ElleIptalEdilemez = Hata.IsKurali("kaynak_belgeden_iptal", "Bu hareket kaynak belgesinden (fatura, tahsilat vb.) iptal edilmelidir.");
    public static readonly Hata ZatenIptalEdilmis = Hata.IsKurali("zaten_iptal", "Bu hareket zaten iptal edilmiş.");
    public static readonly Hata IptalKaydiIptalEdilemez = Hata.IsKurali("iptal_kaydi", "İptal kaydı iptal edilemez.");
    public static readonly Hata IptalliKalemKapanmaz = Hata.IsKurali("iptalli_kalem", "İptal edilmiş hareket kapamaya katılmaz.");
    public static readonly Hata MahsupHedefiYok = Hata.IsKurali("mahsup_hedefi_yok", "Mahsup edilecek açık kalem bulunamadı (belge kapanmış ya da iptal edilmiş olabilir).");
    public static readonly Hata AcikAvansYok = Hata.IsKurali("acik_avans_yok", "Bu carinin bu belgeye mahsup edilebilecek açık avansı yok.");
    public static readonly Hata KapamaTutariGecersiz = Hata.Dogrulama("kapama_gecersiz", "Kapama satırları geçersiz: karşı kalem aynı caride, ters yönde olmalı ve tutar açık kısmı aşmamalıdır.");
}
