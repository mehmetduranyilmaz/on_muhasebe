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
    AlinanAvanslar,
    VerilenAvanslar,
    DigerGelirler,
    GenelYonetimGiderleri
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
        HesapRolu.AlinanAvanslar => "340",
        HesapRolu.VerilenAvanslar => "159",
        HesapRolu.DigerGelirler => "649",
        HesapRolu.GenelYonetimGiderleri => "770",
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
        HesapRolu.AlinanAvanslar => "Alınan sipariş avansları",
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
