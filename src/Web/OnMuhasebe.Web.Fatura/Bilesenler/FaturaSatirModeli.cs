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

    // 2. ve 3. iskonto alanları yalnızca istenince (ya da doluysa) görünür.
    public bool EkIskontoAcik { get; set; }

    public bool KartliMi => StokKartiId is not null;
    public bool StokluMu => KartliMi && KartTuru == StokTuru.Urun;

    public IEnumerable<string> BirimSecenekleri => KartliMi
        ? EkBirimler.Select(b => b.BirimKodu).Prepend(TemelBirim)
        : Birimler.Tumu.Select(b => b.Kod);

    public decimal Katsayi(string birim) =>
        birim == TemelBirim ? 1m : EkBirimler.FirstOrDefault(b => b.BirimKodu == birim)?.Katsayi ?? 1m;

    // Birim değişince fiyat da çevrilir: adet fiyatı 10 ise koli (12) fiyatı 120 olur.
    public void BirimDegistir(string yeni)
    {
        if (KartliMi && yeni != Birim)
        {
            BirimFiyat = Math.Round(BirimFiyat / Katsayi(Birim) * Katsayi(yeni), 4, MidpointRounding.AwayFromZero);
        }

        Birim = yeni;
    }

    public void KartAta(StokKartiDetayi kart)
    {
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
        TemelBirim = kart.TemelBirim;
        EkBirimler = kart.EkBirimler;
    }

    public HesapSatiri Hesap() => new(Miktar, BirimFiyat, KdvDahilMi, KdvOrani, Iskonto1, Iskonto2, Iskonto3);

    public FaturaSatiriIstegi Istek() =>
        new(StokKartiId, Ad, StokluMu ? DepoId : null, Birim, Miktar, BirimFiyat, KdvDahilMi, KdvOrani, Iskonto1, Iskonto2, Iskonto3);

    public static FaturaSatirModeli Detaydan(FaturaSatiriDetayi s) => new()
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
}
