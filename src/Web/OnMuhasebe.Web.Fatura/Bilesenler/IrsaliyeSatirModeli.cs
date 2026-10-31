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

    public IrsaliyeSatiriIstegi Istek(bool fiyatli) => new(StokKartiId, Ad, DepoId, Birim, Miktar, fiyatli ? BirimFiyat : null);

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
        FaturalananMiktar = s.FaturalananMiktar
    };
}
