namespace OnMuhasebe.Modules.Yonetim.Contracts;

// Kullanıcı eklemek davet göndermektir: kişi e-postasındaki bağlantıyla kabul edince firmaya katılır.
public sealed record KullaniciDavetIstegi(string Eposta, string Rol);

public sealed record FirmaKullanicisiCevabi(Guid KullaniciId, string Ad, string Soyad, string Eposta, string Rol, bool AktifMi);

public sealed record DavetOzeti(Guid Id, string Eposta, string Rol, DateTime SonGonderimTarihi, DateTime BitisTarihi);

public sealed record DavetBilgiIstegi(string Kod);

// HesapVarMi: e-postaya ait doğrulanmış hesap var; kabul için yalnızca şifresi istenir, yoksa ad, soyad ve yeni şifre.
public sealed record DavetBilgisi(string FirmaUnvani, string Eposta, string Rol, bool HesapVarMi);

public sealed record DavetKabulIstegi(string Kod, string? Ad, string? Soyad, string Sifre);
