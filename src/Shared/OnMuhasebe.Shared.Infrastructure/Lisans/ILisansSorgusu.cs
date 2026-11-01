using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Shared.Infrastructure.Lisans;

public interface ILisansSorgusu
{
    Task<LisansDurumu?> GetirAsync(Guid firmaId, CancellationToken cancellationToken = default);

    Task OnbellektenKaldirAsync(Guid firmaId, CancellationToken cancellationToken = default);
}

public sealed record LisansDurumu(IReadOnlyList<string> Moduller, DateTime BitisTarihi, int? DepolamaKotasiMb = null)
{
    public bool SuresiDolduMu(DateTime simdi) => simdi >= BitisTarihi;
}

public static class LisansHatalari
{
    public static readonly Hata SuresiDoldu = Hata.Yetki("lisans_suresi_doldu", "Lisans süreniz doldu; kayıtlarınız salt okunur.");
}
