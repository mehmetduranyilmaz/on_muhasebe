using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Shared.Contracts.Birimler;
using OnMuhasebe.Shared.Domain.Abstractions;
using OnMuhasebe.Shared.Domain.Entities;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Stok.Domain;

internal sealed class StokKarti : FirmaEntity, ISilinebilir
{
    private readonly List<StokKartiBirimi> _birimler = [];

    private StokKarti() { }

    public string Kod { get; private set; } = null!;
    public string Ad { get; private set; } = null!;
    public StokTuru Tur { get; private set; }
    public string TemelBirim { get; private set; } = null!;
    public string? Barkod { get; private set; }
    public int KdvOrani { get; private set; }
    public decimal? AlisFiyati { get; private set; }
    public decimal? SatisFiyati { get; private set; }

    // Perakende satış fiyatı KDV dahildir; SatisFiyati toptan (KDV hariç) fiyattır.
    public decimal? PerakendeFiyati { get; private set; }
    public string ParaBirimi { get; private set; } = null!;
    public Guid? GrupId { get; private set; }
    public decimal? KritikSeviye { get; private set; }
    public string? Aciklama { get; private set; }
    public IzlemeTuru Izleme { get; private set; }

    // Son kullanma tarihi lot/seri girişinde zorunlu tutulur (gıda, ilaç, kozmetik).
    public bool SktTakibi { get; private set; }
    public bool AktifMi { get; private set; } = true;
    public bool SilindiMi { get; private set; }
    public DateTime? SilinmeTarihi { get; private set; }

    public StokGrubu? Grup { get; private set; }
    public IReadOnlyCollection<StokKartiBirimi> Birimler => _birimler;

    public static Sonuc<StokKarti> Olustur(string kod, StokKartiBilgileri bilgiler, IEnumerable<EkBirim> ekBirimler)
    {
        if (IzlemeHatasi(bilgiler) is { } izlemeHatasi)
        {
            return izlemeHatasi;
        }

        var kart = new StokKarti { Kod = kod, Tur = bilgiler.Tur, TemelBirim = bilgiler.TemelBirim };
        kart.BilgileriAyarla(bilgiler);
        var sonuc = kart.BirimleriAyarla(ekBirimler);
        return sonuc.BasariliMi ? kart : sonuc.Hata;
    }

    // Hareketi olan kartın temel birimi ve türü değişirse geçmiş miktarlar anlamını yitirir.
    public Sonuc Guncelle(string kod, StokKartiBilgileri bilgiler, IEnumerable<EkBirim> ekBirimler, bool hareketVarMi)
    {
        if (hareketVarMi && bilgiler.TemelBirim != TemelBirim)
        {
            return StokHatalari.TemelBirimDegistirilemez;
        }

        if (hareketVarMi && bilgiler.Tur != Tur)
        {
            return StokHatalari.TurDegistirilemez;
        }

        if (hareketVarMi && bilgiler.Izleme != Izleme)
        {
            return StokHatalari.IzlemeDegistirilemez;
        }

        if (IzlemeHatasi(bilgiler) is { } izlemeHatasi)
        {
            return izlemeHatasi;
        }

        var sonuc = BirimleriAyarla(ekBirimler, bilgiler.TemelBirim);
        if (!sonuc.BasariliMi)
        {
            return sonuc;
        }

        Kod = kod;
        Tur = bilgiler.Tur;
        TemelBirim = bilgiler.TemelBirim;
        BilgileriAyarla(bilgiler);
        return Sonuc.Basarili();
    }

    // 1 birim = kaç temel birim; kartta tanımlı değilse null.
    public decimal? Katsayi(string birim) =>
        birim == TemelBirim ? 1m : _birimler.FirstOrDefault(b => b.BirimKodu == birim)?.Katsayi;

    public void Pasiflestir() => AktifMi = false;

    public void Aktiflestir() => AktifMi = true;

    private Sonuc BirimleriAyarla(IEnumerable<EkBirim> ekBirimler, string? temelBirim = null)
    {
        var temel = temelBirim ?? TemelBirim;
        var liste = ekBirimler.ToList();
        if (liste.Any(b => !Shared.Contracts.Birimler.Birimler.GecerliMi(b.BirimKodu) || b.BirimKodu == temel || b.Katsayi <= 0)
            || liste.Select(b => b.BirimKodu).Distinct().Count() != liste.Count)
        {
            return StokHatalari.EkBirimGecersiz;
        }

        _birimler.Clear();
        _birimler.AddRange(liste.Select(b => StokKartiBirimi.Olustur(Id, b.BirimKodu, b.Katsayi, b.Barkod)));
        return Sonuc.Basarili();
    }

    private void BilgileriAyarla(StokKartiBilgileri b)
    {
        Ad = b.Ad;
        Barkod = b.Barkod;
        KdvOrani = b.KdvOrani;
        AlisFiyati = b.AlisFiyati;
        SatisFiyati = b.SatisFiyati;
        PerakendeFiyati = b.PerakendeFiyati;
        ParaBirimi = b.ParaBirimi;
        GrupId = b.GrupId;
        KritikSeviye = b.Tur == StokTuru.Hizmet ? null : b.KritikSeviye;
        Aciklama = b.Aciklama;
        Izleme = b.Tur == StokTuru.Urun ? b.Izleme : IzlemeTuru.Yok;
        SktTakibi = Izleme != IzlemeTuru.Yok && b.SktTakibi;
    }

    // Hizmet izlenmez; SKT bir lota ya da seriye bağlı olduğundan izleme olmadan tutulamaz.
    private static Hata? IzlemeHatasi(StokKartiBilgileri b) =>
        (b.Tur == StokTuru.Hizmet && b.Izleme != IzlemeTuru.Yok) || (b.SktTakibi && b.Izleme == IzlemeTuru.Yok) ? StokHatalari.IzlemeGecersiz : null;
}

internal sealed record StokKartiBilgileri(
    string Ad,
    StokTuru Tur,
    string TemelBirim,
    string? Barkod,
    int KdvOrani,
    decimal? AlisFiyati,
    decimal? SatisFiyati,
    decimal? PerakendeFiyati,
    string ParaBirimi,
    Guid? GrupId,
    decimal? KritikSeviye,
    string? Aciklama,
    IzlemeTuru Izleme = IzlemeTuru.Yok,
    bool SktTakibi = false)
{
    public static StokKartiBilgileri Istekten(StokKartiKaydetIstegi i) => new(
        i.Ad.Trim(), i.Tur, i.TemelBirim, Temiz(i.Barkod), i.KdvOrani, i.AlisFiyati, i.SatisFiyati, i.PerakendeFiyati, i.ParaBirimi,
        i.GrupId, i.KritikSeviye, Temiz(i.Aciklama), i.Izleme, i.SktTakibi);

    private static string? Temiz(string? deger) => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();
}

internal sealed class StokKartiBirimi : BaseEntity
{
    private StokKartiBirimi() { }

    public Guid StokKartiId { get; private set; }
    public string BirimKodu { get; private set; } = null!;
    public decimal Katsayi { get; private set; }
    public string? Barkod { get; private set; }

    public static StokKartiBirimi Olustur(Guid stokKartiId, string birimKodu, decimal katsayi, string? barkod) => new()
    {
        StokKartiId = stokKartiId,
        BirimKodu = birimKodu,
        Katsayi = katsayi,
        Barkod = string.IsNullOrWhiteSpace(barkod) ? null : barkod.Trim()
    };
}

internal sealed class StokGrubu : FirmaEntity
{
    private StokGrubu() { }

    public string Ad { get; private set; } = null!;

    public static StokGrubu Olustur(string ad) => new() { Ad = ad.Trim() };

    public void AdDegistir(string ad) => Ad = ad.Trim();
}

internal sealed class Depo : FirmaEntity
{
    public const string VarsayilanAd = "Merkez Depo";

    private Depo() { }

    public string Ad { get; private set; } = null!;
    public bool VarsayilanMi { get; private set; }
    public bool AktifMi { get; private set; } = true;

    // Depolar arası sevk irsaliyesinde teslim adresi ve yazdırmada gönderen/teslim yeri olarak kullanılır.
    public string? Adres { get; private set; }
    public string? Il { get; private set; }
    public string? Ilce { get; private set; }

    public static Depo Olustur(string ad, bool varsayilanMi = false) => new() { Ad = ad.Trim(), VarsayilanMi = varsayilanMi };

    public void AdDegistir(string ad) => Ad = ad.Trim();

    public void AdresDegistir(string? adres, string? il, string? ilce) => (Adres, Il, Ilce) = (Bos(adres), Bos(il), Bos(ilce));

    private static string? Bos(string? deger) => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();

    public void VarsayilanYap(bool varsayilanMi) => VarsayilanMi = varsayilanMi;
}

// Firma başına tek satır; kayıt yoksa varsayılanlar geçerlidir.
internal sealed class StokAyari : FirmaEntity
{
    private StokAyari() { }

    public MaliyetYontemi MaliyetYontemi { get; private set; } = MaliyetYontemi.AgirlikliOrtalama;
    public EksiStokDavranisi EksiStokDavranisi { get; private set; } = EksiStokDavranisi.Uyar;

    public static StokAyari Varsayilan() => new();

    public void Guncelle(MaliyetYontemi maliyetYontemi, EksiStokDavranisi eksiStokDavranisi)
    {
        MaliyetYontemi = maliyetYontemi;
        EksiStokDavranisi = eksiStokDavranisi;
    }
}
