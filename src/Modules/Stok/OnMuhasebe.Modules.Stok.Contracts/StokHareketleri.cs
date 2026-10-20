using System.Text.Json.Serialization;

namespace OnMuhasebe.Modules.Stok.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<StokHareketTuru>))]
public enum StokHareketTuru
{
    Acilis,
    SayimFazlasi,
    SayimEksigi,
    Fire,
    Sarf,
    TransferGiris,
    TransferCikis,
    AlisFaturasi,
    SatisFaturasi
}

// Elle girilen hareket. Miktar ve birim fiyat, seçilen birim (temel ya da ek birim) üzerindendir.
// BirimFiyat giriş hareketlerinde maliyettir (TL); çıkışlarda gönderilmez.
public sealed record StokHareketKaydetIstegi(
    StokHareketTuru Tur,
    Guid? DepoId,
    DateOnly Tarih,
    string Birim,
    decimal Miktar,
    decimal? BirimFiyat,
    string? BelgeNo,
    string? Aciklama);

public sealed record TransferIstegi(
    Guid KaynakDepoId,
    Guid HedefDepoId,
    DateOnly Tarih,
    string Birim,
    decimal Miktar,
    string? Aciklama);

// Miktarlar temel birimdedir; girilen birim ve miktar ayrıca gösterim için tutulur.
public sealed record StokHareketSatiri(
    Guid Id,
    DateOnly Tarih,
    StokHareketTuru Tur,
    string DepoAdi,
    decimal Giris,
    decimal Cikis,
    string GirilenBirim,
    decimal GirilenMiktar,
    decimal? BirimFiyat,
    string? BelgeNo,
    string? Aciklama,
    bool IptalEdildiMi,
    bool IptalKaydiMi,
    bool ElleIptalEdilebilirMi);

// Eksi stok ayarı "Uyar" ise kayıt yapılır ve uyarı döner.
public sealed record StokHareketCevabi(IReadOnlyList<StokHareketSatiri> Hareketler, string? Uyari);
