using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Yonetim.Infrastructure;

// Her kayıt doğrulamasında okunduğu için firma başına önbelleklenir; ayar değişince önbellekten silinir.
internal sealed class ZorunluAlanSorgusu(YonetimDbContext db, HybridCache cache, IMevcutKullanici mevcutKullanici) : IZorunluAlanSorgusu
{
    private static readonly HybridCacheEntryOptions _secenekler = new()
    {
        Expiration = TimeSpan.FromMinutes(30),
        LocalCacheExpiration = TimeSpan.FromMinutes(30)
    };

    public async Task<IReadOnlySet<string>> SeciliAsync(CancellationToken cancellationToken = default)
    {
        if (mevcutKullanici.FirmaId is not { } firmaId)
        {
            return new HashSet<string>();
        }

        // Önbellek fabrikası istekten ayrı bir yürütme bağlamında çalışabilir; oturuma bağlı firma filtresi orada boş kalır.
        // Bu yüzden filtre kapatılıp firma açıkça verilir (LisansSorgusu ile aynı yaklaşım).
        var kodlar = await cache.GetOrCreateAsync(
            Anahtar(firmaId),
            (db, firmaId),
            static async (durum, ct) => await durum.db.ZorunluAlanlar
                .IgnoreQueryFilters([ModulDbContext.FirmaFiltresi])
                .AsNoTracking()
                .Where(z => z.FirmaId == durum.firmaId)
                .Select(z => z.Kod)
                .ToArrayAsync(ct),
            _secenekler,
            cancellationToken: cancellationToken);
        return kodlar.ToHashSet();
    }

    public async Task OnbellektenKaldirAsync(Guid firmaId, CancellationToken cancellationToken = default) =>
        await cache.RemoveAsync(Anahtar(firmaId), cancellationToken);

    private static string Anahtar(Guid firmaId) => $"zorunlu-alanlar:{firmaId}";
}
