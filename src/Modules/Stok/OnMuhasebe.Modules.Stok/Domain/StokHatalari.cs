using System.Globalization;
using OnMuhasebe.Shared.Contracts.Birimler;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Stok.Domain;

internal static class StokHatalari
{
    public static readonly Hata KartBulunamadi = Hata.Bulunamadi("stok_karti_bulunamadi", "Stok kartı bulunamadı.");
    public static readonly Hata HareketBulunamadi = Hata.Bulunamadi("stok_hareketi_bulunamadi", "Stok hareketi bulunamadı.");
    public static readonly Hata DepoBulunamadi = Hata.Bulunamadi("depo_bulunamadi", "Depo bulunamadı.");
    public static readonly Hata GrupBulunamadi = Hata.Bulunamadi("grup_bulunamadi", "Stok grubu bulunamadı.");
    public static readonly Hata BarkodBulunamadi = Hata.Bulunamadi("barkod_bulunamadi", "Bu barkoda ait stok kartı yok.");
    public static readonly Hata KodKullaniliyor = Hata.Cakisma("kod_kullaniliyor", "Bu stok kodu başka bir kartta kullanılıyor.");
    public static readonly Hata BarkodKullaniliyor = Hata.Cakisma("barkod_kullaniliyor", "Bu barkod başka bir kartta veya birimde kullanılıyor.");
    public static readonly Hata AdKullaniliyor = Hata.Cakisma("ad_kullaniliyor", "Bu isim zaten kullanılıyor.");
    public static readonly Hata TemelBirimDegistirilemez = Hata.IsKurali("temel_birim_degistirilemez", "Hareketi olan kartın temel birimi değiştirilemez.");
    public static readonly Hata TurDegistirilemez = Hata.IsKurali("tur_degistirilemez", "Hareketi olan kartın türü (ürün/hizmet) değiştirilemez.");
    public static readonly Hata EkBirimGecersiz = Hata.Dogrulama("ek_birim_gecersiz", "Ek birimler geçerli, temel birimden farklı, tekrarsız ve katsayısı sıfırdan büyük olmalıdır.");
    public static readonly Hata HareketliKartSilinemez = Hata.IsKurali("hareketli_kart_silinemez", "Hareketi olan stok kartı silinemez; pasifleştirebilirsiniz.");
    public static readonly Hata HareketliDepoSilinemez = Hata.IsKurali("hareketli_depo_silinemez", "Hareketi olan depo silinemez.");
    public static readonly Hata VarsayilanDepoSilinemez = Hata.IsKurali("varsayilan_depo_silinemez", "Varsayılan depo silinemez; önce başka bir depoyu varsayılan yapın.");
    public static readonly Hata GrupKullaniliyor = Hata.IsKurali("grup_kullaniliyor", "Bu gruba bağlı stok kartları var.");
    public static readonly Hata HizmetKartinaHareket = Hata.IsKurali("hizmet_karti", "Hizmet kartları stok hareketi oluşturmaz.");
    public static readonly Hata PasifKartaHareket = Hata.IsKurali("pasif_kart", "Pasif stok kartına hareket girilemez.");
    public static readonly Hata BirimTanimsiz = Hata.Dogrulama("birim_tanimsiz", "Bu birim stok kartında tanımlı değil.");
    public static readonly Hata MiktarPozitifOlmali = Hata.Dogrulama("miktar_gecersiz", "Miktar sıfırdan büyük olmalıdır.");
    public static readonly Hata KesirliMiktar = Hata.Dogrulama("kesirli_miktar", "Bu birimde kesirli miktar girilemez.");
    public static readonly Hata MaliyetGerekli = Hata.Dogrulama("maliyet_gerekli", "Giriş hareketinde birim maliyet (TL) girilmelidir.");
    public static readonly Hata AyniDepo = Hata.Dogrulama("ayni_depo", "Kaynak ve hedef depo farklı olmalıdır.");
    public static readonly Hata ElleIptalEdilemez = Hata.IsKurali("kaynak_belgeden_iptal", "Bu hareket kaynak belgesinden (fatura vb.) iptal edilmelidir.");
    public static readonly Hata ZatenIptalEdilmis = Hata.IsKurali("zaten_iptal", "Bu hareket zaten iptal edilmiş.");
    public static readonly Hata IptalKaydiIptalEdilemez = Hata.IsKurali("iptal_kaydi", "İptal kaydı iptal edilemez.");

    private static readonly CultureInfo _tr = CultureInfo.GetCultureInfo("tr-TR");

    public static Hata YetersizStok(decimal mevcut, string birim) =>
        Hata.IsKurali("yetersiz_stok", string.Create(_tr, $"Depoda yeterli stok yok (mevcut: {mevcut:#,0.####} {Birimler.Ad(birim)})."));

    public static string EksiStokUyarisi(decimal sonMiktar, string birim) =>
        string.Create(_tr, $"Dikkat: bu işlemden sonra depodaki stok eksiye düştü ({sonMiktar:#,0.####} {Birimler.Ad(birim)}).");
}
