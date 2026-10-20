using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Yonetim.Infrastructure;

// Döneme bağlı her kayıtta okunduğu için firma başına önbelleklenir; ayar değişince önbellekten silinir.
internal sealed class DonemKilidiSorgusu(YonetimDbContext db, HybridCache cache) : IDonemKilidiSorgusu
{
    private static readonly HybridCacheEntryOptions _secenekler = new()
    {
        Expiration = TimeSpan.FromMinutes(30),
        LocalCacheExpiration = TimeSpan.FromMinutes(30)
    };

    // Önbellek fabrikasında oturum filtresi boş kalır; filtre kapatılıp firma açıkça verilir (bkz. ZorunluAlanSorgusu).
    public async Task<DateOnly?> KilitTarihiAsync(Guid firmaId, CancellationToken cancellationToken = default) =>
        await cache.GetOrCreateAsync(
            Anahtar(firmaId),
            (db, firmaId),
            static async (durum, ct) => await durum.db.DonemKilitleri
                .IgnoreQueryFilters([ModulDbContext.FirmaFiltresi])
                .AsNoTracking()
                .Where(k => k.FirmaId == durum.firmaId)
                .Select(k => k.KilitTarihi)
                .FirstOrDefaultAsync(ct),
            _secenekler,
            cancellationToken: cancellationToken);

    public async Task OnbellektenKaldirAsync(Guid firmaId, CancellationToken cancellationToken = default) =>
        await cache.RemoveAsync(Anahtar(firmaId), cancellationToken);

    private static string Anahtar(Guid firmaId) => $"donem-kilidi:{firmaId}";
}
