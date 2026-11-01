using System.Text.Json.Serialization;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Moduller;

namespace OnMuhasebe.Modules.Arac.Contracts;

public static class AracYetkileri
{
    private const string Baslik = "Araç Takibi";

    public static readonly YetkiKaynagi Kart = new("arac.kart", ModulKodlari.Arac, Baslik, "Araç kartları", YetkiIslemleri.Temel, []);

    public static readonly YetkiKaynagi Islem = new("arac.islem", ModulKodlari.Arac, Baslik, "Sigorta, muayene, MTV, masraf ve hasar kayıtları",
        YetkiIslemleri.Temel, ["arac.kart.goruntule"]);

    public static readonly YetkiKaynagi Rapor = new("arac.rapor", ModulKodlari.Arac, Baslik, "Araç maliyet raporu", YetkiIslemleri.SadeceGoruntule, []);

    public static IReadOnlyList<YetkiKaynagi> Tumu { get; } = [Kart, Islem, Rapor];
}

[JsonConverter(typeof(JsonStringEnumConverter<AracTuru>))]
public enum AracTuru { Binek, HafifTicari, Kamyon, Otobus, Motosiklet, IsMakinesi, Diger }

[JsonConverter(typeof(JsonStringEnumConverter<YakitTuru>))]
public enum YakitTuru { Benzin, Dizel, Lpg, Elektrik, Hibrit, Diger }

[JsonConverter(typeof(JsonStringEnumConverter<AracMulkiyeti>))]
public enum AracMulkiyeti { OzMal, Kiralik, Leasing }

[JsonConverter(typeof(JsonStringEnumConverter<AracDurumu>))]
public enum AracDurumu { Aktif, Satildi, Hurda }

[JsonConverter(typeof(JsonStringEnumConverter<YukumlulukTuru>))]
public enum YukumlulukTuru { TrafikSigortasi, Kasko, Muayene, EgzozOlcumu, Diger }

[JsonConverter(typeof(JsonStringEnumConverter<AracMasrafTuru>))]
public enum AracMasrafTuru { Yakit, Bakim, Lastik, Otopark, Hgs, Ceza, Diger }

[JsonConverter(typeof(JsonStringEnumConverter<HasarKusuru>))]
public enum HasarKusuru { Belirsiz, Kusursuz, Kismen, Kusurlu }

[JsonConverter(typeof(JsonStringEnumConverter<SureDurumu>))]
public enum SureDurumu { Gecerli, Yaklasiyor, SuresiDoldu }

public static class AracMetinleri
{
    public static string Tur(AracTuru t) => t switch
    {
        AracTuru.Binek => "Binek",
        AracTuru.HafifTicari => "Hafif ticari",
        AracTuru.Kamyon => "Kamyon / çekici",
        AracTuru.Otobus => "Otobüs / minibüs",
        AracTuru.Motosiklet => "Motosiklet",
        AracTuru.IsMakinesi => "İş makinesi",
        _ => "Diğer"
    };

    public static string Yakit(YakitTuru t) => t switch
    {
        YakitTuru.Lpg => "LPG",
        YakitTuru.Elektrik => "Elektrik",
        YakitTuru.Hibrit => "Hibrit",
        YakitTuru.Dizel => "Dizel",
        YakitTuru.Benzin => "Benzin",
        _ => "Diğer"
    };

    public static string Mulkiyet(AracMulkiyeti m) => m switch
    {
        AracMulkiyeti.Kiralik => "Kiralık",
        AracMulkiyeti.Leasing => "Leasing",
        _ => "Öz mal"
    };

    public static string Durum(AracDurumu d) => d switch
    {
        AracDurumu.Satildi => "Satıldı",
        AracDurumu.Hurda => "Hurda",
        _ => "Aktif"
    };

    public static string Yukumluluk(YukumlulukTuru t) => t switch
    {
        YukumlulukTuru.TrafikSigortasi => "Trafik sigortası",
        YukumlulukTuru.Kasko => "Kasko",
        YukumlulukTuru.Muayene => "Muayene",
        YukumlulukTuru.EgzozOlcumu => "Egzoz ölçümü",
        _ => "Diğer"
    };

    public static string Masraf(AracMasrafTuru t) => t switch
    {
        AracMasrafTuru.Yakit => "Yakıt",
        AracMasrafTuru.Bakim => "Bakım / onarım",
        AracMasrafTuru.Lastik => "Lastik",
        AracMasrafTuru.Otopark => "Otopark",
        AracMasrafTuru.Hgs => "HGS / OGS",
        AracMasrafTuru.Ceza => "Trafik cezası",
        _ => "Diğer"
    };

    public static string Kusur(HasarKusuru k) => k switch
    {
        HasarKusuru.Kusursuz => "Kusursuz",
        HasarKusuru.Kismen => "Kısmen kusurlu",
        HasarKusuru.Kusurlu => "Kusurlu",
        _ => "Belirsiz"
    };

    public static string Sure(SureDurumu d) => d switch
    {
        SureDurumu.Yaklasiyor => "Yaklaşıyor",
        SureDurumu.SuresiDoldu => "Süresi doldu",
        _ => "Geçerli"
    };
}

// İş makinesinde plaka yerine tescil no girilebilir. IsyeriId boşsa firma genelidir.
public sealed record AracKaydetIstegi(
    string Plaka, string Marka, string Model, int? ModelYili, AracTuru Tur, YakitTuru Yakit, string? SasiNo, string? MotorNo, string? RuhsatSahibi,
    AracMulkiyeti Mulkiyet, Guid? IsyeriId, string? Surucu, string? SurucuTelefon, int? GuncelKm, AracDurumu Durum, Guid? DemirbasId, Guid? MasrafMerkeziId,
    string? Aciklama, uint Surum = 0);

public sealed record AracOzeti(
    Guid Id, string Plaka, string Marka, string Model, int? ModelYili, AracTuru Tur, AracDurumu Durum, string? Surucu, int? GuncelKm, Guid? IsyeriId,
    string? EnYakinYukumluluk, DateOnly? EnYakinBitis, SureDurumu? EnYakinDurum);

public sealed record AracDetayi(
    Guid Id, string Plaka, string Marka, string Model, int? ModelYili, AracTuru Tur, YakitTuru Yakit, string? SasiNo, string? MotorNo, string? RuhsatSahibi,
    AracMulkiyeti Mulkiyet, Guid? IsyeriId, string? Surucu, string? SurucuTelefon, int? GuncelKm, AracDurumu Durum, Guid? DemirbasId, Guid? MasrafMerkeziId,
    string? Aciklama, uint Surum);

public sealed record YukumlulukKaydetIstegi(
    YukumlulukTuru Tur, string? Ad, DateOnly? BaslangicTarihi, DateOnly BitisTarihi, string? BelgeNo, string? Sirket, decimal? Tutar, int HatirlatmaGun);

// Yenileme eski kaydı geçmişe alır, yeni dönem için kayıt açar.
public sealed record YukumlulukCevabi(
    Guid Id, Guid AracId, YukumlulukTuru Tur, string? Ad, DateOnly? BaslangicTarihi, DateOnly BitisTarihi, string? BelgeNo, string? Sirket, decimal? Tutar,
    int HatirlatmaGun, bool YenilendiMi, int KalanGun, SureDurumu Durum);

public sealed record YaklasanYukumluluk(
    Guid Id, Guid AracId, string Plaka, YukumlulukTuru Tur, string? Ad, DateOnly BitisTarihi, int KalanGun, SureDurumu Durum);

// Taksit: 1 Ocak, 2 Temmuz. Son ödeme tarihi boşsa dönemin son günüdür.
public sealed record MtvKaydetIstegi(int Yil, int Taksit, decimal Tutar, DateOnly? SonOdemeTarihi);

public sealed record MtvOdemeIstegi(DateOnly OdemeTarihi);

public sealed record MtvCevabi(Guid Id, Guid AracId, int Yil, int Taksit, decimal Tutar, DateOnly SonOdemeTarihi, DateOnly? OdemeTarihi);

// Takip kaydıdır, muhasebe kaydı üretmez. Gider muhasebeye faturayla girer; FaturaId o faturayı gösterir. Tutar KDV dahil TL.
public sealed record MasrafKaydetIstegi(
    AracMasrafTuru Tur, DateOnly Tarih, decimal Tutar, int? Km, decimal? Litre, string? Tedarikci, string? BelgeNo, Guid? FaturaId, string? Aciklama);

public sealed record MasrafCevabi(
    Guid Id, Guid AracId, AracMasrafTuru Tur, DateOnly Tarih, decimal Tutar, int? Km, decimal? Litre, string? Tedarikci, string? BelgeNo, Guid? FaturaId, string? Aciklama);

public sealed record HasarKaydetIstegi(
    DateOnly Tarih, HasarKusuru Kusur, string? Aciklama, decimal OnarimTutari, decimal SigortaOdemesi, string? DosyaNo);

public sealed record HasarCevabi(
    Guid Id, Guid AracId, DateOnly Tarih, HasarKusuru Kusur, string? Aciklama, decimal OnarimTutari, decimal SigortaOdemesi, string? DosyaNo);

// Dönem maliyeti: takip kayıtlarından. KmBasinaMaliyet ve Tuketim (L/100 km) yeterli km kaydı yoksa boştur.
public sealed record AracMaliyeti(
    Guid AracId, string Plaka, decimal Masraf, decimal Yakit, decimal Sigorta, decimal Mtv, decimal HasarNet, decimal Toplam, int? Km, decimal? KmBasinaMaliyet, decimal? Tuketim);
