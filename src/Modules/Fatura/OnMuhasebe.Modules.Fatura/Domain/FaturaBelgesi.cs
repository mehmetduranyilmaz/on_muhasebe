using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Shared.Domain.Abstractions;
using OnMuhasebe.Shared.Domain.Entities;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Fatura.Domain;

// Sınıf adı "Fatura" değil: modül namespace'i (OnMuhasebe.Modules.Fatura) ile çakışmasın.
// Tutarlar faturanın para biriminde (= carinin para birimi), TL karşılıkları kurla hesaplanır.
internal sealed class FaturaBelgesi : FirmaEntity, ISilinebilir, IDonemeTabi
{
    public const int KurOndalik = 6;
    public const string NihaiTuketiciUnvani = "Nihai Tüketici";
    public const string NihaiTuketiciTckn = "11111111111";

    private readonly List<FaturaSatiri> _satirlar = [];

    private FaturaBelgesi() { }

    public FaturaTuru Tur { get; private set; }
    public SatisSekli? SatisSekli { get; private set; }
    public FaturaDurumu Durum { get; private set; }
    public string? Numara { get; private set; }
    public DateOnly Tarih { get; private set; }
    public DateOnly? VadeTarihi { get; private set; }

    public Guid? CariHesapId { get; private set; }
    public string? CariKodu { get; private set; }
    public string CariUnvan { get; private set; } = null!;
    public string? VergiNo { get; private set; }
    public string? VergiDairesi { get; private set; }
    public string? Il { get; private set; }
    public string? Ilce { get; private set; }
    public string? Adres { get; private set; }

    public string ParaBirimi { get; private set; } = null!;
    public decimal Kur { get; private set; }
    public GenelIskontoTuru GenelIskontoTuru { get; private set; }
    public decimal GenelIskonto { get; private set; }
    public string? Aciklama { get; private set; }

    // Kesinleşirken bu kasa/banka/POS hesabından peşin tahsil edilir (ödenir).
    public Guid? PesinHesapId { get; private set; }

    // Gelir/giderin ait olduğu masraf merkezi (Kasa/Banka modülündeki tanım; modüller arası yabancı anahtar yok).
    public Guid? MasrafMerkeziId { get; private set; }

    public decimal BrutToplam { get; private set; }
    public decimal SatirIskontoToplami { get; private set; }
    public decimal GenelIskontoTutari { get; private set; }
    public decimal Matrah { get; private set; }
    public decimal KdvToplami { get; private set; }
    public decimal GenelToplam { get; private set; }
    public decimal GenelToplamTl { get; private set; }

    // Stopaj (gelir vergisi tevkifatı) matrah toplamına uygulanır. OdenecekTutar carinin gördüğü tutardır:
    // GenelToplam - tevkif edilen KDV - stopaj. GenelToplam belge brütü olarak değişmez kalır.
    public decimal StopajOrani { get; private set; }
    public decimal StopajTutari { get; private set; }
    public decimal TevkifEdilenKdvToplami { get; private set; }
    public decimal OdenecekTutar { get; private set; }

    // e-Fatura/e-Arşiv UUID'si; entegratör bağlantısı geldiğinde belge bununla gönderilir.
    public Guid Ettn { get; private set; }
    public DateTime? OnayTarihi { get; private set; }
    public DateTime? IptalTarihi { get; private set; }
    public bool SilindiMi { get; private set; }
    public DateTime? SilinmeTarihi { get; private set; }

    public IReadOnlyCollection<FaturaSatiri> Satirlar => _satirlar;

    public bool GidenMi => FaturaTurleri.GidenMi(Tur);

    public static Sonuc<FaturaBelgesi> Olustur(FaturaBilgileri bilgiler)
    {
        var fatura = new FaturaBelgesi { Tur = bilgiler.Tur, Durum = FaturaDurumu.Taslak, Ettn = Guid.NewGuid() };
        var sonuc = fatura.Ayarla(bilgiler);
        return sonuc.BasariliMi ? fatura : sonuc.Hata;
    }

    // Onaylı faturanın düzenlenip düzenlenemeyeceği firmanın kayıt akışına bağlıdır; bunu çağıran taraf denetler.
    public Sonuc Guncelle(FaturaBilgileri bilgiler)
    {
        if (Durum == FaturaDurumu.Iptal)
        {
            return FaturaHatalari.IptalEdilmis;
        }

        return bilgiler.Tur != Tur ? FaturaHatalari.TurDegistirilemez : Ayarla(bilgiler);
    }

    // Giden faturada numara burada (transaction içinde, sayaçtan) verilir; gelen faturada girilmiş olmalıdır.
    public Sonuc Onayla(string? verilenNumara, DateTime simdi)
    {
        if (Durum != FaturaDurumu.Taslak)
        {
            return Durum == FaturaDurumu.Onayli ? FaturaHatalari.ZatenOnayli : FaturaHatalari.IptalEdilmis;
        }

        if (GidenMi)
        {
            Numara ??= verilenNumara;
        }

        if (Numara is null)
        {
            return FaturaHatalari.NumaraGerekli;
        }

        Durum = FaturaDurumu.Onayli;
        OnayTarihi = simdi;
        return Sonuc.Basarili();
    }

    // Numara korunur; iptal edilen fatura sırasını boşaltmaz.
    public Sonuc IptalEt(DateTime simdi)
    {
        if (Durum != FaturaDurumu.Onayli)
        {
            return Durum == FaturaDurumu.Taslak ? FaturaHatalari.TaslakIptalEdilemez : FaturaHatalari.IptalEdilmis;
        }

        Durum = FaturaDurumu.Iptal;
        IptalTarihi = simdi;
        return Sonuc.Basarili();
    }

    private Sonuc Ayarla(FaturaBilgileri b)
    {
        if (b.Satirlar.Count == 0)
        {
            return FaturaHatalari.SatirGerekli;
        }

        if (b.VadeTarihi < b.Tarih)
        {
            return FaturaHatalari.VadeTarihtenOnceOlamaz;
        }

        var hesap = FaturaHesaplayici.Hesapla(
            b.Satirlar.Select(s => new HesapSatiri(s.Miktar, s.BirimFiyat, s.KdvDahilMi, s.KdvOrani, s.Iskonto1, s.Iskonto2, s.Iskonto3, s.TevkifatPay, s.TevkifatPayda)).ToList(),
            b.GenelIskontoTuru, b.GenelIskonto, b.StopajOrani);
        if (b.GenelIskontoTuru == GenelIskontoTuru.Tutar && FaturaHesaplayici.Yuvarla(b.GenelIskonto) > hesap.BrutToplam - hesap.SatirIskontoToplami)
        {
            return FaturaHatalari.GenelIskontoFazla;
        }

        SatisSekli = FaturaTurleri.SatisTarafiMi(b.Tur) ? b.SatisSekli ?? Contracts.SatisSekli.Toptan : null;
        Tarih = b.Tarih;
        VadeTarihi = b.VadeTarihi;
        if (!GidenMi)
        {
            Numara = b.Numara;
        }

        CariHesapId = b.Cari.CariHesapId;
        CariKodu = b.Cari.Kod;
        CariUnvan = b.Cari.Unvan;
        VergiNo = b.Cari.VergiNo;
        VergiDairesi = b.Cari.VergiDairesi;
        Il = b.Cari.Il;
        Ilce = b.Cari.Ilce;
        Adres = b.Cari.Adres;

        ParaBirimi = b.ParaBirimi;
        Kur = b.ParaBirimi == ParaBirimleri.Tl ? 1m : Math.Round(b.Kur, KurOndalik, MidpointRounding.AwayFromZero);
        GenelIskontoTuru = b.GenelIskontoTuru;
        GenelIskonto = b.GenelIskonto;
        Aciklama = b.Aciklama;
        PesinHesapId = b.PesinHesapId;
        MasrafMerkeziId = b.MasrafMerkeziId;

        _satirlar.Clear();
        for (var i = 0; i < b.Satirlar.Count; i++)
        {
            _satirlar.Add(FaturaSatiri.Olustur(Id, i + 1, b.Satirlar[i], hesap.Satirlar[i]));
        }

        BrutToplam = hesap.BrutToplam;
        SatirIskontoToplami = hesap.SatirIskontoToplami;
        GenelIskontoTutari = hesap.GenelIskontoTutari;
        Matrah = hesap.Matrah;
        KdvToplami = hesap.KdvToplami;
        GenelToplam = hesap.GenelToplam;
        GenelToplamTl = FaturaHesaplayici.Yuvarla(GenelToplam * Kur);
        StopajOrani = b.StopajOrani;
        StopajTutari = hesap.StopajTutari;
        TevkifEdilenKdvToplami = hesap.TevkifEdilenKdvToplami;
        OdenecekTutar = hesap.OdenecekTutar;
        return Sonuc.Basarili();
    }
}

internal sealed class FaturaSatiri : BaseEntity
{
    private FaturaSatiri() { }

    public Guid FaturaId { get; private set; }
    public int Sira { get; private set; }
    public Guid? StokKartiId { get; private set; }
    public string? StokKodu { get; private set; }
    public string Ad { get; private set; } = null!;

    // Ürün kartlı satır; hizmet kartı ve serbest satır stok hareketi oluşturmaz.
    public bool StokluMu { get; private set; }
    public Guid? DepoId { get; private set; }
    public string Birim { get; private set; } = null!;
    public decimal Miktar { get; private set; }
    public decimal BirimFiyat { get; private set; }
    public bool KdvDahilMi { get; private set; }
    public decimal Iskonto1 { get; private set; }
    public decimal Iskonto2 { get; private set; }
    public decimal Iskonto3 { get; private set; }
    public int KdvOrani { get; private set; }
    public decimal BrutTutar { get; private set; }
    public decimal IskontoTutari { get; private set; }
    public decimal GenelIskontoPayi { get; private set; }
    public decimal Matrah { get; private set; }
    public decimal KdvTutari { get; private set; }
    public decimal Toplam { get; private set; }

    public string? TevkifatKodu { get; private set; }
    public int TevkifatPay { get; private set; }
    public int TevkifatPayda { get; private set; }
    public decimal TevkifEdilenKdv { get; private set; }

    // İrsaliyeden aktarılan satır: stok hareketi irsaliyede oluştuğu için fatura stok yazmaz.
    public Guid? IrsaliyeSatiriId { get; private set; }

    // İrsaliyesiz satırın karşıladığı sipariş satırı; irsaliyeli satırda bağ irsaliye satırındadır.
    public Guid? SiparisSatiriId { get; private set; }

    public static FaturaSatiri Olustur(Guid faturaId, int sira, FaturaSatiriBilgisi b, SatirHesabi h) => new()
    {
        FaturaId = faturaId,
        Sira = sira,
        StokKartiId = b.StokKartiId,
        StokKodu = b.StokKodu,
        Ad = b.Ad,
        StokluMu = b.StokluMu,
        DepoId = b.StokluMu ? b.DepoId : null,
        Birim = b.Birim,
        Miktar = b.Miktar,
        BirimFiyat = b.BirimFiyat,
        KdvDahilMi = b.KdvDahilMi,
        Iskonto1 = b.Iskonto1,
        Iskonto2 = b.Iskonto2,
        Iskonto3 = b.Iskonto3,
        KdvOrani = b.KdvOrani,
        BrutTutar = h.BrutTutar,
        IskontoTutari = h.IskontoTutari,
        GenelIskontoPayi = h.GenelIskontoPayi,
        Matrah = h.Matrah,
        KdvTutari = h.KdvTutari,
        Toplam = h.Toplam,
        TevkifatKodu = b.TevkifatKodu,
        TevkifatPay = b.TevkifatPay,
        TevkifatPayda = b.TevkifatPayda,
        TevkifEdilenKdv = h.TevkifEdilenKdv,
        IrsaliyeSatiriId = b.IrsaliyeSatiriId,
        SiparisSatiriId = b.IrsaliyeSatiriId is null ? b.SiparisSatiriId : null
    };
}

// Cari kartının ya da nihai tüketicinin fatura anındaki kopyası.
internal sealed record FaturaCarisi(
    Guid? CariHesapId,
    string? Kod,
    string Unvan,
    string? VergiNo,
    string? VergiDairesi,
    string? Il,
    string? Ilce,
    string? Adres);

internal sealed record FaturaSatiriBilgisi(
    Guid? StokKartiId,
    string? StokKodu,
    string Ad,
    bool StokluMu,
    Guid? DepoId,
    string Birim,
    decimal Miktar,
    decimal BirimFiyat,
    bool KdvDahilMi,
    int KdvOrani,
    decimal Iskonto1,
    decimal Iskonto2,
    decimal Iskonto3,
    string? TevkifatKodu = null,
    int TevkifatPay = 0,
    int TevkifatPayda = 0,
    Guid? IrsaliyeSatiriId = null,
    Guid? SiparisSatiriId = null);

// İstek, cari/stok/kur bilgileriyle çözümlendikten sonra entity'ye verilen hali.
internal sealed record FaturaBilgileri(
    FaturaTuru Tur,
    SatisSekli? SatisSekli,
    FaturaCarisi Cari,
    DateOnly Tarih,
    DateOnly? VadeTarihi,
    string? Numara,
    string ParaBirimi,
    decimal Kur,
    GenelIskontoTuru GenelIskontoTuru,
    decimal GenelIskonto,
    string? Aciklama,
    IReadOnlyList<FaturaSatiriBilgisi> Satirlar,
    Guid? PesinHesapId = null,
    decimal StopajOrani = 0m,
    Guid? MasrafMerkeziId = null);
