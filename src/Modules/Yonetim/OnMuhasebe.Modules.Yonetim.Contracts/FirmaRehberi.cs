namespace OnMuhasebe.Modules.Yonetim.Contracts;

public sealed record FirmaAlicisi(Guid KullaniciId, string AdSoyad, string Eposta, string Rol);

public sealed record FirmaRolOzeti(Guid Id, string Ad, bool TamYetkiliMi);

public sealed record FirmaKullaniciRolu(Guid KullaniciId, Guid RolId, bool TamYetkiliMi);

// Oturumsuz işler (bildirim üretimi) için firma ve kullanıcı bilgisi.
public interface IFirmaRehberi
{
    Task<IReadOnlyList<Guid>> AktifFirmalarAsync(CancellationToken cancellationToken = default);

    // Firmanın aktif kullanıcıları (pasifleştirilen ya da hesabı kapatılan hariç).
    Task<IReadOnlyList<FirmaAlicisi>> KullanicilarAsync(Guid firmaId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FirmaRolOzeti>> RollerAsync(Guid firmaId, CancellationToken cancellationToken = default);

    // Aktif kullanıcıların rolleri: rol bazlı onay ve benzeri akışlar için.
    Task<IReadOnlyList<FirmaKullaniciRolu>> KullaniciRolleriAsync(Guid firmaId, CancellationToken cancellationToken = default);
}
