namespace OnMuhasebe.Modules.Yonetim.Contracts;

// Kullanıcı eklemek davet göndermektir: kişi e-postasındaki bağlantıyla kabul edince firmaya katılır.
public sealed record KullaniciDavetIstegi(string Eposta, Guid RolId);

public sealed record FirmaKullanicisiCevabi(
    Guid KullaniciId, string Ad, string Soyad, string Eposta, Guid RolId, string RolAdi, bool TamYetkiliMi,
    IReadOnlyList<string> EkYetkiler, IReadOnlyList<string> YasakYetkiler, bool AktifMi);

// Kullanıcının rolü ve rolün üzerine ona özel verilen (ek) ya da alınan (yasak) yetkiler.
public sealed record KullaniciYetkiIstegi(Guid RolId, IReadOnlyList<string>? EkYetkiler, IReadOnlyList<string>? YasakYetkiler);

public sealed record DavetOzeti(Guid Id, string Eposta, Guid RolId, string RolAdi, DateTime SonGonderimTarihi, DateTime BitisTarihi);

public sealed record DavetBilgiIstegi(string Kod);

// HesapVarMi: e-postaya ait doğrulanmış hesap var; kabul için yalnızca şifresi istenir, yoksa ad, soyad ve yeni şifre.
public sealed record DavetBilgisi(string FirmaUnvani, string Eposta, string RolAdi, bool HesapVarMi);

public sealed record DavetKabulIstegi(string Kod, string? Ad, string? Soyad, string Sifre);

public sealed record RolCevabi(Guid Id, string Ad, string? Aciklama, bool TamYetkiliMi, IReadOnlyList<string> Yetkiler, int KullaniciSayisi);

public sealed record RolKaydetIstegi(string Ad, string? Aciklama, IReadOnlyList<string> Yetkiler);
