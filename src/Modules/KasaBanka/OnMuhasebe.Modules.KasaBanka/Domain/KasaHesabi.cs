using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Domain.Abstractions;
using OnMuhasebe.Shared.Domain.Entities;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.KasaBanka.Domain;

internal sealed class KasaHesabi : FirmaEntity, ISilinebilir, IIsyeriVarligi
{
    private KasaHesabi() { }

    public string Kod { get; private set; } = null!;
    public string Ad { get; private set; } = null!;
    public HesapTuru Tur { get; private set; }
    public string ParaBirimi { get; private set; } = null!;
    public string? BankaAdi { get; private set; }

    // Banka hesabının alt türü (yalnızca Banka için dolu): vadesiz, vadeli, KMH.
    public BankaHesapTuru? BankaHesapTuru { get; private set; }
    public string? Sube { get; private set; }
    public string? HesapNo { get; private set; }
    public string? Iban { get; private set; }

    // Kredi kartı limiti (bilgi amaçlı).
    public decimal? Limit { get; private set; }

    // Banka: bağlı şube kartı (portföy yöneticisi/iletişim şubede).
    public Guid? SubeId { get; private set; }

    // Kredi kartı: hesap kesim ve son ödeme günü (ayın günü, 1-31).
    public int? HesapKesimGunu { get; private set; }
    public int? SonOdemeGunu { get; private set; }

    // POS: çalışma şekli ve koşulları. Bloke çözülünce tutar PosAktarilacakHesap'a aktarılır.
    public PosCalismaSekli? PosCalismaSekli { get; private set; }
    public decimal? PosKomisyonOrani { get; private set; }
    public int? PosGunSayisi { get; private set; }
    public BlokeGunTipi? PosGunTipi { get; private set; }
    public Guid? PosAktarilacakHesapId { get; private set; }
    public string? PosTerminalNo { get; private set; }
    public string? PosBatchNo { get; private set; }

    // Kasa ve POS bir işyerinindir; banka ve kredi kartı boşsa firma geneli (her işyeri görür).
    public Guid? IsyeriId { get; private set; }

    public string? Aciklama { get; private set; }
    public bool AktifMi { get; private set; } = true;
    public bool SilindiMi { get; private set; }
    public DateTime? SilinmeTarihi { get; private set; }

    public static string KodOneki(HesapTuru tur) => tur switch
    {
        HesapTuru.Kasa => "KS",
        HesapTuru.Banka => "BN",
        HesapTuru.KrediKarti => "KK",
        _ => "PS"
    };

    public static KasaHesabi Olustur(string kod, HesapBilgileri b, Guid? isyeriId)
    {
        var hesap = new KasaHesabi { Kod = kod, Tur = b.Tur, ParaBirimi = b.ParaBirimi, IsyeriId = isyeriId };
        hesap.Ayarla(b);
        return hesap;
    }

    // Hareketi olan hesabın türü ve dövizi değişirse geçmiş bakiyeler anlamını yitirir.
    public Sonuc Guncelle(string kod, HesapBilgileri b, bool hareketVarMi)
    {
        if (hareketVarMi && (b.Tur != Tur || b.ParaBirimi != ParaBirimi))
        {
            return KasaBankaHatalari.HareketliHesapDegistirilemez;
        }

        Kod = kod;
        Tur = b.Tur;
        ParaBirimi = b.ParaBirimi;
        Ayarla(b);
        return Sonuc.Basarili();
    }

    public void IsyeriAta(Guid? isyeriId) => IsyeriId = isyeriId;

    public void Pasiflestir() => AktifMi = false;

    public void Aktiflestir() => AktifMi = true;

    private void Ayarla(HesapBilgileri b)
    {
        Ad = b.Ad;
        BankaAdi = b.Tur == HesapTuru.Kasa ? null : b.BankaAdi;
        BankaHesapTuru = b.Tur == HesapTuru.Banka ? (b.BankaHesapTuru ?? Contracts.BankaHesapTuru.Vadesiz) : null;
        Sube = b.Tur == HesapTuru.Banka ? b.Sube : null;
        SubeId = b.Tur == HesapTuru.Banka ? b.SubeId : null;
        HesapNo = b.Tur == HesapTuru.Kasa ? null : b.HesapNo;
        Iban = b.Tur == HesapTuru.Banka ? b.Iban : null;
        Limit = b.Tur == HesapTuru.KrediKarti ? b.Limit : null;
        HesapKesimGunu = b.Tur == HesapTuru.KrediKarti ? b.HesapKesimGunu : null;
        SonOdemeGunu = b.Tur == HesapTuru.KrediKarti ? b.SonOdemeGunu : null;
        PosCalismaSekli = b.Tur == HesapTuru.Pos ? b.PosCalismaSekli : null;
        PosKomisyonOrani = b.Tur == HesapTuru.Pos ? b.PosKomisyonOrani : null;
        PosGunSayisi = b.Tur == HesapTuru.Pos ? b.PosGunSayisi : null;
        PosGunTipi = b.Tur == HesapTuru.Pos ? b.PosGunTipi : null;
        PosAktarilacakHesapId = b.Tur == HesapTuru.Pos ? b.PosAktarilacakHesapId : null;
        PosTerminalNo = b.Tur == HesapTuru.Pos ? b.PosTerminalNo : null;
        PosBatchNo = b.Tur == HesapTuru.Pos ? b.PosBatchNo : null;
        Aciklama = b.Aciklama;
    }
}

internal sealed record HesapBilgileri(
    string Ad,
    HesapTuru Tur,
    string ParaBirimi,
    string? BankaAdi,
    BankaHesapTuru? BankaHesapTuru,
    string? Sube,
    string? HesapNo,
    string? Iban,
    decimal? Limit,
    Guid? SubeId,
    int? HesapKesimGunu,
    int? SonOdemeGunu,
    PosCalismaSekli? PosCalismaSekli,
    decimal? PosKomisyonOrani,
    int? PosGunSayisi,
    BlokeGunTipi? PosGunTipi,
    Guid? PosAktarilacakHesapId,
    string? PosTerminalNo,
    string? PosBatchNo,
    string? Aciklama)
{
    public static HesapBilgileri Istekten(HesapKaydetIstegi i) => new(
        i.Ad.Trim(), i.Tur, i.ParaBirimi, Temiz(i.BankaAdi), i.BankaHesapTuru, Temiz(i.Sube), Temiz(i.HesapNo),
        Temiz(i.Iban) is { } iban ? Shared.Domain.Dogrulama.Iban.Normalize(iban) : null, i.Limit, i.SubeId,
        i.HesapKesimGunu, i.SonOdemeGunu, i.PosCalismaSekli, i.PosKomisyonOrani, i.PosGunSayisi, i.PosGunTipi,
        i.PosAktarilacakHesapId, Temiz(i.PosTerminalNo), Temiz(i.PosBatchNo), Temiz(i.Aciklama));

    private static string? Temiz(string? deger) => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();
}

// Değiştirilemez kasa/banka kaydı. Tutarlar hesabın dövizinde; TL karşılığı işlem kuruyla sabitlenir. Düzeltme ters kayıtla yapılır.
internal sealed class KasaHareketi : FirmaEntity, IDonemeTabi
{
    public const int KurOndalik = 6;

    private KasaHareketi() { }

    public Guid HesapId { get; private set; }
    public Guid IslemId { get; private set; }
    public DateOnly Tarih { get; private set; }
    public KasaHareketTuru Tur { get; private set; }
    public string ParaBirimi { get; private set; } = null!;
    public decimal Giris { get; private set; }
    public decimal Cikis { get; private set; }
    public decimal Kur { get; private set; }
    public decimal GirisTl { get; private set; }
    public decimal CikisTl { get; private set; }
    public string? BelgeNo { get; private set; }
    public string? Aciklama { get; private set; }
    public Guid? CariHesapId { get; private set; }
    public Guid? KategoriId { get; private set; }

    // Fatura gibi başka modüldeki kaynak belge (peşin tahsilat); bu işlemler belgesinden iptal edilir.
    public Guid? KaynakBelgeId { get; private set; }
    public Guid? IptalEdilenHareketId { get; private set; }
    [DonemKilidiDisinda]
    public Guid? IptalHareketId { get; private set; }

    // Banka ekstresinde karşılığı olan satır (mutabakat). Bir hareket yalnızca bir ekstre satırıyla eşleşir.
    [DonemKilidiDisinda]
    public Guid? EkstreSatiriId { get; private set; }

    public bool IptalEdildiMi => IptalHareketId is not null;
    public bool IptalKaydiMi => IptalEdilenHareketId is not null;

    public void EkstreyleEslestir(Guid ekstreSatiriId) => EkstreSatiriId = ekstreSatiriId;

    public static Sonuc<KasaHareketi> Olustur(
        KasaHesabi hesap, Guid islemId, KasaHareketTuru tur, bool girisMi, DateOnly tarih, decimal tutar, decimal kur,
        string? belgeNo, string? aciklama, Guid? cariHesapId = null, Guid? kategoriId = null, Guid? kaynakBelgeId = null)
    {
        if (!hesap.AktifMi)
        {
            return KasaBankaHatalari.PasifHesap;
        }

        tutar = Para.Yuvarla(tutar);
        kur = hesap.ParaBirimi == ParaBirimleri.Tl ? 1m : Math.Round(kur, KurOndalik, MidpointRounding.AwayFromZero);
        if (tutar <= 0)
        {
            return KasaBankaHatalari.TutarPozitifOlmali;
        }

        if (kur <= 0)
        {
            return KasaBankaHatalari.KurGerekli;
        }

        var tl = Para.TlKarsiligi(tutar, kur);
        return new KasaHareketi
        {
            HesapId = hesap.Id,
            IslemId = islemId,
            Tarih = tarih,
            Tur = tur,
            ParaBirimi = hesap.ParaBirimi,
            Giris = girisMi ? tutar : 0m,
            Cikis = girisMi ? 0m : tutar,
            Kur = kur,
            GirisTl = girisMi ? tl : 0m,
            CikisTl = girisMi ? 0m : tl,
            BelgeNo = belgeNo,
            Aciklama = aciklama,
            CariHesapId = cariHesapId,
            KategoriId = kategoriId,
            KaynakBelgeId = kaynakBelgeId
        };
    }

    public Sonuc<KasaHareketi> IptalEt()
    {
        if (IptalKaydiMi || IptalEdildiMi)
        {
            return KasaBankaHatalari.ZatenIptalEdilmis;
        }

        var ters = new KasaHareketi
        {
            HesapId = HesapId,
            IslemId = IslemId,
            Tarih = Tarih,
            Tur = Tur,
            ParaBirimi = ParaBirimi,
            Giris = Cikis,
            Cikis = Giris,
            Kur = Kur,
            GirisTl = CikisTl,
            CikisTl = GirisTl,
            BelgeNo = BelgeNo,
            Aciklama = $"İptal: {Aciklama}".TrimEnd(' ', ':'),
            CariHesapId = CariHesapId,
            KategoriId = KategoriId,
            KaynakBelgeId = KaynakBelgeId,
            IptalEdilenHareketId = Id
        };

        IptalHareketId = ters.Id;
        return ters;
    }
}

// Gelir/gider raporunun kaynağı. Kasa hareketi (Ödendi) ya da yalnızca cari hareketi (Tahakkuk) aynı IslemId ile bağlıdır.
internal sealed class GelirGiderKaydi : FirmaEntity, IDonemeTabi
{
    private GelirGiderKaydi() { }

    public Guid IslemId { get; private set; }
    public GelirGiderTuru Tur { get; private set; }
    public GelirGiderBicimi Bicim { get; private set; }
    public Guid KategoriId { get; private set; }
    public DateOnly Tarih { get; private set; }
    public string ParaBirimi { get; private set; } = null!;
    public decimal Tutar { get; private set; }
    public decimal Kur { get; private set; }
    public decimal TutarTl { get; private set; }
    public Guid? HesapId { get; private set; }
    public Guid? CariHesapId { get; private set; }
    public string? BelgeNo { get; private set; }
    public string? Aciklama { get; private set; }
    public Guid? MasrafMerkeziId { get; private set; }
    [DonemKilidiDisinda]
    public DateTime? IptalTarihi { get; private set; }

    public static GelirGiderKaydi Olustur(
        Guid islemId, GelirGiderTuru tur, GelirGiderBicimi bicim, Guid kategoriId, DateOnly tarih, string paraBirimi, decimal tutar, decimal kur,
        Guid? hesapId, Guid? cariHesapId, string? belgeNo, string? aciklama, Guid? masrafMerkeziId = null) => new()
    {
        MasrafMerkeziId = masrafMerkeziId,
        IslemId = islemId,
        Tur = tur,
        Bicim = bicim,
        KategoriId = kategoriId,
        Tarih = tarih,
        ParaBirimi = paraBirimi,
        Tutar = Para.Yuvarla(tutar),
        Kur = kur,
        TutarTl = Para.TlKarsiligi(tutar, kur),
        HesapId = hesapId,
        CariHesapId = cariHesapId,
        BelgeNo = belgeNo,
        Aciklama = aciklama
    };

    public void IptalEt(DateTime simdi) => IptalTarihi ??= simdi;
}

internal sealed class MasrafMerkezi : FirmaEntity
{
    private MasrafMerkezi() { }

    public string Kod { get; private set; } = null!;
    public string Ad { get; private set; } = null!;
    public bool AktifMi { get; private set; } = true;

    public static MasrafMerkezi Olustur(string kod, string ad) => new() { Kod = kod.Trim().ToUpperInvariant(), Ad = ad.Trim() };

    public void Guncelle(string kod, string ad) => (Kod, Ad) = (kod.Trim().ToUpperInvariant(), ad.Trim());

    public void AktiflikAyarla(bool aktif) => AktifMi = aktif;
}

internal sealed class GelirGiderKategorisi : FirmaEntity
{
    private GelirGiderKategorisi() { }

    public string Ad { get; private set; } = null!;
    public GelirGiderTuru Tur { get; private set; }
    public bool AktifMi { get; private set; } = true;

    // Firmanın ilk gelir/gider kaydında açılan başlangıç kategorileri; kullanıcı düzenleyebilir.
    public static IReadOnlyList<(string Ad, GelirGiderTuru Tur)> Varsayilanlar { get; } =
    [
        ("Kira", GelirGiderTuru.Gider),
        ("Elektrik, su, doğalgaz", GelirGiderTuru.Gider),
        ("Personel", GelirGiderTuru.Gider),
        ("Vergi ve SGK", GelirGiderTuru.Gider),
        ("Banka ve POS komisyonu", GelirGiderTuru.Gider),
        ("Yakıt ve ulaşım", GelirGiderTuru.Gider),
        ("Diğer giderler", GelirGiderTuru.Gider),
        ("Faiz geliri", GelirGiderTuru.Gelir),
        ("Kira geliri", GelirGiderTuru.Gelir),
        ("Diğer gelirler", GelirGiderTuru.Gelir)
    ];

    public static GelirGiderKategorisi Olustur(string ad, GelirGiderTuru tur) => new() { Ad = ad.Trim(), Tur = tur };

    public void Guncelle(string ad) => Ad = ad.Trim();

    public void AktiflikAyarla(bool aktif) => AktifMi = aktif;
}
