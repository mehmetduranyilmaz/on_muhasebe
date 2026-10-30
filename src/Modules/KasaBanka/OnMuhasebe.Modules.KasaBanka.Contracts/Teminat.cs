using System.Text.Json.Serialization;

namespace OnMuhasebe.Modules.KasaBanka.Contracts;

// Teminat türü. TeminatMektubu: bankadan alınan/verilen mektup; Ipotek/Rehin: taşınmaz/menkul üzerinde; Kefalet: kefil;
// NakitTeminat: bloke nakit; TeminatCeki/TeminatSenedi: teminat amaçlı evrak (portföydeki çek/senetten ayrıdır, burada sadece kaydı tutulur).
[JsonConverter(typeof(JsonStringEnumConverter<TeminatTuru>))]
public enum TeminatTuru
{
    TeminatMektubu,
    Ipotek,
    Rehin,
    Kefalet,
    NakitTeminat,
    TeminatCeki,
    TeminatSenedi,
    Diger
}

// Alinan: lehimize alınan teminat; Verilen: karşı tarafa verdiğimiz teminat.
[JsonConverter(typeof(JsonStringEnumConverter<TeminatYonu>))]
public enum TeminatYonu
{
    Alinan,
    Verilen
}

// Aktif: yürürlükte; IadeEdildi: serbest bırakıldı/geri alındı; Irat: teminat nakde çevrildi/irat kaydedildi.
[JsonConverter(typeof(JsonStringEnumConverter<TeminatDurumu>))]
public enum TeminatDurumu
{
    Aktif,
    IadeEdildi,
    Irat
}

public sealed record TeminatOzeti(
    Guid Id,
    TeminatTuru Tur,
    TeminatYonu Yon,
    string TarafAdi,
    string? ReferansNo,
    decimal Tutar,
    string ParaBirimi,
    DateOnly VerilisTarihi,
    DateOnly? VadeTarihi,
    TeminatDurumu Durum);

public sealed record TeminatDetayi(
    Guid Id,
    TeminatTuru Tur,
    TeminatYonu Yon,
    string TarafAdi,
    string? ReferansNo,
    decimal Tutar,
    string ParaBirimi,
    DateOnly VerilisTarihi,
    DateOnly? VadeTarihi,
    DateOnly? IadeTarihi,
    TeminatDurumu Durum,
    string? Aciklama,
    uint Surum);

// Taraf: teminat mektubunda muhatap/lehtar, ipotek/rehinde karşı taraf, kefalette kefil vb. Surum yalnızca güncellemede gönderilir.
public sealed record TeminatKaydetIstegi(
    TeminatTuru Tur,
    TeminatYonu Yon,
    string TarafAdi,
    string? ReferansNo,
    decimal Tutar,
    string ParaBirimi,
    DateOnly VerilisTarihi,
    DateOnly? VadeTarihi,
    string? Aciklama,
    uint Surum = 0);

// Durum değişikliği: iade/irat tarihini taşır (Aktif'e dönüşte tarih temizlenir).
public sealed record TeminatDurumIstegi(TeminatDurumu Durum, DateOnly? IadeTarihi);
