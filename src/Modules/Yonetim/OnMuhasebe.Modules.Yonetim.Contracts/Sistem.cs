namespace OnMuhasebe.Modules.Yonetim.Contracts;

public static class PaketKodlari
{
    public const string Deneme = "deneme";
    public const string Basic = "basic";
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

public sealed record PaketOzeti(string Kod, string Ad, bool TumModullerMi, IReadOnlyList<string> Moduller);
