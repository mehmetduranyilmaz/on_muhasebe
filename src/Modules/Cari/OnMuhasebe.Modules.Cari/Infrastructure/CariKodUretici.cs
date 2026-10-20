using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Cari.Infrastructure;

internal sealed class CariKodUretici(CariDbContext db, IMevcutKullanici mevcutKullanici)
{
    public Task<string> SonrakiAsync(CancellationToken cancellationToken) =>
        KodUretici.SonrakiAsync(db, "cari.cari_hesap", "C", mevcutKullanici.FirmaId!.Value, cancellationToken);
}
