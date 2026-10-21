namespace OnMuhasebe.Modules.Yonetim.Contracts;

public sealed record GirisIstegi(string Eposta, string Sifre);

public sealed record KayitIstegi(string FirmaUnvani, string? VergiNo, string Ad, string Soyad, string Eposta, string Sifre);

public sealed record FirmaDegistirIstegi(Guid FirmaId);

public sealed record TokenCevabi(string AccessToken, DateTime BitisTarihi);

public sealed record KullaniciBilgisi(
    Guid Id,
    string Ad,
    string Soyad,
    string Eposta,
    bool SistemYoneticisiMi,
    FirmaOzeti? AktifFirma,
    IReadOnlyList<FirmaOzeti> Firmalar,
    LisansOzeti? Lisans);

public sealed record FirmaOzeti(Guid Id, string Unvan, string Rol);

public sealed record LisansOzeti(IReadOnlyList<string> Moduller, DateTime BitisTarihi, bool SaltOkunur);

public sealed record SifreDegistirIstegi(string MevcutSifre, string YeniSifre);

public sealed record SifreSifirlamaIstegi(string Eposta);

public sealed record SifreSifirlaIstegi(string Kod, string YeniSifre);

public sealed record EpostaDogrulaIstegi(string Kod);

public sealed record DogrulamaTekrarIstegi(string Eposta);
