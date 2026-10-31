using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Modules.Stok.Contracts;

namespace OnMuhasebe.Web.Fatura.Bilesenler;

// İrsaliye formunda düzenlenen satır; yalnızca ürün kartı. Fiyat opsiyoneldir (alış irsaliyesinde zorunlu).
public sealed class IrsaliyeSatirModeli
{
    public Guid Anahtar { get; } = Guid.NewGuid();
    public Guid StokKartiId { get; set; }
    public string StokKodu { get; set; } = "";
    public string TemelBirim { get; set; } = "";
    public IReadOnlyList<EkBirim> EkBirimler { get; set; } = [];
    public string Ad { get; set; } = "";
    public Guid? DepoId { get; set; }
    public string Birim { get; set; } = "";
    public decimal Miktar { get; set; } = 1;
    public decimal? BirimFiyat { get; set; }

    // Düzenlenen irsaliyede faturalanmış miktar (bilgi amaçlı).
    public decimal FaturalananMiktar { get; set; }

    // Siparişten aktarılan satır: ürün ve birim siparişle aynı kalır.
    public Guid? SiparisSatiriId { get; set; }
    public string? SiparisNo { get; set; }

    public bool SiparisliMi => SiparisSatiriId is not null;

    public IEnumerable<string> BirimSecenekleri => EkBirimler.Select(b => b.BirimKodu).Prepend(TemelBirim);

    public decimal? Tutar => BirimFiyat is { } f ? Math.Round(f * Miktar, 2, MidpointRounding.AwayFromZero) : null;

    public decimal Katsayi(string birim) =>
        birim == TemelBirim ? 1m : EkBirimler.FirstOrDefault(b => b.BirimKodu == birim)?.Katsayi ?? 1m;

    // Birim değişince fiyat da çevrilir: adet fiyatı 10 ise koli (12) fiyatı 120 olur.
    public void BirimDegistir(string yeni)
    {
        if (yeni != Birim && BirimFiyat is { } fiyat)
        {
            BirimFiyat = Math.Round(fiyat / Katsayi(Birim) * Katsayi(yeni), 4, MidpointRounding.AwayFromZero);
        }

        Birim = yeni;
    }

    public void KartAta(StokKartiDetayi kart)
    {
        StokKartiId = kart.Id;
        StokKodu = kart.Kod;
        TemelBirim = kart.TemelBirim;
        EkBirimler = kart.EkBirimler;
        Ad = kart.Ad;
        Birim = kart.TemelBirim;
    }

    public void BirimleriAta(StokKartiDetayi kart)
    {
        TemelBirim = kart.TemelBirim;
        EkBirimler = kart.EkBirimler;
    }

    public IrsaliyeSatiriIstegi Istek(bool fiyatli) => new(StokKartiId, Ad, DepoId, Birim, Miktar, fiyatli ? BirimFiyat : null, SiparisSatiriId);

    // İrsaliye fiyatı KDV hariçtir: siparişteki KDV dahil fiyat hariçe, iskontolar düşülerek çevrilir (döviz farkını çağıran çevirir).
    public static IrsaliyeSatirModeli Siparisten(AktarilacakSiparisSatiri a, decimal miktar, StokKartiDetayi kart)
    {
        var fiyat = a.BirimFiyat * (1 - a.Iskonto1 / 100m) * (1 - a.Iskonto2 / 100m) * (1 - a.Iskonto3 / 100m);
        if (a.KdvDahilMi)
        {
            fiyat /= 1m + a.KdvOrani / 100m;
        }

        var satir = new IrsaliyeSatirModeli { DepoId = a.DepoId, Miktar = miktar, SiparisSatiriId = a.SiparisSatiriId, SiparisNo = a.SiparisNo };
        satir.KartAta(kart);
        satir.Ad = a.Ad;
        satir.Birim = a.Birim;
        satir.BirimFiyat = Math.Round(fiyat, 4, MidpointRounding.AwayFromZero);
        return satir;
    }

    public static IrsaliyeSatirModeli Detaydan(IrsaliyeSatiriDetayi s) => new()
    {
        StokKartiId = s.StokKartiId,
        StokKodu = s.StokKodu,
        TemelBirim = s.Birim,
        Ad = s.Ad,
        DepoId = s.DepoId,
        Birim = s.Birim,
        Miktar = s.Miktar,
        BirimFiyat = s.BirimFiyat,
        FaturalananMiktar = s.FaturalananMiktar,
        SiparisSatiriId = s.SiparisSatiriId
    };
}
