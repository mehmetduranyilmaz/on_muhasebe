using System.Text.Json.Serialization;

namespace OnMuhasebe.Modules.Stok.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<StokTuru>))]
public enum StokTuru
{
    Urun,
    // Faturada kullanılır ama stok hareketi oluşturmaz (danışmanlık, nakliye vb.).
    Hizmet
}

// Ek birim: 1 {BirimKodu} = {Katsayi} temel birim (ör. 1 KOLI = 12 ADET).
public sealed record EkBirim(string BirimKodu, decimal Katsayi, string? Barkod);

// Kod boş bırakılırsa otomatik verilir. Fiyatlar temel birim içindir. Surum yalnızca güncellemede gönderilir.
public sealed record StokKartiKaydetIstegi(
    string? Kod,
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
    IReadOnlyList<EkBirim>? EkBirimler,
    uint Surum = 0);

public sealed record StokKartiOzeti(
    Guid Id,
    string Kod,
    string Ad,
    StokTuru Tur,
    string TemelBirim,
    string? Barkod,
    string? GrupAdi,
    decimal Miktar,
    decimal? KritikSeviye,
    decimal? SatisFiyati,
    decimal? PerakendeFiyati,
    string ParaBirimi,
    bool AktifMi)
{
    public bool KritikMi => Tur == StokTuru.Urun && KritikSeviye is { } seviye && Miktar <= seviye;
    public bool EksiMi => Miktar < 0;
}

public sealed record DepoMiktari(Guid DepoId, string DepoAdi, decimal Miktar);

public sealed record StokKartiDetayi(
    Guid Id,
    string Kod,
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
    string? GrupAdi,
    decimal? KritikSeviye,
    string? Aciklama,
    bool AktifMi,
    IReadOnlyList<EkBirim> EkBirimler,
    IReadOnlyList<DepoMiktari> DepoMiktarlari,
    decimal Miktar,
    decimal BirimMaliyet,
    decimal StokDegeri,
    MaliyetYontemi MaliyetYontemi,
    bool HareketVarMi,
    uint Surum);

// Barkod okutma: barkod kartın kendisine ya da ek birimlerinden birine ait olabilir.
public sealed record BarkodSonucu(Guid StokKartiId, string Kod, string Ad, string Birim, decimal Katsayi);

public sealed record Depo(Guid Id, string Ad, bool VarsayilanMi, bool AktifMi);

public sealed record DepoKaydetIstegi(string Ad);
