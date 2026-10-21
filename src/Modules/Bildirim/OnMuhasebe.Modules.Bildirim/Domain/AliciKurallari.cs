using OnMuhasebe.Modules.Bildirim.Contracts;
using OnMuhasebe.Shared.Contracts.Bildirimler;
using OnMuhasebe.Shared.Contracts.Kimlik;

namespace OnMuhasebe.Modules.Bildirim.Domain;

internal static class AliciKurallari
{
    public const int OzetSaati = 8;

    // Tercih kaydı olmayan kullanıcı: yönetici her türü alır (e-posta günlük özet), kullanıcı rolü almaz.
    public static BildirimTercihleri Varsayilan(string rol) => new(rol == Roller.Yonetici, [], EpostaSikligi.GunlukOzet);

    // Abonelik ve ödeme bildirimi yalnız yöneticiye gider; kullanıcı rolü abonelik ekranını açamaz.
    public static bool Alir(string rol, BildirimTercihleri tercih, BildirimTuru tur) =>
        tercih.BildirimAlir && !tercih.KapaliTurler.Contains(tur)
        && (rol == Roller.Yonetici || tur is not (BildirimTuru.AbonelikBitiyor or BildirimTuru.OdemeBildirimiSonucu));

    // Günlük özet sabah 08.00'den sonra günde bir kez gider.
    public static bool OzetZamaniMi(DateTime yerelSaat, DateOnly? sonOzet) =>
        yerelSaat.Hour >= OzetSaati && sonOzet != DateOnly.FromDateTime(yerelSaat);
}
