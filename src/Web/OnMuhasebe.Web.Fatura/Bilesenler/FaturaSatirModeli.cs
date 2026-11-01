using OnMuhasebe.Web.Bilesenler;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Shared.Contracts.Birimler;
using OnMuhasebe.Shared.Contracts.Vergi;

namespace OnMuhasebe.Web.Fatura.Bilesenler;

// Fatura formunda düzenlenen satır. Stok kartından geldiyse birim seçenekleri kartın birimleridir.
public sealed class FaturaSatirModeli
{
    public Guid Anahtar { get; } = Guid.NewGuid();
    public Guid? StokKartiId { get; set; }
    public string? StokKodu { get; set; }
    public StokTuru? KartTuru { get; set; }
    public string TemelBirim { get; set; } = Birimler.Adet;
    public IReadOnlyList<EkBirim> EkBirimler { get; set; } = [];
    public string Ad { get; set; } = "";
    public Guid? DepoId { get; set; }
    public string Birim { get; set; } = Birimler.Adet;
    public decimal Miktar { get; set; } = 1;
    public decimal BirimFiyat { get; set; }
    public bool KdvDahilMi { get; set; }
    public int KdvOrani { get; set; } = KdvOranlari.Genel;
    public decimal Iskonto1 { get; set; }
    public decimal Iskonto2 { get; set; }
    public decimal Iskonto3 { get; set; }

    public string? TevkifatKodu { get; set; }
    public int TevkifatPay { get; set; }
    public int TevkifatPayda { get; set; }

    // 2. ve 3. iskonto alanları yalnızca istenince (ya da doluysa) görünür.
    public bool EkIskontoAcik { get; set; }

    // Tevkifat şeridi yalnızca istenince (ya da doluysa) görünür.
    public bool TevkifatAcik { get; set; }

    public bool TevkifatliMi => TevkifatPayda > 0;

    // İrsaliyeden aktarılan satır: ürün, birim ve depo irsaliyeyle aynı kalır; miktar faturalanmamış kalanı aşamaz.
    public Guid? IrsaliyeSatiriId { get; set; }
    public string? IrsaliyeNo { get; set; }
    public decimal? AzamiMiktar { get; set; }

    public bool IrsaliyeliMi => IrsaliyeSatiriId is not null;

    // Siparişten aktarılan (irsaliyesiz) satır: ürün ve birim siparişle aynı kalır.
    public Guid? SiparisSatiriId { get; set; }
    public string? SiparisNo { get; set; }

    public bool SiparisliMi => SiparisSatiriId is not null;

    // Kod listesinden seçince oran da dolar; temizleyince tevkifat kalkar.
    public void TevkifatSec(string? kod)
    {
        var t = TevkifatKodlari.Bul(kod);
        TevkifatKodu = t?.Kod;
        TevkifatPay = t?.Pay ?? 0;
        TevkifatPayda = t?.Payda ?? 0;
    }

    public bool KartliMi => StokKartiId is not null;
    public bool StokluMu => KartliMi && KartTuru == StokTuru.Urun;

    public IEnumerable<string> BirimSecenekleri => KartliMi
        ? EkBirimler.Select(b => b.BirimKodu).Prepend(TemelBirim)
        : Birimler.Tumu.Select(b => b.Kod);

    public decimal Katsayi(string birim) =>
        birim == TemelBirim ? 1m : EkBirimler.FirstOrDefault(b => b.BirimKodu == birim)?.Katsayi ?? 1m;

    // Fiyat listesinden ya da karttan gelen öneri; miktar/birim değişince ulaşılan kademenin fiyatı ve iskontosu uygulanır.
    // Kullanıcı fiyatı elle değiştirirse öneri bırakılır (elle girilen fiyat korunur).
    public FiyatOnerisi? Oneri { get; set; }

    public string? FiyatListesi => Oneri?.ListeAdi;

    public void OneriUygula()
    {
        if (Oneri?.Kademe(Miktar * Katsayi(Birim)) is { } k)
        {
            BirimFiyat = Math.Round(k.Fiyat * Katsayi(Birim), 4, MidpointRounding.AwayFromZero);
            Iskonto1 = k.Iskonto;
        }
    }

    public void MiktarDegistir(decimal miktar)
    {
        Miktar = miktar;
        OneriUygula();
    }

    public void FiyatGir(decimal fiyat)
    {
        BirimFiyat = fiyat;
        Oneri = null;
    }

    // Birim değişince fiyat da çevrilir: adet fiyatı 10 ise koli (12) fiyatı 120 olur.
    public void BirimDegistir(string yeni)
    {
        if (Oneri is not null)
        {
            Birim = yeni;
            OneriUygula();
            return;
        }

        if (KartliMi && yeni != Birim)
        {
            BirimFiyat = Math.Round(BirimFiyat / Katsayi(Birim) * Katsayi(yeni), 4, MidpointRounding.AwayFromZero);
        }

        Birim = yeni;
    }

    // Lot/seri izlenen üründe stok hareketine giden lot, SKT ve seriler.
    public IzlemeModeli Izleme { get; } = new();

    public decimal TemelMiktar => Miktar * Katsayi(Birim);

    public void KartAta(StokKartiDetayi kart)
    {
        Izleme.Ayarla(kart.Izleme.ToString(), kart.SktTakibi);
        StokKartiId = kart.Id;
        StokKodu = kart.Kod;
        KartTuru = kart.Tur;
        TemelBirim = kart.TemelBirim;
        EkBirimler = kart.EkBirimler;
        Ad = kart.Ad;
        Birim = kart.TemelBirim;
        KdvOrani = kart.KdvOrani;
    }

    // Düzenlenen faturada satırın kendi değerleri korunur; yalnızca birim seçenekleri karttan alınır.
    public void BirimleriAta(StokKartiDetayi kart)
    {
        Izleme.Ayarla(kart.Izleme.ToString(), kart.SktTakibi);
        TemelBirim = kart.TemelBirim;
        EkBirimler = kart.EkBirimler;
    }

    public HesapSatiri Hesap() => new(Miktar, BirimFiyat, KdvDahilMi, KdvOrani, Iskonto1, Iskonto2, Iskonto3, TevkifatPay, TevkifatPayda);

    public FaturaSatiriIstegi Istek() =>
        new(StokKartiId, Ad, StokluMu ? DepoId : null, Birim, Miktar, BirimFiyat, KdvDahilMi, KdvOrani, Iskonto1, Iskonto2, Iskonto3,
            TevkifatKodu, TevkifatPay, TevkifatPayda, IrsaliyeSatiriId, SiparisSatiriId,
            IrsaliyeliMi ? null : Izleme.LotIstegi, IrsaliyeliMi ? null : Izleme.SktIstegi, IrsaliyeliMi ? null : Izleme.SeriIstegi);

    // Tekrar planında saklanan satır; stok kartlı satırın birimleri form açılırken karttan yüklenir.
    public static FaturaSatirModeli Istekten(FaturaSatiriIstegi s) => new()
    {
        StokKartiId = s.StokKartiId,
        TemelBirim = s.Birim,
        Ad = s.Ad ?? "",
        DepoId = s.DepoId,
        Birim = s.Birim,
        Miktar = s.Miktar,
        BirimFiyat = s.BirimFiyat,
        KdvDahilMi = s.KdvDahilMi,
        KdvOrani = s.KdvOrani,
        Iskonto1 = s.Iskonto1,
        Iskonto2 = s.Iskonto2,
        Iskonto3 = s.Iskonto3,
        TevkifatKodu = s.TevkifatKodu,
        TevkifatPay = s.TevkifatPay,
        TevkifatPayda = s.TevkifatPayda,
        EkIskontoAcik = s.Iskonto2 > 0 || s.Iskonto3 > 0,
        TevkifatAcik = s.TevkifatPayda > 0
    };

    public static FaturaSatirModeli Detaydan(FaturaSatiriDetayi s)
    {
        var satir = DetayModeli(s);
        satir.Izleme.Yukle(s.LotNo, s.SonKullanmaTarihi, s.SeriNolari);
        return satir;
    }

    private static FaturaSatirModeli DetayModeli(FaturaSatiriDetayi s) => new()
    {
        StokKartiId = s.StokKartiId,
        StokKodu = s.StokKodu,
        KartTuru = s.StokKartiId is null ? null : s.StokluMu ? StokTuru.Urun : StokTuru.Hizmet,
        TemelBirim = s.Birim,
        Ad = s.Ad,
        DepoId = s.DepoId,
        Birim = s.Birim,
        Miktar = s.Miktar,
        BirimFiyat = s.BirimFiyat,
        KdvDahilMi = s.KdvDahilMi,
        KdvOrani = s.KdvOrani,
        Iskonto1 = s.Iskonto1,
        Iskonto2 = s.Iskonto2,
        Iskonto3 = s.Iskonto3,
        TevkifatKodu = s.TevkifatKodu,
        TevkifatPay = s.TevkifatPay,
        TevkifatPayda = s.TevkifatPayda,
        EkIskontoAcik = s.Iskonto2 > 0 || s.Iskonto3 > 0,
        TevkifatAcik = s.TevkifatPayda > 0,
        IrsaliyeSatiriId = s.IrsaliyeSatiriId,
        SiparisSatiriId = s.SiparisSatiriId
    };

    public static FaturaSatirModeli TeklifSiparistan(TeklifSiparisSatiriDetayi s) => new()
    {
        StokKartiId = s.StokKartiId,
        StokKodu = s.StokKodu,
        KartTuru = s.StokKartiId is null ? null : s.StokluMu ? StokTuru.Urun : StokTuru.Hizmet,
        TemelBirim = s.Birim,
        Ad = s.Ad,
        DepoId = s.DepoId,
        Birim = s.Birim,
        Miktar = s.Miktar,
        BirimFiyat = s.BirimFiyat,
        KdvDahilMi = s.KdvDahilMi,
        KdvOrani = s.KdvOrani,
        Iskonto1 = s.Iskonto1,
        Iskonto2 = s.Iskonto2,
        Iskonto3 = s.Iskonto3,
        EkIskontoAcik = s.Iskonto2 > 0 || s.Iskonto3 > 0
    };

    // Fiyat ve iskontolar siparişte anlaşılan gibidir; döviz farklıysa çağıran taraf fiyatı çevirir.
    public static FaturaSatirModeli Siparisten(AktarilacakSiparisSatiri a, decimal miktar, StokKartiDetayi? kart) => new()
    {
        StokKartiId = a.StokKartiId,
        StokKodu = a.StokKodu,
        KartTuru = a.StokKartiId is null ? null : a.StokluMu ? StokTuru.Urun : StokTuru.Hizmet,
        TemelBirim = kart?.TemelBirim ?? a.Birim,
        EkBirimler = kart?.EkBirimler ?? [],
        Ad = a.Ad,
        DepoId = a.DepoId,
        Birim = a.Birim,
        Miktar = miktar,
        BirimFiyat = a.BirimFiyat,
        KdvDahilMi = a.KdvDahilMi,
        KdvOrani = a.KdvOrani,
        Iskonto1 = a.Iskonto1,
        Iskonto2 = a.Iskonto2,
        Iskonto3 = a.Iskonto3,
        EkIskontoAcik = a.Iskonto2 > 0 || a.Iskonto3 > 0,
        SiparisSatiriId = a.SiparisSatiriId,
        SiparisNo = a.SiparisNo
    };

    // İrsaliye fiyatı KDV hariçtir; fiyat yoksa çağıran taraf önerilen fiyatı verir.
    public static FaturaSatirModeli Irsaliyeden(AktarilacakIrsaliyeSatiri a, decimal miktar, StokKartiDetayi kart) => new()
    {
        StokKartiId = a.StokKartiId,
        StokKodu = a.StokKodu,
        KartTuru = StokTuru.Urun,
        TemelBirim = kart.TemelBirim,
        EkBirimler = kart.EkBirimler,
        Ad = a.Ad,
        DepoId = a.DepoId,
        Birim = a.Birim,
        Miktar = miktar,
        BirimFiyat = a.BirimFiyat ?? 0m,
        KdvDahilMi = false,
        KdvOrani = a.KdvOrani,
        IrsaliyeSatiriId = a.IrsaliyeSatiriId,
        IrsaliyeNo = a.IrsaliyeNo,
        AzamiMiktar = a.KalanMiktar
    };
}
