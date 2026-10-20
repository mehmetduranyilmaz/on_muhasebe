namespace OnMuhasebe.Shared.Infrastructure.Lisans;

public interface ILisansSorgusu
{
    Task<LisansDurumu?> GetirAsync(Guid firmaId, CancellationToken cancellationToken = default);

    Task OnbellektenKaldirAsync(Guid firmaId, CancellationToken cancellationToken = default);
}

public sealed record LisansDurumu(IReadOnlyList<string> Moduller, DateTime BitisTarihi)
{
    public bool SuresiDolduMu(DateTime simdi) => simdi >= BitisTarihi;
}
