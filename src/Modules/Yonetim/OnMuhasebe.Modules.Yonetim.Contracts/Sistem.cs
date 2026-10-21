namespace OnMuhasebe.Modules.Yonetim.Contracts;

public static class PaketKodlari
{
    public const string Deneme = "deneme";
    public const string Basic = "basic";
    public const string Profesyonel = "profesyonel";
    public const string Kurumsal = "kurumsal";
}

public sealed record FirmaOlusturIstegi(
    string FirmaUnvani,
    string? VergiNo,
    string? VergiDairesi,
    string YoneticiAd,
    string YoneticiSoyad,
    string YoneticiEposta,
    string? YoneticiSifre,
    string PaketKodu,
    DateOnly LisansBitisTarihi);

public sealed record FirmaOlusturCevabi(Guid FirmaId, Guid YoneticiId);

public sealed record LisansAtaIstegi(string PaketKodu, DateOnly BitisTarihi, IReadOnlyList<string>? EkModuller);

public sealed record SistemFirmaOzeti(
    Guid Id,
    string Unvan,
    string? VergiNo,
    bool AktifMi,
    string? PaketKodu,
    DateTime? LisansBitisTarihi,
    IReadOnlyList<string> EkModuller,
    int KullaniciSayisi);

public sealed record PaketOzeti(
    string Kod,
    string Ad,
    bool TumModullerMi,
    IReadOnlyList<string> Moduller,
    string? Aciklama,
    int? KullaniciLimiti,
    decimal? AylikUcret,
    decimal? YillikUcret,
    bool SatistaMi,
    int Sira,
    int FirmaSayisi);

// Kod yalnızca yeni pakette kullanılır (küçük harf, rakam, tire); güncellemede adresteki kod esastır.
public sealed record PaketKaydetIstegi(
    string Kod,
    string Ad,
    string? Aciklama,
    bool TumModullerMi,
    IReadOnlyList<string> Moduller,
    int? KullaniciLimiti,
    decimal? AylikUcret,
    decimal? YillikUcret,
    bool SatistaMi,
    int Sira);

// Firmanın kendi bilgileri; yazdırılan belgelerin (fatura) üst bilgisinde kullanılır.
public sealed record FirmaBilgileri(
    string Unvan,
    string? VergiNo,
    string? VergiDairesi,
    string? Il,
    string? Ilce,
    string? Adres,
    string? Telefon,
    string? Eposta);
