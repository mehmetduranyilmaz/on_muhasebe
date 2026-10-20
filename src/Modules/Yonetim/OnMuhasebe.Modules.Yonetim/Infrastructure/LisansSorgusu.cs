using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Yonetim.Infrastructure;

internal sealed class LisansSorgusu(YonetimDbContext db, HybridCache cache) : ILisansSorgusu
{
    private static readonly HybridCacheEntryOptions _secenekler = new()
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(10)
    };

    public async Task<LisansDurumu?> GetirAsync(Guid firmaId, CancellationToken cancellationToken = default) =>
        await cache.GetOrCreateAsync(
            Anahtar(firmaId),
            (db, firmaId),
            static async (durum, ct) => await HesaplaAsync(durum.db, durum.firmaId, ct),
            _secenekler,
            cancellationToken: cancellationToken);

    public async Task OnbellektenKaldirAsync(Guid firmaId, CancellationToken cancellationToken = default) =>
        await cache.RemoveAsync(Anahtar(firmaId), cancellationToken);

    private static async Task<LisansDurumu?> HesaplaAsync(YonetimDbContext db, Guid firmaId, CancellationToken cancellationToken)
    {
        var lisans = await db.FirmaLisanslari
            .IgnoreQueryFilters([ModulDbContext.FirmaFiltresi])
            .AsNoTracking()
            .Include(l => l.Paket)
            .Where(l => l.FirmaId == firmaId && db.Firmalar.Any(f => f.Id == firmaId && f.AktifMi))
            .SingleOrDefaultAsync(cancellationToken);

        return lisans is null ? null : new LisansDurumu(lisans.AcikModuller(), lisans.BitisTarihi);
    }

    private static string Anahtar(Guid firmaId) => $"lisans:{firmaId}";
}
