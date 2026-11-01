using System.Text.Json.Serialization;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Moduller;

namespace OnMuhasebe.Modules.Muhasebe.Contracts;

public static class MuhasebeYetkileri
{
    private const string Baslik = "Muhasebe Aktarımı";

    // Görüntüle: fiş önizleme ve dosya indirme. Ekle: dönemi "aktarıldı" işaretleme (mali müşavir de yapabilir).
    public static readonly YetkiKaynagi Aktarim = new("muhasebe.aktarim", ModulKodlari.Muhasebe, Baslik, "Muhasebe fişleri ve aktarım",
        [YetkiIslemleri.Goruntule, YetkiIslemleri.Ekle], ["cari.hesap.goruntule"]);

    public static readonly YetkiKaynagi Plan = new("muhasebe.plan", ModulKodlari.Muhasebe, Baslik, "Hesap planı eşleştirmesi",
        [YetkiIslemleri.Goruntule, YetkiIslemleri.Duzenle], []);

    public static IReadOnlyList<YetkiKaynagi> Tumu { get; } = [Aktarim, Plan];
}

// Belgelerin fişe çevrilirken kullandığı ana hesaplar (Tek Düzen Hesap Planı). Kodlar firma ayarıdır; varsayılanlar THDP'dir.
[JsonConverter(typeof(JsonStringEnumConverter<HesapRolu>))]
public enum HesapRolu
{
    Alicilar,
    Saticilar,
    YurticiSatislar,
    SatistanIadeler,
    TicariMallar,
    HizmetAlimGiderleri,
    HesaplananKdv,
    IndirilecekKdv,
    PesinOdenenVergiler,
    OdenecekVergiler,
    Kasa,
    Bankalar,
    KrediKartlari,
    PosAlacaklari,
    AlinanCekler,
    VerilenCekler,
    AlacakSenetleri,
    BorcSenetleri,
    BankaKredileri,
    FinansmanGiderleri,
    KambiyoKarlari,
    KambiyoZararlari,
    AlinanAvanslar,
    VerilenAvanslar,
    DigerGelirler,
    GenelYonetimGiderleri,
    PersonelBorclari,
    PersonelAvanslari
}

public static class HesapRolleri
{
    public static IReadOnlyList<HesapRolu> Tumu { get; } = Enum.GetValues<HesapRolu>();

    public static string Varsayilan(HesapRolu rol) => rol switch
    {
        HesapRolu.Alicilar => "120",
        HesapRolu.Saticilar => "320",
        HesapRolu.YurticiSatislar => "600",
        HesapRolu.SatistanIadeler => "610",
        HesapRolu.TicariMallar => "153",
        HesapRolu.HizmetAlimGiderleri => "770",
        HesapRolu.HesaplananKdv => "391",
        HesapRolu.IndirilecekKdv => "191",
        HesapRolu.PesinOdenenVergiler => "193",
        HesapRolu.OdenecekVergiler => "360",
        HesapRolu.Kasa => "100",
        HesapRolu.Bankalar => "102",
        HesapRolu.KrediKartlari => "309",
        HesapRolu.PosAlacaklari => "108",
        HesapRolu.AlinanCekler => "101",
        HesapRolu.VerilenCekler => "103",
        HesapRolu.AlacakSenetleri => "121",
        HesapRolu.BorcSenetleri => "321",
        HesapRolu.BankaKredileri => "300",
        HesapRolu.FinansmanGiderleri => "780",
        HesapRolu.KambiyoKarlari => "646",
        HesapRolu.KambiyoZararlari => "656",
        HesapRolu.AlinanAvanslar => "340",
        HesapRolu.VerilenAvanslar => "159",
        HesapRolu.DigerGelirler => "649",
        HesapRolu.GenelYonetimGiderleri => "770",
        HesapRolu.PersonelBorclari => "335",
        HesapRolu.PersonelAvanslari => "196",
        _ => throw new ArgumentOutOfRangeException(nameof(rol))
    };

    public static string Ad(HesapRolu rol) => rol switch
    {
        HesapRolu.Alicilar => "Alıcılar (müşteriler)",
        HesapRolu.Saticilar => "Satıcılar (tedarikçiler)",
        HesapRolu.YurticiSatislar => "Yurt içi satışlar",
        HesapRolu.SatistanIadeler => "Satıştan iadeler",
        HesapRolu.TicariMallar => "Ticari mallar (stoklu alış)",
        HesapRolu.HizmetAlimGiderleri => "Hizmet alımı / stoksuz alış gideri",
        HesapRolu.HesaplananKdv => "Hesaplanan KDV",
        HesapRolu.IndirilecekKdv => "İndirilecek KDV",
        HesapRolu.PesinOdenenVergiler => "Peşin ödenen vergiler (satışta kesilen stopaj)",
        HesapRolu.OdenecekVergiler => "Ödenecek vergi ve fonlar (tevkifat, stopaj)",
        HesapRolu.Kasa => "Kasa",
        HesapRolu.Bankalar => "Bankalar",
        HesapRolu.KrediKartlari => "Kredi kartı borçları",
        HesapRolu.PosAlacaklari => "POS (diğer hazır değerler)",
        HesapRolu.AlinanCekler => "Alınan çekler",
        HesapRolu.VerilenCekler => "Verilen çekler ve ödeme emirleri",
        HesapRolu.AlacakSenetleri => "Alacak senetleri",
        HesapRolu.BorcSenetleri => "Borç senetleri",
        HesapRolu.BankaKredileri => "Banka kredileri",
        HesapRolu.FinansmanGiderleri => "Finansman giderleri (kredi faizi)",
        HesapRolu.KambiyoKarlari => "Kambiyo kârları (dövizli virman kur farkı)",
        HesapRolu.KambiyoZararlari => "Kambiyo zararları (dövizli virman kur farkı)",
        HesapRolu.AlinanAvanslar => "Alınan sipariş avansları",
        HesapRolu.PersonelBorclari => "Personele borçlar",
        HesapRolu.PersonelAvanslari => "Personel avansları",
        HesapRolu.VerilenAvanslar => "Verilen sipariş avansları",
        HesapRolu.DigerGelirler => "Diğer gelirler (gelir kategorisi varsayılanı)",
        HesapRolu.GenelYonetimGiderleri => "Genel yönetim giderleri (gider kategorisi varsayılanı)",
        _ => rol.ToString()
    };
}

// Kartına özel hesap kodu verilebilen kayıtlar. Verilmezse otomatik kod kullanılır.
[JsonConverter(typeof(JsonStringEnumConverter<KartTuru>))]
public enum KartTuru
{
    Cari,
    KasaHesabi,
    Kategori
}

public sealed record HesapPlaniSatiri(HesapRolu Rol, string Ad, string Kod, string VarsayilanKod);

// Ayirac: alt hesap ayıracı ("." -> 120.01.00001). CariAltGrup: cari alt hesaplarının ara düzeyi (boşsa 120.00001).
public sealed record HesapPlaniCevabi(string Ayirac, string? CariAltGrup, IReadOnlyList<HesapPlaniSatiri> Hesaplar);

public sealed record HesapPlaniIstegi(string Ayirac, string? CariAltGrup, IReadOnlyDictionary<HesapRolu, string> Kodlar);

// Kartın otomatik kodu ve (varsa) elle verilen kodu; Kod her zaman kullanılacak koddur.
public sealed record KartEslemesi(Guid KartId, string KartKodu, string Ad, string Tur, string OtomatikKod, string? OzelKod, string Kod);

public sealed record KartEslemeIstegi(string? Kod);

// Tahsil/tediye fişi yalnız kasa (nakit) hareketidir; banka, kredi kartı ve diğer tüm kayıtlar mahsup fişidir.
[JsonConverter(typeof(JsonStringEnumConverter<FisTuru>))]
public enum FisTuru
{
    Mahsup,
    Tahsil,
    Tediye
}

public sealed record FisSatiri(string HesapKodu, string HesapAdi, decimal Borc, decimal Alacak, string? Aciklama);

public static class FisKaynaklari
{
    public const string Fatura = "Fatura";
    public const string KasaBanka = "KasaBanka";
    public const string CekSenet = "CekSenet";
    public const string Tahakkuk = "Tahakkuk";
    public const string Personel = "Personel";
}

// Değişti: aktarıldıktan sonra tarih, belge no ya da borç/alacak satırları (hesap kodu dahil) değişmiş; yeniden aktarılmalı.
[JsonConverter(typeof(JsonStringEnumConverter<FisAktarimDurumu>))]
public enum FisAktarimDurumu
{
    Aktarilmadi,
    Aktarildi,
    Degisti
}

// Kaynak: FisKaynaklari; BelgeTuru kaynak belgenin türüdür (Satis, Tahsilat...). Dengesiz fiş aktarılmış sayılamaz, uyarıyla döner.
public sealed record MuhasebeFisi(
    Guid KaynakId, string Kaynak, string BelgeTuru, FisTuru Tur, DateOnly Tarih, string? BelgeNo, string Aciklama, Guid? IsyeriId,
    IReadOnlyList<FisSatiri> Satirlar, FisAktarimDurumu AktarimDurumu = FisAktarimDurumu.Aktarilmadi)
{
    public decimal ToplamBorc => Satirlar.Sum(s => s.Borc);

    public decimal ToplamAlacak => Satirlar.Sum(s => s.Alacak);

    public bool DengeliMi => ToplamBorc == ToplamAlacak;
}

// Aktarıldıktan sonra iptal edilen ya da dönemden çıkan belgenin fişi: muhasebe programında elle düzeltilmelidir.
public sealed record KalkanFis(Guid KaynakId, string Kaynak, string BelgeTuru, DateOnly Tarih, string? BelgeNo);

// Parmak: listenin içerik özeti. Aktarım bununla işaretlenir: indirilen dosyadan sonra değişen liste aktarılmış sayılmaz.
public sealed record FisListesi(
    DateOnly Baslangic, DateOnly Bitis, IReadOnlyList<MuhasebeFisi> Fisler, IReadOnlyList<string> Uyarilar, string Parmak,
    IReadOnlyList<KalkanFis> Kalkanlar);

public sealed record AktarimIstegi(DateOnly Baslangic, DateOnly Bitis, Guid? IsyeriId, string Parmak, string? Aciklama);

public sealed record AktarimCevabi(
    Guid Id, DateOnly Baslangic, DateOnly Bitis, Guid? IsyeriId, int FisSayisi, decimal ToplamBorc, string? Aciklama, string? AktaranAdi,
    DateTime AktarimTarihi, DateTime? IptalTarihi);
