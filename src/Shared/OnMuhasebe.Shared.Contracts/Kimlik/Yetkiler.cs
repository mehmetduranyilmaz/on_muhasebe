namespace OnMuhasebe.Shared.Contracts.Kimlik;

public static class YetkiIslemleri
{
    public const string Goruntule = "goruntule";
    public const string Ekle = "ekle";
    public const string Duzenle = "duzenle";
    public const string Sil = "sil";
    public const string Onayla = "onayla";
    public const string Iptal = "iptal";

    public static IReadOnlyList<string> Temel { get; } = [Goruntule, Ekle, Duzenle, Sil];
    public static IReadOnlyList<string> Belge { get; } = [Goruntule, Ekle, Duzenle, Sil, Onayla, Iptal];
    public static IReadOnlyList<string> Hareket { get; } = [Goruntule, Ekle, Iptal];
    public static IReadOnlyList<string> SadeceGoruntule { get; } = [Goruntule];

    public static string Ad(string islem) => islem switch
    {
        Goruntule => "Görüntüle",
        Ekle => "Ekle",
        Duzenle => "Düzenle",
        Sil => "Sil",
        Onayla => "Onayla",
        Iptal => "İptal",
        _ => islem
    };
}

// Yetki verilebilen ekran ya da belge. Yetki kodu "kaynak.islem" biçimindedir (ör. fatura.belge.onayla).
// Modul lisans kodudur; boşsa çekirdektir (her pakette). Lisansı olmayan modülün kaynakları rol ekranında listelenmez.
// Gerekenler: kaynakta herhangi bir yetkisi olana kendiliğinden verilen yetkiler (ör. fatura kesen cari ve stok listesini görmeli).
public sealed record YetkiKaynagi(string Kod, string Modul, string Grup, string Ad, IReadOnlyList<string> Islemler, IReadOnlyList<string> Gerekenler)
{
    public string Yetki(string islem) => $"{Kod}.{islem}";

    public IEnumerable<string> Yetkiler() => Islemler.Select(Yetki);
}

// Çekirdek (Yönetim) yetkileri. Modül ayar ekranları da "Firma ayarları" yetkisine bağlıdır.
public static class YonetimYetkileri
{
    private const string Baslik = "Yönetim";

    public static readonly YetkiKaynagi Kullanici = new("yonetim.kullanici", "", Baslik, "Kullanıcılar ve roller",
        YetkiIslemleri.Temel, []);

    public static readonly YetkiKaynagi Firma = new("yonetim.firma", "", Baslik, "Firma bilgileri ve ayarlar",
        [YetkiIslemleri.Duzenle], []);

    public static readonly YetkiKaynagi Abonelik = new("yonetim.abonelik", "", Baslik, "Abonelik ve ödeme bildirimi",
        [YetkiIslemleri.Goruntule, YetkiIslemleri.Ekle], []);

    public static readonly YetkiKaynagi Degisiklik = new("yonetim.degisiklik", "", Baslik, "Değişiklik geçmişi",
        YetkiIslemleri.SadeceGoruntule, []);

    public static IReadOnlyList<YetkiKaynagi> Tumu { get; } = [Kullanici, Firma, Abonelik, Degisiklik];
}
