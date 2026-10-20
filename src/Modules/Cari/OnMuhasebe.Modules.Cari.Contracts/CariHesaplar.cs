using System.Text.Json.Serialization;

namespace OnMuhasebe.Modules.Cari.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<CariTuru>))]
public enum CariTuru
{
    Musteri,
    Tedarikci,
    MusteriVeTedarikci
}

[JsonConverter(typeof(JsonStringEnumConverter<KisiTipi>))]
public enum KisiTipi
{
    Tuzel,
    Sahis
}

// Kod boş bırakılırsa otomatik verilir. Surum yalnızca güncellemede gönderilir (eşzamanlı düzenleme kontrolü).
public sealed record CariKaydetIstegi(
    string? Kod,
    string Unvan,
    CariTuru Tur,
    KisiTipi KisiTipi,
    string? VergiNo,
    string? VergiDairesi,
    string? Il,
    string? Ilce,
    string? Adres,
    string? Telefon,
    string? Eposta,
    string ParaBirimi,
    int? VadeGun,
    decimal? RiskLimiti,
    Guid? GrupId,
    string? YetkiliAd,
    string? YetkiliTelefon,
    string? Not,
    uint Surum = 0);

public sealed record CariOzeti(
    Guid Id,
    string Kod,
    string Unvan,
    CariTuru Tur,
    string? GrupAdi,
    string ParaBirimi,
    string? Telefon,
    bool AktifMi,
    decimal Bakiye,
    decimal BakiyeTl,
    decimal? RiskLimiti)
{
    public bool RiskAsildiMi => RiskLimiti is { } limit && Bakiye > limit;
}

public sealed record CariDetayi(
    Guid Id,
    string Kod,
    string Unvan,
    CariTuru Tur,
    KisiTipi KisiTipi,
    string? VergiNo,
    string? VergiDairesi,
    string? Il,
    string? Ilce,
    string? Adres,
    string? Telefon,
    string? Eposta,
    string ParaBirimi,
    int? VadeGun,
    decimal? RiskLimiti,
    Guid? GrupId,
    string? GrupAdi,
    string? YetkiliAd,
    string? YetkiliTelefon,
    string? Not,
    bool AktifMi,
    decimal Bakiye,
    decimal BakiyeTl,
    bool HareketVarMi,
    uint Surum);

public sealed record CariGrubu(Guid Id, string Ad, int CariSayisi);

public sealed record CariGrubuKaydetIstegi(string Ad);
