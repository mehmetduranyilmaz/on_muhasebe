using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Shared.Domain.Entities;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Stok.Domain;

internal static class FiyatListesiHatalari
{
    public static readonly Hata Bulunamadi = Hata.Bulunamadi("fiyat_listesi_bulunamadi", "Fiyat listesi bulunamadı.");
    public static readonly Hata TarihAraligi = Hata.Dogrulama("fiyat_listesi_tarih", "Bitiş tarihi başlangıçtan önce olamaz.");
    public static readonly Hata TekrarlananSatir = Hata.Dogrulama("fiyat_listesi_satir_tekrar", "Aynı ürün aynı kademe eşiğiyle iki kez yazılamaz.");
    public static readonly Hata TekrarlananHedef = Hata.Dogrulama("fiyat_listesi_hedef_tekrar", "Aynı cari ya da grup listeye iki kez atanamaz.");
    public static readonly Hata KartBulunamadi = Hata.Dogrulama("fiyat_listesi_kart", "Listedeki ürünlerden biri bulunamadı.");
}

// Cariye ya da cari grubuna atanan, belirli tarih aralığında geçerli fiyat listesi. Satırlar kart başına kademeli olabilir.
// Muhasebe kaydı değildir (belgeye fiyat önerir); silinebilir ve geçmiş belgeleri etkilemez.
internal sealed class FiyatListesi : FirmaEntity
{
    private readonly List<FiyatListesiSatiri> _satirlar = [];
    private readonly List<FiyatListesiAtamasi> _atamalar = [];

    private FiyatListesi() { }

    public string Ad { get; private set; } = null!;
    public FiyatListesiTuru Tur { get; private set; }
    public string ParaBirimi { get; private set; } = null!;
    public bool KdvDahilMi { get; private set; }
    public DateOnly? BaslangicTarihi { get; private set; }
    public DateOnly? BitisTarihi { get; private set; }
    public bool AktifMi { get; private set; }
    public string? Aciklama { get; private set; }

    public IReadOnlyCollection<FiyatListesiSatiri> Satirlar => _satirlar;
    public IReadOnlyCollection<FiyatListesiAtamasi> Atamalar => _atamalar;

    public static Sonuc<FiyatListesi> Olustur(FiyatListesiKaydetIstegi istek)
    {
        var liste = new FiyatListesi();
        var sonuc = liste.Ayarla(istek);
        return sonuc.BasariliMi ? liste : sonuc.Hata;
    }

    public Sonuc Ayarla(FiyatListesiKaydetIstegi i)
    {
        if (i.BaslangicTarihi is { } bas && i.BitisTarihi is { } bit && bit < bas)
        {
            return FiyatListesiHatalari.TarihAraligi;
        }

        if (i.Satirlar.GroupBy(s => (s.StokKartiId, s.MinMiktar)).Any(g => g.Count() > 1))
        {
            return FiyatListesiHatalari.TekrarlananSatir;
        }

        if (i.Hedefler.GroupBy(h => (h.Tur, h.HedefId)).Any(g => g.Count() > 1))
        {
            return FiyatListesiHatalari.TekrarlananHedef;
        }

        Ad = i.Ad.Trim();
        Tur = i.Tur;
        ParaBirimi = i.ParaBirimi;
        KdvDahilMi = i.KdvDahilMi;
        BaslangicTarihi = i.BaslangicTarihi;
        BitisTarihi = i.BitisTarihi;
        AktifMi = i.AktifMi;
        Aciklama = string.IsNullOrWhiteSpace(i.Aciklama) ? null : i.Aciklama.Trim();

        _satirlar.Clear();
        _satirlar.AddRange(i.Satirlar.Select(s => new FiyatListesiSatiri(Id, s.StokKartiId, s.MinMiktar, s.Fiyat, s.Iskonto)));
        _atamalar.Clear();
        _atamalar.AddRange(i.Hedefler.Select(h => new FiyatListesiAtamasi(Id, h.Tur, h.HedefId, h.Ad.Trim())));
        return Sonuc.Basarili();
    }
}

internal sealed class FiyatListesiSatiri : BaseEntity
{
    private FiyatListesiSatiri() { }

    public FiyatListesiSatiri(Guid fiyatListesiId, Guid stokKartiId, decimal minMiktar, decimal fiyat, decimal iskonto) =>
        (FiyatListesiId, StokKartiId, MinMiktar, Fiyat, Iskonto) = (fiyatListesiId, stokKartiId, minMiktar, fiyat, iskonto);

    public Guid FiyatListesiId { get; private set; }
    public Guid StokKartiId { get; private set; }

    // Temel birimde kademe eşiği ve birim fiyat; iskonto yüzdedir (belgede satır iskontosu olarak gelir).
    public decimal MinMiktar { get; private set; }
    public decimal Fiyat { get; private set; }
    public decimal Iskonto { get; private set; }
}

internal sealed class FiyatListesiAtamasi : BaseEntity
{
    private FiyatListesiAtamasi() { }

    public FiyatListesiAtamasi(Guid fiyatListesiId, FiyatHedefTuru tur, Guid hedefId, string ad) =>
        (FiyatListesiId, Tur, HedefId, Ad) = (fiyatListesiId, tur, hedefId, ad);

    public Guid FiyatListesiId { get; private set; }
    public FiyatHedefTuru Tur { get; private set; }

    // Cari ya da cari grubu kimliği (Cari modülünde); modüller arası yabancı anahtar kurulmaz.
    public Guid HedefId { get; private set; }
    public string Ad { get; private set; } = null!;
}
