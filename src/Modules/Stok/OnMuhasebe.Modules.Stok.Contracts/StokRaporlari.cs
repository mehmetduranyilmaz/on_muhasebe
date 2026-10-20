using System.Text.Json.Serialization;
using OnMuhasebe.Shared.Contracts.Sayfalama;

namespace OnMuhasebe.Modules.Stok.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<MaliyetYontemi>))]
public enum MaliyetYontemi
{
    AgirlikliOrtalama,
    Fifo,
    SonAlisFiyati
}

[JsonConverter(typeof(JsonStringEnumConverter<EksiStokDavranisi>))]
public enum EksiStokDavranisi
{
    IzinVer,
    Uyar,
    Engelle
}

public sealed record StokAyarlari(MaliyetYontemi MaliyetYontemi, EksiStokDavranisi EksiStokDavranisi);

// Ürün hareket dökümü. Miktarlar temel birimde; tutarlar TL ve seçili maliyet yöntemine göre.
public sealed record StokEkstreSatiri(
    Guid HareketId,
    DateOnly Tarih,
    StokHareketTuru Tur,
    string DepoAdi,
    string? BelgeNo,
    string? Aciklama,
    decimal Giris,
    decimal Cikis,
    decimal Bakiye,
    decimal BirimMaliyet,
    decimal Tutar,
    Guid? KaynakBelgeId);

public sealed record StokEkstresi(
    Guid StokKartiId,
    string Kod,
    string Ad,
    string TemelBirim,
    Guid? DepoId,
    DateOnly Baslangic,
    DateOnly Bitis,
    MaliyetYontemi MaliyetYontemi,
    decimal DevirMiktar,
    IReadOnlyList<StokEkstreSatiri> Satirlar,
    decimal ToplamGiris,
    decimal ToplamCikis,
    decimal SonMiktar,
    decimal SonBirimMaliyet,
    decimal SonDeger);

public sealed record StokDurumuSatiri(
    Guid StokKartiId,
    string Kod,
    string Ad,
    string TemelBirim,
    string? GrupAdi,
    decimal Miktar,
    decimal BirimMaliyet,
    decimal Deger,
    decimal? KritikSeviye)
{
    public bool KritikMi => KritikSeviye is { } seviye && Miktar <= seviye;
    public bool EksiMi => Miktar < 0;
}

public sealed record StokDurumuCevabi(
    DateOnly Tarih,
    Guid? DepoId,
    MaliyetYontemi MaliyetYontemi,
    SayfaliSonuc<StokDurumuSatiri> Satirlar,
    decimal ToplamDeger,
    int KritikSayisi,
    int EksiSayisi);

public sealed record KritikStok(Guid Id, string Ad, string TemelBirim, decimal Miktar, decimal KritikSeviye);

public sealed record SonStokHareketi(Guid StokKartiId, string Ad, string TemelBirim, DateOnly Tarih, StokHareketTuru Tur, decimal Giris, decimal Cikis);

public sealed record DepoDegeri(string DepoAdi, decimal Deger);

public sealed record StokOzetRaporu(
    int UrunSayisi,
    decimal ToplamDeger,
    int KritikSayisi,
    int EksiSayisi,
    MaliyetYontemi MaliyetYontemi,
    IReadOnlyList<DepoDegeri> DepoDegerleri,
    IReadOnlyList<KritikStok> KritikStoklar,
    IReadOnlyList<SonStokHareketi> SonHareketler);
