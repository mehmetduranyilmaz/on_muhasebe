using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Fatura.Domain;

internal static class FaturaHatalari
{
    public static readonly Hata FaturaBulunamadi = Hata.Bulunamadi("fatura_bulunamadi", "Fatura bulunamadı.");
    public static readonly Hata CariBulunamadi = Hata.Bulunamadi("cari_bulunamadi", "Cari hesap bulunamadı.");
    public static readonly Hata StokKartiBulunamadi = Hata.Bulunamadi("stok_karti_bulunamadi", "Faturadaki stok kartlarından biri bulunamadı.");
    public static readonly Hata DepoBulunamadi = Hata.Bulunamadi("depo_bulunamadi", "Depo bulunamadı veya pasif.");
    public static readonly Hata PasifCari = Hata.IsKurali("pasif_cari", "Pasif cariye fatura kesilemez.");
    public static readonly Hata CariVeyaNihaiTuketici = Hata.Dogrulama("cari_gerekli", "Cari seçilmelidir (yalnızca perakende faturada nihai tüketici girilebilir).");
    public static readonly Hata SatirGerekli = Hata.Dogrulama("satir_gerekli", "Faturada en az bir satır olmalıdır.");
    public static readonly Hata SatirAdiGerekli = Hata.Dogrulama("satir_adi_gerekli", "Stok kartı seçilmeyen satırda ürün/hizmet adı girilmelidir.");
    public static readonly Hata BirimTanimsiz = Hata.Dogrulama("birim_tanimsiz", "Satırdaki birim stok kartında tanımlı değil.");
    public static readonly Hata PasifStokKarti = Hata.IsKurali("pasif_kart", "Pasif stok kartı faturaya eklenemez.");
    public static readonly Hata KurGerekli = Hata.Dogrulama("kur_gerekli", "Bu tarih için TCMB kuru bulunamadı; kuru elle girin.");
    public static readonly Hata VadeTarihtenOnceOlamaz = Hata.Dogrulama("vade_gecersiz", "Vade tarihi fatura tarihinden önce olamaz.");
    public static readonly Hata GenelIskontoFazla = Hata.Dogrulama("genel_iskonto_fazla", "Genel iskonto, iskontolu satır toplamını aşamaz.");
    public static readonly Hata NumaraGerekli = Hata.Dogrulama("numara_gerekli", "Gelen faturanın (alış, satıştan iade) numarası girilmelidir.");
    public static readonly Hata NumaraKullaniliyor = Hata.Cakisma("numara_kullaniliyor", "Bu cariye ait aynı numaralı bir fatura zaten kayıtlı.");
    public static readonly Hata TurDegistirilemez = Hata.IsKurali("tur_degistirilemez", "Faturanın türü değiştirilemez.");
    public static readonly Hata IptalEdilmis = Hata.IsKurali("fatura_iptal", "İptal edilmiş fatura üzerinde işlem yapılamaz.");
    public static readonly Hata OnayliDuzenlenemez = Hata.IsKurali("onayli_duzenlenemez", "Onaylı fatura düzenlenemez; iptal edip yeniden kesebilirsiniz.");
    public static readonly Hata ZatenOnayli = Hata.IsKurali("zaten_onayli", "Fatura zaten onaylı.");
    public static readonly Hata TaslakIptalEdilemez = Hata.IsKurali("taslak_iptal", "Taslak fatura iptal edilmez; silebilirsiniz.");
    public static readonly Hata YalnizcaTaslakSilinir = Hata.IsKurali("yalnizca_taslak_silinir", "Yalnızca taslak fatura silinebilir; onaylı faturayı iptal edin.");
    public static readonly Hata CariLisansiYok = Hata.Yetki("cari_lisansi_yok", "Cariye fatura kesmek için Cari modülü lisansı gerekir.");
    public static readonly Hata StokLisansiYok = Hata.Yetki("stok_lisansi_yok", "Stok kartlı satır için Stok modülü lisansı gerekir; serbest satır kullanabilirsiniz.");
}
