using System.Text.Json.Serialization;

namespace OnMuhasebe.Modules.Stok.Contracts;

// Fiyat listesi kart fiyatının önüne geçer: belgeye ürün eklenirken carinin geçerli listesindeki fiyat ve iskonto önerilir.
// Satış listesi müşterilere, alış listesi tedarikçilerin fiyatlarına (anlaşmalı alış fiyatı) karşılık gelir.
[JsonConverter(typeof(JsonStringEnumConverter<FiyatListesiTuru>))]
public enum FiyatListesiTuru
{
    Satis,
    Alis
}

[JsonConverter(typeof(JsonStringEnumConverter<FiyatHedefTuru>))]
public enum FiyatHedefTuru
{
    Cari,
    CariGrubu
}

// Listenin atandığı cari ya da cari grubu; Ad, atama anındaki unvan/grup adıdır (ekranda gösterim için).
// Hedefi olmayan liste geneldir: o türdeki tüm carilere uygulanır.
public sealed record FiyatListesiHedefi(FiyatHedefTuru Tur, Guid HedefId, string Ad);

// Fiyat ve kademe eşiği (MinMiktar) kartın temel birimindedir; belgede ek birim seçilirse katsayıyla çevrilir.
// Aynı karta farklı eşiklerle birden çok satır kademeli fiyattır (ör. 0+ → 10 TL, 100+ → 9 TL).
public sealed record FiyatListesiSatiriIstegi(Guid StokKartiId, decimal MinMiktar, decimal Fiyat, decimal Iskonto);

public sealed record FiyatListesiKaydetIstegi(
    string Ad,
    FiyatListesiTuru Tur,
    string ParaBirimi,
    bool KdvDahilMi,
    DateOnly? BaslangicTarihi,
    DateOnly? BitisTarihi,
    bool AktifMi,
    string? Aciklama,
    IReadOnlyList<FiyatListesiHedefi> Hedefler,
    IReadOnlyList<FiyatListesiSatiriIstegi> Satirlar,
    uint Surum = 0);

public sealed record FiyatListesiOzeti(
    Guid Id,
    string Ad,
    FiyatListesiTuru Tur,
    string ParaBirimi,
    bool KdvDahilMi,
    DateOnly? BaslangicTarihi,
    DateOnly? BitisTarihi,
    bool AktifMi,
    int HedefSayisi,
    int SatirSayisi,
    bool GecerliMi);

public sealed record FiyatListesiSatiriDetayi(
    Guid StokKartiId,
    string StokKodu,
    string StokAdi,
    string TemelBirim,
    decimal MinMiktar,
    decimal Fiyat,
    decimal Iskonto);

public sealed record FiyatListesiDetayi(
    Guid Id,
    string Ad,
    FiyatListesiTuru Tur,
    string ParaBirimi,
    bool KdvDahilMi,
    DateOnly? BaslangicTarihi,
    DateOnly? BitisTarihi,
    bool AktifMi,
    string? Aciklama,
    IReadOnlyList<FiyatListesiHedefi> Hedefler,
    IReadOnlyList<FiyatListesiSatiriDetayi> Satirlar,
    uint Surum);

public sealed record FiyatKademesi(decimal MinMiktar, decimal Fiyat, decimal Iskonto);

// Bir kart için uygulanacak liste ve kademeleri; belge, satırın miktarına göre kademeyi kendisi seçer (miktar değişince yeniden).
public sealed record UygulananFiyat(
    Guid StokKartiId,
    Guid FiyatListesiId,
    string ListeAdi,
    string ParaBirimi,
    bool KdvDahilMi,
    IReadOnlyList<FiyatKademesi> Kademeler);

public static class FiyatListesiKurallari
{
    public const int AzamiSatirSayisi = 5000;

    public static bool GecerliMi(bool aktifMi, DateOnly? baslangic, DateOnly? bitis, DateOnly tarih) =>
        aktifMi && (baslangic is null || baslangic <= tarih) && (bitis is null || tarih <= bitis);

    // Carinin listeye uygunluğu ve önceliği: cariye özel (2) > cari grubuna (1) > genel liste (0); uygun değilse null.
    // Özel anlaşma genel fiyatın önüne geçer — piyasadaki ön muhasebe programlarının ortak kuralı.
    public static int? Oncelik(IReadOnlyCollection<FiyatListesiHedefi> hedefler, Guid? cariId, Guid? cariGrupId)
    {
        if (hedefler.Count == 0)
        {
            return 0;
        }

        if (cariId is not null && hedefler.Any(h => h.Tur == FiyatHedefTuru.Cari && h.HedefId == cariId))
        {
            return 2;
        }

        if (cariGrupId is not null && hedefler.Any(h => h.Tur == FiyatHedefTuru.CariGrubu && h.HedefId == cariGrupId))
        {
            return 1;
        }

        return null;
    }

    // Miktarın ulaştığı en yüksek eşik; hiçbir eşiğe ulaşılmıyorsa liste bu miktar için fiyat vermez.
    public static FiyatKademesi? Kademe(IEnumerable<FiyatKademesi> kademeler, decimal temelMiktar) =>
        kademeler.Where(k => k.MinMiktar <= temelMiktar).MaxBy(k => k.MinMiktar);
}
