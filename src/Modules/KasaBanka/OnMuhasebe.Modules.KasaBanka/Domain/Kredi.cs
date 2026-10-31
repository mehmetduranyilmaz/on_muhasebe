using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Shared.Domain.Entities;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.KasaBanka.Domain;

// Nakdi banka kredisi: kullandırımda tutar hesaba girer; ödeme planı bankanın tablosudur. Taksit ödenince anapara krediyi azaltır,
// faiz ve vergiler (BSMV/KKDF) gider olarak kaydedilir. Ödenmiş taksiti olan kredi iptal edilemez.
internal sealed class Kredi : FirmaEntity
{
    private readonly List<KrediTaksiti> _taksitler = [];

    private Kredi() { }

    public string Kod { get; private set; } = null!;
    public string Ad { get; private set; } = null!;
    public KrediTuru Tur { get; private set; }
    public KrediDurumu Durum { get; private set; }
    public Guid HesapId { get; private set; }
    public string ParaBirimi { get; private set; } = null!;
    public DateOnly KullandirimTarihi { get; private set; }
    public decimal Tutar { get; private set; }
    public decimal AylikFaizOrani { get; private set; }
    public decimal KullandirimMasrafi { get; private set; }
    public string? Aciklama { get; private set; }
    public Guid KullandirimIslemId { get; private set; }

    public IReadOnlyCollection<KrediTaksiti> Taksitler => _taksitler;

    public decimal KalanAnapara => Tutar - _taksitler.Where(t => t.OdendiMi).Sum(t => t.Anapara);

    public static Sonuc<Kredi> Olustur(string kod, KrediKaydetIstegi i, KasaHesabi hesap, Guid kullandirimIslemId)
    {
        if (hesap.Tur != HesapTuru.Banka)
        {
            return KasaBankaHatalari.KrediBankaHesabiGerekli;
        }

        if (KrediPlaniHesaplayici.Hata(i.Tutar, i.KullandirimTarihi, i.Taksitler) is { } hata)
        {
            return Hata.Dogrulama("kredi_plani", hata);
        }

        var kredi = new Kredi
        {
            Kod = kod,
            Ad = i.Ad.Trim(),
            Tur = i.Tur,
            Durum = KrediDurumu.Acik,
            HesapId = hesap.Id,
            ParaBirimi = hesap.ParaBirimi,
            KullandirimTarihi = i.KullandirimTarihi,
            Tutar = i.Tutar,
            AylikFaizOrani = i.AylikFaizOrani,
            KullandirimMasrafi = i.KullandirimMasrafi,
            Aciklama = string.IsNullOrWhiteSpace(i.Aciklama) ? null : i.Aciklama.Trim(),
            KullandirimIslemId = kullandirimIslemId
        };
        kredi._taksitler.AddRange(i.Taksitler.Select(t => KrediTaksiti.Olustur(kredi.Id, t)));
        return kredi;
    }

    public Sonuc<KrediTaksiti> TaksitOde(int sira, Guid islemId, DateOnly tarih)
    {
        if (Durum == KrediDurumu.Iptal)
        {
            return KasaBankaHatalari.KrediIptal;
        }

        if (_taksitler.FirstOrDefault(t => t.Sira == sira) is not { } taksit)
        {
            return KasaBankaHatalari.TaksitBulunamadi;
        }

        if (taksit.OdendiMi)
        {
            return KasaBankaHatalari.TaksitOdenmis;
        }

        if (tarih < KullandirimTarihi)
        {
            return KasaBankaHatalari.TaksitTarihi;
        }

        taksit.Ode(islemId, tarih);
        Durum = _taksitler.All(t => t.OdendiMi) ? KrediDurumu.Kapandi : KrediDurumu.Acik;
        return taksit;
    }

    public Sonuc<Guid> TaksitOdemesiniGeriAl(int sira)
    {
        if (_taksitler.FirstOrDefault(t => t.Sira == sira) is not { OdemeIslemId: { } islemId } taksit)
        {
            return KasaBankaHatalari.TaksitOdenmemis;
        }

        taksit.OdemeyiGeriAl();
        Durum = KrediDurumu.Acik;
        return islemId;
    }

    public Sonuc IptalEt()
    {
        if (Durum == KrediDurumu.Iptal)
        {
            return KasaBankaHatalari.KrediIptal;
        }

        if (_taksitler.Any(t => t.OdendiMi))
        {
            return KasaBankaHatalari.OdenmisKrediIptalEdilemez;
        }

        Durum = KrediDurumu.Iptal;
        return Sonuc.Basarili();
    }
}

internal sealed class KrediTaksiti : BaseEntity
{
    private KrediTaksiti() { }

    public Guid KrediId { get; private set; }
    public int Sira { get; private set; }
    public DateOnly Vade { get; private set; }
    public decimal Anapara { get; private set; }
    public decimal Faiz { get; private set; }
    public decimal Vergi { get; private set; }
    public DateOnly? OdemeTarihi { get; private set; }
    public Guid? OdemeIslemId { get; private set; }

    public decimal Toplam => Anapara + Faiz + Vergi;
    public bool OdendiMi => OdemeIslemId is not null;

    public static KrediTaksiti Olustur(Guid krediId, KrediTaksitiGirdisi t) => new()
    {
        KrediId = krediId, Sira = t.Sira, Vade = t.Vade, Anapara = t.Anapara, Faiz = t.Faiz, Vergi = t.Vergi
    };

    public void Ode(Guid islemId, DateOnly tarih) => (OdemeIslemId, OdemeTarihi) = (islemId, tarih);

    public void OdemeyiGeriAl() => (OdemeIslemId, OdemeTarihi) = (null, null);
}
