using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Shared.Domain.Entities;

namespace OnMuhasebe.Modules.KasaBanka.Domain.CekSenet;

// Çek keşidecisi / senet borçlusu. Cari olmak zorunda değildir (müşterinin ciroladığı üçüncü kişi çekleri).
// Karşılıksız geçmişi evraklardan hesaplanır; kara liste elle de işaretlenebilir.
internal sealed class EvrakBorclusu : FirmaEntity
{
    private readonly List<BorcluHesabi> _hesaplar = [];

    private EvrakBorclusu() { }

    public string Ad { get; private set; } = null!;

    // VKN (10) ya da TCKN (11); firmada tekildir.
    public string? KimlikNo { get; private set; }
    public string? Telefon { get; private set; }
    public string? Adres { get; private set; }
    public string? Not { get; private set; }
    public bool KaraListede { get; private set; }

    // Karşılıksız geçmişi olsa da yeni evrakta uyarı verilmez (borç kapandı, kullanıcı güveniyor).
    public bool UyariKapali { get; private set; }
    public string? RiskNotu { get; private set; }

    public IReadOnlyList<BorcluHesabi> Hesaplar => _hesaplar;

    public static EvrakBorclusu Olustur(string ad, string? kimlikNo, string? telefon) => new()
    {
        Ad = ad.Trim(),
        KimlikNo = Temiz(kimlikNo),
        Telefon = Temiz(telefon)
    };

    public void Guncelle(BorcluKaydetIstegi i)
    {
        Ad = i.Ad.Trim();
        KimlikNo = Temiz(i.KimlikNo);
        Telefon = Temiz(i.Telefon);
        Adres = Temiz(i.Adres);
        Not = Temiz(i.Not);
    }

    public void RiskAyarla(RiskDurumuIstegi i)
    {
        KaraListede = i.KaraListede;
        UyariKapali = i.UyariKapali && !i.KaraListede;
        RiskNotu = Temiz(i.RiskNotu);
    }

    // Çek hesabı (banka/şube/hesap no) bir kez girilir, sonraki çeklerde seçilir.
    public void HesapHatirla(string? bankaAdi, string? sube, string? hesapNo)
    {
        if (string.IsNullOrWhiteSpace(hesapNo) && string.IsNullOrWhiteSpace(bankaAdi))
        {
            return;
        }

        var (b, s, h) = (Temiz(bankaAdi), Temiz(sube), Temiz(hesapNo));
        if (!_hesaplar.Any(x => x.BankaAdi == b && x.Sube == s && x.HesapNo == h))
        {
            _hesaplar.Add(BorcluHesabi.Olustur(Id, b, s, h));
        }
    }

    private static string? Temiz(string? deger) => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();
}

internal sealed class BorcluHesabi : FirmaEntity
{
    private BorcluHesabi() { }

    public Guid BorcluId { get; private set; }
    public string? BankaAdi { get; private set; }
    public string? Sube { get; private set; }
    public string? HesapNo { get; private set; }

    public static BorcluHesabi Olustur(Guid borcluId, string? bankaAdi, string? sube, string? hesapNo) =>
        new() { BorcluId = borcluId, BankaAdi = bankaAdi, Sube = sube, HesapNo = hesapNo };
}

// Firma başına tek satır; kayıt yoksa varsayılanlar geçerlidir.
internal sealed class CekSenetAyari : FirmaEntity
{
    private CekSenetAyari() { }

    public bool KesideciKimlikZorunlu { get; private set; } = true;
    public bool SenetBorcluKimlikZorunlu { get; private set; }
    public RiskDavranisi RiskDavranisi { get; private set; } = RiskDavranisi.UyarVeOnayIste;
    public int KarneUyariEsigi { get; private set; } = 5;

    public static CekSenetAyari Varsayilan() => new();

    public void Guncelle(CekSenetAyarlari a)
    {
        KesideciKimlikZorunlu = a.KesideciKimlikZorunlu;
        SenetBorcluKimlikZorunlu = a.SenetBorcluKimlikZorunlu;
        RiskDavranisi = a.RiskDavranisi;
        KarneUyariEsigi = a.KarneUyariEsigi;
    }

    public CekSenetAyarlari Sozlesme() => new(KesideciKimlikZorunlu, SenetBorcluKimlikZorunlu, RiskDavranisi, KarneUyariEsigi);

    public bool KimlikZorunluMu(EvrakTuru tur) => tur == EvrakTuru.Cek ? KesideciKimlikZorunlu : SenetBorcluKimlikZorunlu;
}
