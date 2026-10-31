using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Shared.Domain.Abstractions;
using OnMuhasebe.Shared.Domain.Entities;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Fatura.Domain;

// Sevk belgesi: onaylanınca stok hareketi oluşur, cari etkilenmez. Faturalanan miktar satır bazında izlenir;
// bir irsaliye birden çok faturaya (kısmi), bir fatura birden çok irsaliyeye (toplu) bağlanabilir.
internal sealed class Irsaliye : FirmaEntity, ISilinebilir, IDonemeTabi
{
    private readonly List<IrsaliyeSatiri> _satirlar = [];

    private Irsaliye() { }

    public IrsaliyeTuru Tur { get; private set; }
    public IrsaliyeDurumu Durum { get; private set; }
    public string? Numara { get; private set; }
    public DateOnly Tarih { get; private set; }
    public DateOnly SevkTarihi { get; private set; }
    public TimeOnly SevkSaati { get; private set; }

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

    public Guid? HedefDepoId { get; private set; }
    public string? TeslimAdresi { get; private set; }
    public string? AracPlakasi { get; private set; }
    public string? SoforAdi { get; private set; }
    public string? SoforTckn { get; private set; }
    public string? TasiyiciUnvani { get; private set; }
    public string? TasiyiciVkn { get; private set; }
    public string? Aciklama { get; private set; }

    public DateTime? OnayTarihi { get; private set; }
    public DateTime? IptalTarihi { get; private set; }
    public bool SilindiMi { get; private set; }
    public DateTime? SilinmeTarihi { get; private set; }

    public IReadOnlyCollection<IrsaliyeSatiri> Satirlar => _satirlar;

    public bool GidenMi => IrsaliyeTurleri.GidenMi(Tur);
    public bool FaturalanmisMi => _satirlar.Any(s => s.FaturalananMiktar > 0m);

    public FaturalamaDurumu? FaturalamaDurumu => IrsaliyeTurleri.FaturalanirMi(Tur)
        ? IrsaliyeHesaplari.Durum(_satirlar.Select(s => (s.Miktar, s.FaturalananMiktar)))
        : null;

    public static Sonuc<Irsaliye> Olustur(IrsaliyeBilgileri bilgiler)
    {
        var irsaliye = new Irsaliye { Tur = bilgiler.Tur, Durum = IrsaliyeDurumu.Taslak };
        var sonuc = irsaliye.Ayarla(bilgiler);
        return sonuc.BasariliMi ? irsaliye : sonuc.Hata;
    }

    // Onaylı irsaliyenin düzenlenebilmesi firmanın kayıt akışına bağlıdır; bunu çağıran taraf denetler.
    public Sonuc Guncelle(IrsaliyeBilgileri bilgiler)
    {
        if (Durum == IrsaliyeDurumu.Iptal)
        {
            return IrsaliyeHatalari.IptalEdilmis;
        }

        if (FaturalanmisMi)
        {
            return IrsaliyeHatalari.FaturalanmisDegistirilemez;
        }

        return bilgiler.Tur != Tur ? IrsaliyeHatalari.TurDegistirilemez : Ayarla(bilgiler);
    }

    public Sonuc Onayla(string? verilenNumara, DateTime simdi)
    {
        if (Durum != IrsaliyeDurumu.Taslak)
        {
            return Durum == IrsaliyeDurumu.Onayli ? IrsaliyeHatalari.ZatenOnayli : IrsaliyeHatalari.IptalEdilmis;
        }

        if (GidenMi)
        {
            Numara ??= verilenNumara;
        }

        if (Numara is null)
        {
            return IrsaliyeHatalari.NumaraGerekli;
        }

        Durum = IrsaliyeDurumu.Onayli;
        OnayTarihi = simdi;
        return Sonuc.Basarili();
    }

    public Sonuc IptalEt(DateTime simdi)
    {
        if (Durum != IrsaliyeDurumu.Onayli)
        {
            return Durum == IrsaliyeDurumu.Taslak ? IrsaliyeHatalari.TaslakIptalEdilemez : IrsaliyeHatalari.IptalEdilmis;
        }

        if (FaturalanmisMi)
        {
            return IrsaliyeHatalari.FaturalanmisDegistirilemez;
        }

        Durum = IrsaliyeDurumu.Iptal;
        IptalTarihi = simdi;
        return Sonuc.Basarili();
    }

    // Faturalanan miktar, bu satıra bağlı onaylı fatura satırlarının toplamıdır; fatura onaylanınca, iptal edilince
    // ya da düzenlenince bu toplamdan yeniden ayarlanır (tek doğru kaynak fatura satırlarıdır).
    public Sonuc FaturalananAyarla(Guid satirId, decimal toplam)
    {
        if (_satirlar.FirstOrDefault(s => s.Id == satirId) is not { } satir)
        {
            return IrsaliyeHatalari.SatirBulunamadi;
        }

        if (toplam > 0m && (Durum != IrsaliyeDurumu.Onayli || !IrsaliyeTurleri.FaturalanirMi(Tur)))
        {
            return IrsaliyeHatalari.FaturalanamazDurum;
        }

        return satir.FaturalananAyarla(toplam);
    }

    private Sonuc Ayarla(IrsaliyeBilgileri b)
    {
        if (b.Satirlar.Count == 0)
        {
            return IrsaliyeHatalari.SatirGerekli;
        }

        if (b.SevkTarihi < b.Tarih)
        {
            return IrsaliyeHatalari.SevkTarihtenOnceOlamaz;
        }

        if (b.Tur == IrsaliyeTuru.DepoSevk)
        {
            if (b.Cari is not null)
            {
                return IrsaliyeHatalari.DepoSevkindeCariOlmaz;
            }

            if (b.HedefDepoId is not { } hedef)
            {
                return IrsaliyeHatalari.HedefDepoGerekli;
            }

            if (b.Satirlar.Any(s => s.DepoId == hedef))
            {
                return IrsaliyeHatalari.HedefDepoAyni;
            }

            if (string.IsNullOrWhiteSpace(b.TeslimAdresi))
            {
                return IrsaliyeHatalari.TeslimAdresiGerekli;
            }
        }
        else if (b.Cari is null)
        {
            return IrsaliyeHatalari.CariGerekli;
        }

        if (b.Tur == IrsaliyeTuru.Alis && b.Satirlar.Any(s => s.BirimFiyat is null))
        {
            return IrsaliyeHatalari.MaliyetGerekli;
        }

        // Bizim düzenlediğimiz irsaliyede malı kimin taşıdığı belli olmalı: kendi aracımızla plaka + şoför, ya da
        // taşıyıcı firmayla (kargo/nakliye) unvan + VKN. Gelen irsaliyede bu bilgi karşı tarafın belgesindedir.
        if (IrsaliyeTurleri.GidenMi(b.Tur) && !TasimaBilgisiTamMi(b))
        {
            return IrsaliyeHatalari.TasimaBilgisiGerekli;
        }

        Tarih = b.Tarih;
        SevkTarihi = b.SevkTarihi;
        SevkSaati = b.SevkSaati;
        if (!GidenMi)
        {
            Numara = b.Numara;
        }

        var c = b.Cari;
        CariHesapId = c?.CariHesapId;
        CariKodu = c?.Kod;
        CariUnvan = c?.Unvan ?? b.DepoSevkUnvani ?? "";
        VergiNo = c?.VergiNo;
        VergiDairesi = c?.VergiDairesi;
        Il = c?.Il;
        Ilce = c?.Ilce;
        Adres = c?.Adres;
        ParaBirimi = b.ParaBirimi;
        Kur = b.Kur;

        HedefDepoId = b.Tur == IrsaliyeTuru.DepoSevk ? b.HedefDepoId : null;
        TeslimAdresi = b.TeslimAdresi;
        AracPlakasi = b.AracPlakasi;
        SoforAdi = b.SoforAdi;
        SoforTckn = b.SoforTckn;
        TasiyiciUnvani = b.TasiyiciUnvani;
        TasiyiciVkn = b.TasiyiciVkn;
        Aciklama = b.Aciklama;

        _satirlar.Clear();
        for (var i = 0; i < b.Satirlar.Count; i++)
        {
            _satirlar.Add(IrsaliyeSatiri.Olustur(Id, i + 1, b.Satirlar[i], b.Tur == IrsaliyeTuru.DepoSevk));
        }

        return Sonuc.Basarili();
    }

    private static bool TasimaBilgisiTamMi(IrsaliyeBilgileri b) =>
        (Dolu(b.AracPlakasi) && Dolu(b.SoforAdi) && Dolu(b.SoforTckn)) || (Dolu(b.TasiyiciUnvani) && Dolu(b.TasiyiciVkn));

    private static bool Dolu(string? deger) => !string.IsNullOrWhiteSpace(deger);
}

internal sealed class IrsaliyeSatiri : BaseEntity
{
    private IrsaliyeSatiri() { }

    public Guid IrsaliyeId { get; private set; }
    public int Sira { get; private set; }
    public Guid StokKartiId { get; private set; }
    public string StokKodu { get; private set; } = null!;
    public string Ad { get; private set; } = null!;
    public Guid DepoId { get; private set; }
    public string Birim { get; private set; } = null!;
    public decimal Miktar { get; private set; }

    // KDV hariç, irsaliyenin dövizinde. Alış irsaliyesinde stoğa giriş için tahmini maliyettir.
    public decimal? BirimFiyat { get; private set; }

    // Faturaya aktarılırken önerilen KDV oranı (kartın oranı).
    public int KdvOrani { get; private set; }
    public decimal FaturalananMiktar { get; private set; }
    public Guid? SiparisSatiriId { get; private set; }
    public string? LotNo { get; private set; }
    public DateOnly? SonKullanmaTarihi { get; private set; }
    public List<string>? SeriNolari { get; private set; }

    public decimal KalanMiktar => IrsaliyeHesaplari.Kalan(Miktar, FaturalananMiktar);

    public static IrsaliyeSatiri Olustur(Guid irsaliyeId, int sira, IrsaliyeSatiriBilgisi b, bool depoSevkMi) => new()
    {
        IrsaliyeId = irsaliyeId,
        Sira = sira,
        StokKartiId = b.StokKartiId,
        StokKodu = b.StokKodu,
        Ad = b.Ad,
        DepoId = b.DepoId,
        Birim = b.Birim,
        Miktar = b.Miktar,
        BirimFiyat = depoSevkMi ? null : b.BirimFiyat,
        KdvOrani = b.KdvOrani,
        SiparisSatiriId = depoSevkMi ? null : b.SiparisSatiriId,
        LotNo = IzlemeAlanlari.Lot(b.LotNo),
        SonKullanmaTarihi = b.SonKullanmaTarihi,
        SeriNolari = IzlemeAlanlari.Seriler(b.SeriNolari)
    };

    public Sonuc FaturalananAyarla(decimal toplam)
    {
        if (toplam < 0m || toplam > Miktar)
        {
            return IrsaliyeHatalari.KalanAsildi;
        }

        FaturalananMiktar = toplam;
        return Sonuc.Basarili();
    }
}

internal sealed record IrsaliyeSatiriBilgisi(
    Guid StokKartiId,
    string StokKodu,
    string Ad,
    Guid DepoId,
    string Birim,
    decimal Miktar,
    decimal? BirimFiyat,
    int KdvOrani,
    Guid? SiparisSatiriId = null,
    string? LotNo = null,
    DateOnly? SonKullanmaTarihi = null,
    IReadOnlyList<string>? SeriNolari = null);

// İstek, cari/stok/depo bilgileriyle çözümlendikten sonra entity'ye verilen hali. Depo sevkinde Cari boştur;
// "alıcı" olarak hedef deponun adı yazılır.
internal sealed record IrsaliyeBilgileri(
    IrsaliyeTuru Tur,
    FaturaCarisi? Cari,
    string? DepoSevkUnvani,
    string ParaBirimi,
    decimal Kur,
    DateOnly Tarih,
    DateOnly SevkTarihi,
    TimeOnly SevkSaati,
    string? Numara,
    Guid? HedefDepoId,
    string? TeslimAdresi,
    string? AracPlakasi,
    string? SoforAdi,
    string? SoforTckn,
    string? TasiyiciUnvani,
    string? TasiyiciVkn,
    string? Aciklama,
    IReadOnlyList<IrsaliyeSatiriBilgisi> Satirlar);

// Belge satırındaki lot/seri girdisinin saklanacak biçimi; kartın izleme kuralları stok hareketi yazılırken uygulanır.
internal static class IzlemeAlanlari
{
    public static string? Lot(string? lot) => string.IsNullOrWhiteSpace(lot) ? null : lot.Trim().ToUpperInvariant();

    public static List<string>? Seriler(IReadOnlyList<string>? seriler) =>
        seriler?.Select(s => s.Trim()).Where(s => s.Length > 0).Distinct(StringComparer.Ordinal).ToList() is { Count: > 0 } liste ? liste : null;
}
