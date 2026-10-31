using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using OnMuhasebe.Shared.Contracts.Isyerleri;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Yonetim.Infrastructure;

// Belge kaydında her seferinde okunduğu için firma başına önbelleklenir; işyeri eklenince/değişince silinir.
internal sealed class IsyeriSorgusu(YonetimDbContext db, HybridCache cache) : IIsyeriSorgusu
{
    private static readonly HybridCacheEntryOptions _secenekler = new()
    {
        Expiration = TimeSpan.FromMinutes(30),
        LocalCacheExpiration = TimeSpan.FromMinutes(30)
    };

    public async Task<IReadOnlyList<IsyeriOzeti>> FirmaninAsync(Guid firmaId, CancellationToken cancellationToken = default) =>
        await cache.GetOrCreateAsync(
            Anahtar(firmaId),
            (db, firmaId),
            static async (d, ct) => await d.db.Isyerleri
                .IgnoreQueryFilters([ModulDbContext.FirmaFiltresi])
                .AsNoTracking()
                .Where(i => i.FirmaId == d.firmaId)
                .OrderByDescending(i => i.MerkezMi).ThenBy(i => i.Kod)
                .Select(i => new IsyeriOzeti(i.Id, i.Kod, i.Ad, i.MerkezMi, i.AktifMi, i.Adres, i.Il, i.Ilce, i.Telefon, i.SgkSicilNo))
                .ToListAsync(ct),
            _secenekler,
            cancellationToken: cancellationToken);

    public async Task OnbellektenKaldirAsync(Guid firmaId, CancellationToken cancellationToken = default) =>
        await cache.RemoveAsync(Anahtar(firmaId), cancellationToken);

    private static string Anahtar(Guid firmaId) => $"isyerleri:{firmaId}";
}
