using System.Text.Json.Serialization;
using OnMuhasebe.Shared.Contracts.Sayfalama;

namespace OnMuhasebe.Modules.Cari.Contracts;

public sealed record EkstreSatiri(
    Guid HareketId,
    DateOnly Tarih,
    DateOnly? VadeTarihi,
    CariHareketTuru Tur,
    string? BelgeNo,
    string? Aciklama,
    decimal Borc,
    decimal Alacak,
    decimal Bakiye,
    decimal Kur,
    decimal BorcTl,
    decimal AlacakTl,
    decimal BakiyeTl);

// Bakiye pozitifse cari borçlu (bize borçlu), negatifse alacaklıdır.
public sealed record EkstreCevabi(
    Guid CariId,
    string Kod,
    string Unvan,
    string ParaBirimi,
    DateOnly Baslangic,
    DateOnly Bitis,
    decimal DevirBakiye,
    decimal DevirBakiyeTl,
    IReadOnlyList<EkstreSatiri> Satirlar,
    decimal ToplamBorc,
    decimal ToplamAlacak,
    decimal SonBakiye,
    decimal ToplamBorcTl,
    decimal ToplamAlacakTl,
    decimal SonBakiyeTl);

[JsonConverter(typeof(JsonStringEnumConverter<BakiyeDurumu>))]
public enum BakiyeDurumu
{
    Tumu,
    Borclu,
    Alacakli,
    SifirHaric
}

// GuncelTl: döviz bakiyenin rapor tarihindeki kurla TL değeri; KurFarki = GuncelTl - BakiyeTl.
public sealed record BakiyeSatiri(
    Guid CariId,
    string Kod,
    string Unvan,
    string ParaBirimi,
    decimal Borc,
    decimal Alacak,
    decimal Bakiye,
    decimal BakiyeTl,
    decimal? GuncelKur,
    decimal? GuncelTl,
    decimal? KurFarki);

public sealed record ParaBirimiToplami(
    string ParaBirimi,
    decimal Borc,
    decimal Alacak,
    decimal Bakiye,
    decimal BakiyeTl,
    decimal? GuncelTl,
    decimal? KurFarki);

public sealed record BakiyeListesiCevabi(
    DateOnly Tarih,
    SayfaliSonuc<BakiyeSatiri> Satirlar,
    IReadOnlyList<ParaBirimiToplami> Toplamlar);
