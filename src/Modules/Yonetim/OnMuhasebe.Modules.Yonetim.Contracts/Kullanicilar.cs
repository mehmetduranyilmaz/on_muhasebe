namespace OnMuhasebe.Modules.Yonetim.Contracts;

// Sifre yalnızca sistemde henüz olmayan bir kullanıcı eklenirken gerekir;
// e-posta zaten kayıtlıysa mevcut kullanıcı firmaya bağlanır.
public sealed record KullaniciEkleIstegi(string Ad, string Soyad, string Eposta, string? Sifre, string Rol);

public sealed record FirmaKullanicisiCevabi(Guid KullaniciId, string Ad, string Soyad, string Eposta, string Rol, bool AktifMi);
