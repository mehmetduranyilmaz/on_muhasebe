namespace OnMuhasebe.Modules.Yonetim.Contracts;

public sealed record FirmaAlicisi(Guid KullaniciId, string AdSoyad, string Eposta, string Rol);

// Oturumsuz işler (bildirim üretimi) için firma ve kullanıcı bilgisi.
public interface IFirmaRehberi
{
    Task<IReadOnlyList<Guid>> AktifFirmalarAsync(CancellationToken cancellationToken = default);

    // Firmanın aktif kullanıcıları (pasifleştirilen ya da hesabı kapatılan hariç).
    Task<IReadOnlyList<FirmaAlicisi>> KullanicilarAsync(Guid firmaId, CancellationToken cancellationToken = default);
}
