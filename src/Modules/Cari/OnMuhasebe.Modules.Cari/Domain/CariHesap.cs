using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Shared.Domain.Abstractions;
using OnMuhasebe.Shared.Domain.Dogrulama;
using OnMuhasebe.Shared.Domain.Entities;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Cari.Domain;

internal sealed class CariHesap : FirmaEntity, ISilinebilir
{
    public const string VarsayilanUlke = CariVarsayilanlari.Ulke;

    private readonly List<CariAdres> _adresler = [];
    private readonly List<CariIlgiliKisi> _ilgiliKisiler = [];
    private readonly List<CariBankaHesabi> _bankaHesaplari = [];

    private CariHesap() { }

    public string Kod { get; private set; } = null!;
    public string Unvan { get; private set; } = null!;
    public CariTuru Tur { get; private set; }
    public KisiTipi KisiTipi { get; private set; }

    // e-Fatura'da şahıs alıcı ad ve soyadla ayrı gönderilir; tüzel caride boştur.
    public string? Ad { get; private set; }
    public string? Soyad { get; private set; }
    public string? VergiNo { get; private set; }
    public string? VergiDairesi { get; private set; }
    public string Ulke { get; private set; } = VarsayilanUlke;
    public string? Il { get; private set; }
    public string? Ilce { get; private set; }
    public string? Adres { get; private set; }
    public string? PostaKodu { get; private set; }
    public string? Telefon { get; private set; }
    public string? CepTelefonu { get; private set; }
    public string? Faks { get; private set; }
    public string? Eposta { get; private set; }
    public string? WebSitesi { get; private set; }
    public string ParaBirimi { get; private set; } = null!;
    public int? VadeGun { get; private set; }

    // Geç ödemede uygulanacak yıllık vade farkı (gecikme faizi) oranı, %. Sözleşmede yoksa boş bırakılır.
    public decimal? VadeFarkiOrani { get; private set; }
    public decimal? RiskLimiti { get; private set; }
    public Guid? GrupId { get; private set; }
    public string? Not { get; private set; }
    // Açıksa bu cariyle farklı dövizdeki kasa/banka hesabından tahsilat/ödeme yapılabilir (çevrim kuruyla).
    public bool FarkliDovizdeIslem { get; private set; }

    // Alıcının GİB'de kayıtlı e-Fatura posta kutusu etiketi (urn:mail:...); entegratör bağlanınca sorgudan da doldurulacak.
    public bool EFaturaMukellefiMi { get; private set; }
    public string? EFaturaEtiketi { get; private set; }

    // Bu cariye kesilen faturada yeni satırlara önerilen değerler; satırda değiştirilebilir.
    public string? VarsayilanKdvIstisnaKodu { get; private set; }
    public string? VarsayilanTevkifatKodu { get; private set; }
    public bool AktifMi { get; private set; } = true;
    public bool SilindiMi { get; private set; }
    public DateTime? SilinmeTarihi { get; private set; }

    public CariGrup? Grup { get; private set; }
    public IReadOnlyCollection<CariAdres> Adresler => _adresler;
    public IReadOnlyCollection<CariIlgiliKisi> IlgiliKisiler => _ilgiliKisiler;
    public IReadOnlyCollection<CariBankaHesabi> BankaHesaplari => _bankaHesaplari;

    public static CariHesap Olustur(string kod, CariBilgileri bilgiler)
    {
        var cari = new CariHesap { Kod = kod, ParaBirimi = bilgiler.ParaBirimi };
        cari.BilgileriAyarla(bilgiler);
        return cari;
    }

    // Hareketi olan carinin para birimi değişirse geçmiş tutarlar anlamını yitirir.
    public Sonuc Guncelle(string kod, CariBilgileri bilgiler, bool hareketVarMi)
    {
        if (bilgiler.ParaBirimi != ParaBirimi && hareketVarMi)
        {
            return CariHatalari.ParaBirimiDegistirilemez;
        }

        Kod = kod;
        ParaBirimi = bilgiler.ParaBirimi;
        BilgileriAyarla(bilgiler);
        return Sonuc.Basarili();
    }

    public void Pasiflestir() => AktifMi = false;

    public void Aktiflestir() => AktifMi = true;

    public static Sonuc SilinebilirMi(bool hareketVarMi) =>
        hareketVarMi ? CariHatalari.HareketliCariSilinemez : Sonuc.Basarili();

    // Şahısta ad/soyad girilmemişse ünvandan ayrılır: son kelime soyad, öncesi ad (tek kelimede yalnızca ad).
    public static (string? Ad, string? Soyad) AdSoyadAyir(string unvan)
    {
        var parcalar = unvan.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parcalar.Length switch
        {
            0 => (null, null),
            1 => (parcalar[0], null),
            _ => (string.Join(' ', parcalar[..^1]), parcalar[^1])
        };
    }

    private void BilgileriAyarla(CariBilgileri b)
    {
        Unvan = b.Unvan;
        Tur = b.Tur;
        KisiTipi = b.KisiTipi;
        (Ad, Soyad) = b.KisiTipi != KisiTipi.Sahis ? (null, null)
            : b.Ad is null && b.Soyad is null ? AdSoyadAyir(b.Unvan)
            : (b.Ad, b.Soyad);
        VergiNo = b.VergiNo;
        VergiDairesi = b.VergiDairesi;
        Ulke = b.Ulke ?? VarsayilanUlke;
        Il = b.Il;
        Ilce = b.Ilce;
        Adres = b.Adres;
        PostaKodu = b.PostaKodu;
        Telefon = b.Telefon;
        CepTelefonu = b.CepTelefonu;
        Faks = b.Faks;
        Eposta = b.Eposta;
        WebSitesi = b.WebSitesi;
        VadeGun = b.VadeGun;
        VadeFarkiOrani = b.VadeFarkiOrani;
        RiskLimiti = b.RiskLimiti;
        GrupId = b.GrupId;
        Not = b.Not;
        FarkliDovizdeIslem = b.FarkliDovizdeIslem;
        EFaturaMukellefiMi = b.EFaturaMukellefiMi;
        EFaturaEtiketi = b.EFaturaMukellefiMi ? b.EFaturaEtiketi : null;
        VarsayilanKdvIstisnaKodu = b.VarsayilanKdvIstisnaKodu;
        VarsayilanTevkifatKodu = b.VarsayilanTevkifatKodu;

        // null: istemci bu bölümü göndermedi (eski istemci, Excel aktarımı), mevcut kayıtlar korunur.
        if (b.Adresler is not null)
        {
            _adresler.Clear();
            _adresler.AddRange(b.Adresler.Select((a, i) => CariAdres.Olustur(i + 1, a)));
        }

        if (b.IlgiliKisiler is not null)
        {
            _ilgiliKisiler.Clear();
            _ilgiliKisiler.AddRange(b.IlgiliKisiler.Select((k, i) => CariIlgiliKisi.Olustur(i + 1, k)));
        }

        if (b.BankaHesaplari is not null)
        {
            _bankaHesaplari.Clear();
            _bankaHesaplari.AddRange(b.BankaHesaplari.Select((h, i) => CariBankaHesabi.Olustur(i + 1, h)));
        }
    }
}

// Teslimat/şube adresi. Fatura adresi kartın kendisindedir.
internal sealed class CariAdres : BaseEntity
{
    private CariAdres() { }

    public Guid CariHesapId { get; private set; }
    public int Sira { get; private set; }
    public string Baslik { get; private set; } = null!;
    public string? AdSoyad { get; private set; }
    public string? Telefon { get; private set; }
    public string Ulke { get; private set; } = null!;
    public string? Il { get; private set; }
    public string? Ilce { get; private set; }
    public string Adres { get; private set; } = null!;
    public string? PostaKodu { get; private set; }

    public static CariAdres Olustur(int sira, CariAdresBilgisi a) => new()
    {
        Sira = sira, Baslik = a.Baslik, AdSoyad = a.AdSoyad, Telefon = a.Telefon, Ulke = a.Ulke ?? CariHesap.VarsayilanUlke,
        Il = a.Il, Ilce = a.Ilce, Adres = a.Adres, PostaKodu = a.PostaKodu
    };
}

internal sealed class CariIlgiliKisi : BaseEntity
{
    private CariIlgiliKisi() { }

    public Guid CariHesapId { get; private set; }
    public int Sira { get; private set; }
    public string AdSoyad { get; private set; } = null!;
    public string? Gorev { get; private set; }
    public string? Telefon { get; private set; }
    public string? Eposta { get; private set; }

    public static CariIlgiliKisi Olustur(int sira, CariIlgiliKisiBilgisi k) => new()
    {
        Sira = sira, AdSoyad = k.AdSoyad, Gorev = k.Gorev, Telefon = k.Telefon, Eposta = k.Eposta
    };
}

internal sealed class CariBankaHesabi : BaseEntity
{
    private CariBankaHesabi() { }

    public Guid CariHesapId { get; private set; }
    public int Sira { get; private set; }
    public string BankaAdi { get; private set; } = null!;
    public string? Sube { get; private set; }
    public string? HesapNo { get; private set; }
    public string? Iban { get; private set; }
    public string ParaBirimi { get; private set; } = null!;

    public static CariBankaHesabi Olustur(int sira, CariBankaHesabiBilgisi h) => new()
    {
        Sira = sira, BankaAdi = h.BankaAdi, Sube = h.Sube, HesapNo = h.HesapNo, Iban = h.Iban, ParaBirimi = h.ParaBirimi
    };
}

internal sealed record CariBilgileri(
    string Unvan,
    CariTuru Tur,
    KisiTipi KisiTipi,
    string? Ad,
    string? Soyad,
    string? VergiNo,
    string? VergiDairesi,
    string? Ulke,
    string? Il,
    string? Ilce,
    string? Adres,
    string? PostaKodu,
    string? Telefon,
    string? CepTelefonu,
    string? Faks,
    string? Eposta,
    string? WebSitesi,
    string ParaBirimi,
    int? VadeGun,
    decimal? RiskLimiti,
    decimal? VadeFarkiOrani,
    Guid? GrupId,
    string? Not,
    bool FarkliDovizdeIslem,
    bool EFaturaMukellefiMi,
    string? EFaturaEtiketi,
    string? VarsayilanKdvIstisnaKodu,
    string? VarsayilanTevkifatKodu,
    IReadOnlyList<CariAdresBilgisi>? Adresler,
    IReadOnlyList<CariIlgiliKisiBilgisi>? IlgiliKisiler,
    IReadOnlyList<CariBankaHesabiBilgisi>? BankaHesaplari)
{
    public static CariBilgileri Istekten(CariKaydetIstegi i) => new(
        i.Unvan.Trim(), i.Tur, i.KisiTipi, Temiz(i.Ad), Temiz(i.Soyad), Temiz(i.VergiNo), Temiz(i.VergiDairesi), Temiz(i.Ulke),
        Temiz(i.Il), Temiz(i.Ilce), Temiz(i.Adres), Temiz(i.PostaKodu), Temiz(i.Telefon), Temiz(i.CepTelefonu), Temiz(i.Faks),
        Temiz(i.Eposta)?.ToLowerInvariant(), Temiz(i.WebSitesi), i.ParaBirimi, i.VadeGun, i.RiskLimiti, i.VadeFarkiOrani, i.GrupId,
        Temiz(i.Not), i.FarkliDovizdeIslem, i.EFaturaMukellefiMi, Temiz(i.EFaturaEtiketi), Temiz(i.VarsayilanKdvIstisnaKodu),
        Temiz(i.VarsayilanTevkifatKodu),
        i.Adresler?.Select(a => new CariAdresBilgisi(a.Baslik.Trim(), Temiz(a.AdSoyad), Temiz(a.Telefon), Temiz(a.Ulke), Temiz(a.Il),
            Temiz(a.Ilce), a.Adres.Trim(), Temiz(a.PostaKodu))).ToList(),
        i.IlgiliKisiler?.Select(k => new CariIlgiliKisiBilgisi(k.AdSoyad.Trim(), Temiz(k.Gorev), Temiz(k.Telefon),
            Temiz(k.Eposta)?.ToLowerInvariant())).ToList(),
        i.BankaHesaplari?.Select(h => new CariBankaHesabiBilgisi(h.BankaAdi.Trim(), Temiz(h.Sube), Temiz(h.HesapNo),
            Temiz(h.Iban) is { } iban ? Iban.Normalize(iban) : null, h.ParaBirimi)).ToList());

    private static string? Temiz(string? deger) => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();
}

internal sealed class CariGrup : FirmaEntity
{
    private CariGrup() { }

    public string Ad { get; private set; } = null!;

    public static CariGrup Olustur(string ad) => new() { Ad = ad.Trim() };

    public void AdDegistir(string ad) => Ad = ad.Trim();
}
