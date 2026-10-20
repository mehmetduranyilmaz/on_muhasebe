using System.Text.Json.Serialization;

namespace OnMuhasebe.Modules.Cari.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<CariHareketTuru>))]
public enum CariHareketTuru
{
    Acilis,
    Dekont,
    SatisFaturasi,
    AlisFaturasi,
    Tahsilat,
    Odeme
}

[JsonConverter(typeof(JsonStringEnumConverter<HareketYonu>))]
public enum HareketYonu
{
    Borc,
    Alacak
}

// Elle girilebilen hareketler: açılış bakiyesi ve borç/alacak dekontu. Kur TRY carilerde yok sayılır.
public sealed record CariHareketKaydetIstegi(
    CariHareketTuru Tur,
    HareketYonu Yon,
    DateOnly Tarih,
    DateOnly? VadeTarihi,
    decimal Tutar,
    decimal? Kur,
    string? BelgeNo,
    string? Aciklama);

public sealed record CariHareketSatiri(
    Guid Id,
    DateOnly Tarih,
    DateOnly? VadeTarihi,
    CariHareketTuru Tur,
    string? BelgeNo,
    string? Aciklama,
    decimal Borc,
    decimal Alacak,
    decimal Kur,
    decimal BorcTl,
    decimal AlacakTl,
    bool IptalEdildiMi,
    bool IptalKaydiMi,
    bool ElleIptalEdilebilirMi);
