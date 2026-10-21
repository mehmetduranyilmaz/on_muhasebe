using System.Text.Json.Serialization;

namespace OnMuhasebe.Modules.Yonetim.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<OdemeBildirimiDurumu>))]
public enum OdemeBildirimiDurumu
{
    Bekliyor,
    Onaylandi,
    Reddedildi
}

// Abonelik ödemesinin yapılacağı banka hesabı (sistem yöneticisi girer, firmalar abonelik ekranında görür).
public sealed record OdemeBilgileri(string? Alici, string? Banka, string? Iban, string? Not);

// AySayisi: 1 (aylık) ya da 12 (yıllık); paketin o süre için ücreti tanımlı olmalı.
public sealed record OdemeBildirimiIstegi(string PaketKodu, int AySayisi, decimal Tutar, DateOnly OdemeTarihi, string Gonderen, string? Aciklama);

public sealed record OdemeBildirimiOzeti(
    Guid Id,
    Guid FirmaId,
    string FirmaUnvani,
    string PaketKodu,
    string PaketAdi,
    int AySayisi,
    decimal Tutar,
    decimal? PaketUcreti,
    DateOnly OdemeTarihi,
    string Gonderen,
    string? Aciklama,
    OdemeBildirimiDurumu Durum,
    string? SonucNotu,
    DateTime BildirimTarihi,
    DateTime? SonucTarihi,
    DateTime? YeniBitisTarihi);

public sealed record BildirimOnayIstegi(string? Not);

public sealed record BildirimRetIstegi(string Not);

public sealed record SatistakiPaket(
    string Kod,
    string Ad,
    string? Aciklama,
    bool TumModullerMi,
    IReadOnlyList<string> Moduller,
    int? KullaniciLimiti,
    decimal? AylikUcret,
    decimal? YillikUcret);

public sealed record AbonelikDurumu(
    string? PaketKodu,
    string? PaketAdi,
    DateTime? BitisTarihi,
    int KalanGun,
    bool SaltOkunur,
    int KullaniciSayisi,
    int? KullaniciLimiti,
    IReadOnlyList<SatistakiPaket> Paketler,
    OdemeBilgileri? OdemeBilgileri,
    IReadOnlyList<OdemeBildirimiOzeti> Bildirimler);
