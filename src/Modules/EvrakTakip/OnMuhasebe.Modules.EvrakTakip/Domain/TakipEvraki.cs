using OnMuhasebe.Modules.EvrakTakip.Contracts;
using OnMuhasebe.Shared.Domain.Abstractions;
using OnMuhasebe.Shared.Domain.Entities;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.EvrakTakip.Domain;

internal static class TakipHatalari
{
    public static readonly Hata Bulunamadi = Hata.Bulunamadi("takip_evraki_bulunamadi", "Evrak bulunamadı.");
    public static readonly Hata BitisBaslangictanOnce = Hata.Dogrulama("bitis_tarihi", "Bitiş tarihi başlangıç tarihinden önce olamaz.");
    public static readonly Hata YenilemeTarihi = Hata.IsKurali("yenileme_tarihi", "Yeni bitiş tarihi mevcut bitiş tarihinden sonra olmalıdır.");
    public static readonly Hata PasifYenilenemez = Hata.IsKurali("pasif_evrak", "Pasif evrak yenilenemez; önce aktifleştirin.");
    public static readonly Hata SorumluGecersiz = Hata.Dogrulama("sorumlu_gecersiz", "Sorumlu kişi bu firmanın aktif kullanıcısı olmalıdır.");
}

// Bitiş tarihi izlenen evrak (ruhsat, poliçe, sözleşme...). Dosyası Dosya modülünde bu kayda eklenir.
internal sealed class TakipEvraki : FirmaEntity, ISilinebilir, IIsyeriVarligi
{
    private readonly List<TakipEvrakiYenilemesi> _yenilemeler = [];

    private TakipEvraki() { }

    public string Ad { get; private set; } = null!;
    public TakipEvrakTuru Tur { get; private set; }
    public string? BelgeNo { get; private set; }
    public string? Kurum { get; private set; }
    public DateOnly? BaslangicTarihi { get; private set; }
    public DateOnly BitisTarihi { get; private set; }
    public int HatirlatmaGun { get; private set; }
    public Guid? SorumluKullaniciId { get; private set; }
    public Guid? IsyeriId { get; private set; }
    public string? Aciklama { get; private set; }
    public bool AktifMi { get; private set; } = true;
    public bool SilindiMi { get; private set; }
    public DateTime? SilinmeTarihi { get; private set; }

    public IReadOnlyList<TakipEvrakiYenilemesi> Yenilemeler => _yenilemeler;

    public static Sonuc<TakipEvraki> Olustur(TakipEvrakiKaydetIstegi i)
    {
        var evrak = new TakipEvraki();
        var sonuc = evrak.Guncelle(i);
        return sonuc.BasariliMi ? evrak : sonuc.Hata;
    }

    public Sonuc Guncelle(TakipEvrakiKaydetIstegi i)
    {
        if (i.BaslangicTarihi is { } baslangic && i.BitisTarihi < baslangic)
        {
            return TakipHatalari.BitisBaslangictanOnce;
        }

        Ad = i.Ad.Trim();
        Tur = i.Tur;
        BelgeNo = Temiz(i.BelgeNo);
        Kurum = Temiz(i.Kurum);
        BaslangicTarihi = i.BaslangicTarihi;
        BitisTarihi = i.BitisTarihi;
        HatirlatmaGun = i.HatirlatmaGun;
        SorumluKullaniciId = i.SorumluKullaniciId;
        IsyeriId = i.IsyeriId;
        Aciklama = Temiz(i.Aciklama);
        return Sonuc.Basarili();
    }

    public Sonuc<TakipEvrakiYenilemesi> Yenile(TakipYenilemeIstegi i, string? yapan)
    {
        if (!AktifMi)
        {
            return TakipHatalari.PasifYenilenemez;
        }

        if (i.YeniBitisTarihi <= BitisTarihi)
        {
            return TakipHatalari.YenilemeTarihi;
        }

        if (i.YeniBaslangicTarihi is { } baslangic && i.YeniBitisTarihi < baslangic)
        {
            return TakipHatalari.BitisBaslangictanOnce;
        }

        var yenileme = TakipEvrakiYenilemesi.Olustur(Id, BitisTarihi, i.YeniBitisTarihi, BelgeNo, Temiz(i.Not), yapan);
        _yenilemeler.Add(yenileme);
        BitisTarihi = i.YeniBitisTarihi;
        BaslangicTarihi = i.YeniBaslangicTarihi ?? BaslangicTarihi;
        BelgeNo = Temiz(i.YeniBelgeNo) ?? BelgeNo;
        return yenileme;
    }

    public void Pasiflestir() => AktifMi = false;

    public void Aktiflestir() => AktifMi = true;

    private static string? Temiz(string? deger) => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();
}

internal sealed class TakipEvrakiYenilemesi : FirmaEntity
{
    private TakipEvrakiYenilemesi() { }

    public Guid TakipEvrakiId { get; private set; }
    public DateOnly EskiBitisTarihi { get; private set; }
    public DateOnly YeniBitisTarihi { get; private set; }
    public string? EskiBelgeNo { get; private set; }
    public string? Not { get; private set; }
    public string? Yapan { get; private set; }

    public static TakipEvrakiYenilemesi Olustur(Guid evrakId, DateOnly eski, DateOnly yeni, string? eskiBelgeNo, string? not, string? yapan) => new()
    {
        TakipEvrakiId = evrakId,
        EskiBitisTarihi = eski,
        YeniBitisTarihi = yeni,
        EskiBelgeNo = eskiBelgeNo,
        Not = not,
        Yapan = yapan
    };
}
