using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Shared.Domain.Abstractions;
using OnMuhasebe.Shared.Domain.Entities;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Fatura.Domain;

internal static class TeklifSiparisHatalari
{
    public static readonly Hata Bulunamadi = Hata.Bulunamadi("teklif_siparis_bulunamadi", "Teklif/sipariş bulunamadı.");
    public static readonly Hata SatirGerekli = Hata.Dogrulama("teklif_siparis_satir", "En az bir satır olmalıdır.");
    public static readonly Hata CariGerekli = Hata.Dogrulama("siparis_cari_gerekli", "Sipariş için cari seçilmelidir (teklif aday müşteriye verilebilir).");
    public static readonly Hata UnvanGerekli = Hata.Dogrulama("teklif_unvan_gerekli", "Cari seçilmeyen teklifte müşterinin unvanı yazılmalıdır.");
    public static readonly Hata TarihGecersiz = Hata.Dogrulama("teklif_siparis_tarih", "Geçerlilik ve termin tarihi belge tarihinden önce olamaz.");
    public static readonly Hata TurDegistirilemez = Hata.IsKurali("teklif_siparis_tur", "Belgenin türü değiştirilemez.");
    public static readonly Hata Duzenlenemez = Hata.IsKurali("teklif_siparis_duzenlenemez",
        "Yalnızca taslak ya da karşılanmamış açık belge düzenlenebilir.");
    public static readonly Hata OnaylanamazDurum = Hata.IsKurali("teklif_siparis_onay", "Yalnızca taslak belge onaylanır.");
    public static readonly Hata YanitlanamazDurum = Hata.IsKurali("teklif_yanit", "Yalnızca verilmiş (açık) teklif kabul ya da reddedilebilir.");
    public static readonly Hata DonusturulemezDurum = Hata.IsKurali("teklif_donusum", "Siparişe yalnızca açık ya da kabul edilmiş teklif dönüştürülür.");
    public static readonly Hata KapatilamazDurum = Hata.IsKurali("siparis_kapat", "Yalnızca açık sipariş kapatılabilir.");
    public static readonly Hata IptalEdilemez = Hata.IsKurali("teklif_siparis_iptal", "Karşılanmış (sevk/fatura edilmiş) sipariş iptal edilemez; kalanı için 'kapat' kullanın.");
    public static readonly Hata YalnizcaTaslakSilinir = Hata.IsKurali("teklif_siparis_sil", "Yalnızca taslak silinebilir.");
    public static readonly Hata KarsilananAsildi = Hata.IsKurali("siparis_karsilanan_asildi", "Sevk/fatura edilen miktar negatif olamaz.");
    public static readonly Hata SiparisBagiGecersiz = Hata.IsKurali("siparis_bagi_gecersiz",
        "Sipariş bağı geçersiz: sipariş açık olmalı; cari, belge türü, ürün ve birim siparişle aynı olmalıdır.");
}

// Teklif ve sipariş: fatura gibi hesaplanır ama stok/cari hareketi yaratmaz. Siparişte satır bazında karşılanan miktar
// (irsaliye ya da irsaliyesiz fatura) onaylı belgelerden yeniden hesaplanır.
internal sealed class TeklifSiparis : FirmaEntity, ISilinebilir, IDonemeTabi, IIsyeriVarligi
{
    private readonly List<TeklifSiparisSatiri> _satirlar = [];

    private TeklifSiparis() { }

    public TeklifSiparisTuru Tur { get; private set; }
    public TeklifSiparisDurumu Durum { get; private set; }
    public string? Numara { get; private set; }
    public DateOnly Tarih { get; private set; }
    public DateOnly? GecerlilikTarihi { get; private set; }
    public DateOnly? TerminTarihi { get; private set; }

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
    public string? OdemeKosulu { get; private set; }
    public string? TeslimKosulu { get; private set; }
    public string? Aciklama { get; private set; }

    public decimal BrutToplam { get; private set; }
    public decimal SatirIskontoToplami { get; private set; }
    public decimal GenelIskontoTutari { get; private set; }
    public decimal Matrah { get; private set; }
    public decimal KdvToplami { get; private set; }
    public decimal GenelToplam { get; private set; }

    public Guid? KaynakTeklifId { get; private set; }
    public DateTime? OnayTarihi { get; private set; }
    // Belgenin düzenlendiği işyeri (seri, raporlar ve işyeri kısıtı buna göre).
    public Guid IsyeriId { get; private set; }

    public bool SilindiMi { get; private set; }
    public DateTime? SilinmeTarihi { get; private set; }

    public void IsyeriAta(Guid isyeriId) => IsyeriId = isyeriId;

    public IReadOnlyCollection<TeklifSiparisSatiri> Satirlar => _satirlar;

    public bool TeklifMi => TeklifSiparisTurleri.TeklifMi(Tur);
    public bool KarsilanmisMi => _satirlar.Any(s => s.KarsilananMiktar > 0m);

    public KarsilamaDurumu? Karsilama => TeklifMi ? null : TeklifSiparisHesaplari.Karsilama(_satirlar.Select(s => (s.Miktar, s.KarsilananMiktar)));

    public static Sonuc<TeklifSiparis> Olustur(TeklifSiparisBilgileri b, Guid? kaynakTeklifId = null)
    {
        var belge = new TeklifSiparis { Tur = b.Tur, Durum = TeklifSiparisDurumu.Taslak, KaynakTeklifId = kaynakTeklifId };
        var sonuc = belge.Ayarla(b);
        return sonuc.BasariliMi ? belge : sonuc.Hata;
    }

    public Sonuc Guncelle(TeklifSiparisBilgileri b)
    {
        if (b.Tur != Tur)
        {
            return TeklifSiparisHatalari.TurDegistirilemez;
        }

        return Durum == TeklifSiparisDurumu.Taslak || (Durum == TeklifSiparisDurumu.Acik && !KarsilanmisMi)
            ? Ayarla(b)
            : TeklifSiparisHatalari.Duzenlenemez;
    }

    public Sonuc Onayla(string numara, DateTime simdi)
    {
        if (Durum != TeklifSiparisDurumu.Taslak)
        {
            return TeklifSiparisHatalari.OnaylanamazDurum;
        }

        (Numara, Durum, OnayTarihi) = (numara, TeklifSiparisDurumu.Acik, simdi);
        return Sonuc.Basarili();
    }

    public Sonuc Yanitla(bool kabulMu, string? neden)
    {
        if (!TeklifMi || Durum != TeklifSiparisDurumu.Acik)
        {
            return TeklifSiparisHatalari.YanitlanamazDurum;
        }

        Durum = kabulMu ? TeklifSiparisDurumu.Kabul : TeklifSiparisDurumu.Red;
        if (!kabulMu && !string.IsNullOrWhiteSpace(neden))
        {
            Aciklama = string.IsNullOrWhiteSpace(Aciklama) ? $"Red nedeni: {neden.Trim()}" : $"{Aciklama}\nRed nedeni: {neden.Trim()}";
        }

        return Sonuc.Basarili();
    }

    // Teklif siparişe dönüşünce kabul edilmiş sayılır.
    public Sonuc SipariseDonustu()
    {
        if (!TeklifMi || Durum is not (TeklifSiparisDurumu.Acik or TeklifSiparisDurumu.Kabul))
        {
            return TeklifSiparisHatalari.DonusturulemezDurum;
        }

        Durum = TeklifSiparisDurumu.Kabul;
        return Sonuc.Basarili();
    }

    // Sipariş kapatılınca kalan miktar artık beklenmez (müşteri vazgeçti / tedarikçi gönderemeyecek).
    public Sonuc Kapat()
    {
        if (TeklifMi || Durum != TeklifSiparisDurumu.Acik)
        {
            return TeklifSiparisHatalari.KapatilamazDurum;
        }

        Durum = TeklifSiparisDurumu.Kapatildi;
        return Sonuc.Basarili();
    }

    public Sonuc IptalEt()
    {
        if (Durum is TeklifSiparisDurumu.Iptal or TeklifSiparisDurumu.Taslak || KarsilanmisMi)
        {
            return TeklifSiparisHatalari.IptalEdilemez;
        }

        Durum = TeklifSiparisDurumu.Iptal;
        return Sonuc.Basarili();
    }

    // Karşılanan miktar, bu satıra bağlı onaylı irsaliye ve irsaliyesiz fatura satırlarının toplamıdır.
    public Sonuc KarsilananAyarla(Guid satirId, decimal toplam) =>
        _satirlar.FirstOrDefault(s => s.Id == satirId) is { } satir ? satir.KarsilananAyarla(toplam) : TeklifSiparisHatalari.Bulunamadi;

    private Sonuc Ayarla(TeklifSiparisBilgileri b)
    {
        if (b.Satirlar.Count == 0)
        {
            return TeklifSiparisHatalari.SatirGerekli;
        }

        if (b.Cari.CariHesapId is null && !TeklifSiparisTurleri.TeklifMi(b.Tur))
        {
            return TeklifSiparisHatalari.CariGerekli;
        }

        if (string.IsNullOrWhiteSpace(b.Cari.Unvan))
        {
            return TeklifSiparisHatalari.UnvanGerekli;
        }

        if (b.GecerlilikTarihi < b.Tarih || b.TerminTarihi < b.Tarih)
        {
            return TeklifSiparisHatalari.TarihGecersiz;
        }

        var hesap = FaturaHesaplayici.Hesapla(
            b.Satirlar.Select(s => new HesapSatiri(s.Miktar, s.BirimFiyat, s.KdvDahilMi, s.KdvOrani, s.Iskonto1, s.Iskonto2, s.Iskonto3)).ToList(),
            b.GenelIskontoTuru, b.GenelIskonto);

        Tarih = b.Tarih;
        GecerlilikTarihi = TeklifSiparisTurleri.TeklifMi(b.Tur) ? b.GecerlilikTarihi : null;
        TerminTarihi = b.TerminTarihi;
        (CariHesapId, CariKodu, CariUnvan, VergiNo, VergiDairesi, Il, Ilce, Adres) =
            (b.Cari.CariHesapId, b.Cari.Kod, b.Cari.Unvan, b.Cari.VergiNo, b.Cari.VergiDairesi, b.Cari.Il, b.Cari.Ilce, b.Cari.Adres);
        ParaBirimi = b.ParaBirimi;
        Kur = b.ParaBirimi == ParaBirimleri.Tl ? 1m : Math.Round(b.Kur, FaturaBelgesi.KurOndalik, MidpointRounding.AwayFromZero);
        (GenelIskontoTuru, GenelIskonto, OdemeKosulu, TeslimKosulu, Aciklama) = (b.GenelIskontoTuru, b.GenelIskonto, b.OdemeKosulu, b.TeslimKosulu, b.Aciklama);

        _satirlar.Clear();
        for (var i = 0; i < b.Satirlar.Count; i++)
        {
            _satirlar.Add(TeklifSiparisSatiri.Olustur(Id, i + 1, b.Satirlar[i], hesap.Satirlar[i]));
        }

        (BrutToplam, SatirIskontoToplami, GenelIskontoTutari, Matrah, KdvToplami, GenelToplam) =
            (hesap.BrutToplam, hesap.SatirIskontoToplami, hesap.GenelIskontoTutari, hesap.Matrah, hesap.KdvToplami, hesap.GenelToplam);
        return Sonuc.Basarili();
    }
}

internal sealed class TeklifSiparisSatiri : BaseEntity
{
    private TeklifSiparisSatiri() { }

    public Guid TeklifSiparisId { get; private set; }
    public int Sira { get; private set; }
    public Guid? StokKartiId { get; private set; }
    public string? StokKodu { get; private set; }
    public string Ad { get; private set; } = null!;
    public bool StokluMu { get; private set; }
    public Guid? DepoId { get; private set; }
    public string Birim { get; private set; } = null!;
    public decimal Miktar { get; private set; }
    public decimal BirimFiyat { get; private set; }
    public bool KdvDahilMi { get; private set; }
    public int KdvOrani { get; private set; }
    public decimal Iskonto1 { get; private set; }
    public decimal Iskonto2 { get; private set; }
    public decimal Iskonto3 { get; private set; }
    public decimal Matrah { get; private set; }
    public decimal KdvTutari { get; private set; }
    public decimal Toplam { get; private set; }
    public decimal KarsilananMiktar { get; private set; }

    public decimal KalanMiktar => Math.Max(0m, Miktar - KarsilananMiktar);

    public static TeklifSiparisSatiri Olustur(Guid belgeId, int sira, FaturaSatiriBilgisi b, SatirHesabi h) => new()
    {
        TeklifSiparisId = belgeId, Sira = sira, StokKartiId = b.StokKartiId, StokKodu = b.StokKodu, Ad = b.Ad, StokluMu = b.StokluMu,
        DepoId = b.StokluMu ? b.DepoId : null, Birim = b.Birim, Miktar = b.Miktar, BirimFiyat = b.BirimFiyat, KdvDahilMi = b.KdvDahilMi,
        KdvOrani = b.KdvOrani, Iskonto1 = b.Iskonto1, Iskonto2 = b.Iskonto2, Iskonto3 = b.Iskonto3,
        Matrah = h.Matrah, KdvTutari = h.KdvTutari, Toplam = h.Toplam
    };

    // Siparişten fazla teslim (tolerans, yuvarlama, tartım farkı) olağandır; engellenmez, kalan sıfırda kalır.
    public Sonuc KarsilananAyarla(decimal toplam)
    {
        if (toplam < 0m)
        {
            return TeklifSiparisHatalari.KarsilananAsildi;
        }

        KarsilananMiktar = toplam;
        return Sonuc.Basarili();
    }
}

internal sealed record TeklifSiparisBilgileri(
    TeklifSiparisTuru Tur,
    FaturaCarisi Cari,
    DateOnly Tarih,
    DateOnly? GecerlilikTarihi,
    DateOnly? TerminTarihi,
    string ParaBirimi,
    decimal Kur,
    GenelIskontoTuru GenelIskontoTuru,
    decimal GenelIskonto,
    string? OdemeKosulu,
    string? TeslimKosulu,
    string? Aciklama,
    IReadOnlyList<FaturaSatiriBilgisi> Satirlar);
