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

// Ana sayfa özet paneli. Tutarlar TL; döviz bakiyeler bugünkü kurla değerlenir (kur yoksa kayıtlı TL).
// Alacaklarımız: bize borçlu carilerin toplamı. Borçlarımız: alacaklı carilere borcumuz.
public sealed record CariOzetRaporu(
    int AktifCariSayisi,
    int RiskAsanCariSayisi,
    decimal AlacaklarimizTl,
    decimal BorclarimizTl,
    IReadOnlyList<DovizPozisyonu> DovizPozisyonlari,
    IReadOnlyList<OzetCari> EnCokBorcluCariler,
    IReadOnlyList<SonCariHareketi> SonHareketler);

public sealed record DovizPozisyonu(string ParaBirimi, decimal Bakiye, decimal GuncelTl);

public sealed record OzetCari(Guid Id, string Unvan, string ParaBirimi, decimal Bakiye, decimal GuncelTl);

public sealed record SonCariHareketi(
    Guid CariId, string CariUnvan, DateOnly Tarih, CariHareketTuru Tur, string ParaBirimi, decimal Borc, decimal Alacak, string? Aciklama);
