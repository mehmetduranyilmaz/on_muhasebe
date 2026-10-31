using System.Text.Json.Serialization;

namespace OnMuhasebe.Modules.Cari.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<CariHareketTuru>))]
public enum CariHareketTuru
{
    Acilis,
    Dekont,
    SatisFaturasi,
    AlisFaturasi,
    SatistanIadeFaturasi,
    AlistanIadeFaturasi,
    Tahsilat,
    Odeme,
    // Kasa/banka modülünden gelir/gider tahakkuku (ör. kira bedeli cariye borç yazılır, ödeme sonra yapılır).
    Gelir,
    Gider,
    // Çek/senet modülünden: alınan evrak (alacak), verilen ya da ciro edilen evrak (borç), iadeler ve karşılıksız geri yansıma.
    CekSenetGirisi,
    CekSenetCikisi,
    CekSenetIadesi,
    KarsiliksizCekSenet,
    // Fatura modülünden: gider pusulası ve serbest meslek makbuzu (ikisi de cariyi alacaklandırır, biz borçlanırız).
    GiderPusulasi,
    SerbestMeslekMakbuzu
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
    bool ElleIptalEdilebilirMi,
    Guid? KaynakBelgeId);
