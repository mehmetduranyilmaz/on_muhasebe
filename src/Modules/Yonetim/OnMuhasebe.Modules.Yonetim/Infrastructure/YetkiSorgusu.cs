using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using OnMuhasebe.Modules.Yonetim.Domain;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Yonetim.Infrastructure;

// Her istekte okunduğu için kullanıcı başına önbelleklenir; firmanın rolü ya da bir üyeliği değişince firma etiketiyle topluca silinir.
// Pasif üyeye yetki dönmez: üyeliği kapatılan kişinin süresi dolmamış access token'ı da bu sayede hemen işlevsiz kalır.
internal sealed class YetkiSorgusu(YonetimDbContext db, HybridCache cache, YetkiKatalogu katalog) : IYetkiSorgusu
{
    private static readonly HybridCacheEntryOptions _secenekler = new()
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(10)
    };

    public async Task<KullaniciYetkileri> GetirAsync(Guid firmaId, Guid kullaniciId, CancellationToken cancellationToken = default) =>
        await cache.GetOrCreateAsync(
            $"yetki:{firmaId}:{kullaniciId}",
            (db, katalog, firmaId, kullaniciId),
            static async (d, ct) => await HesaplaAsync(d.db, d.katalog, d.firmaId, d.kullaniciId, ct),
            _secenekler,
            [Etiket(firmaId)],
            cancellationToken);

    public async Task OnbellektenKaldirAsync(Guid firmaId, CancellationToken cancellationToken = default) =>
        await cache.RemoveByTagAsync(Etiket(firmaId), cancellationToken);

    private static async Task<KullaniciYetkileri> HesaplaAsync(
        YonetimDbContext db, YetkiKatalogu katalog, Guid firmaId, Guid kullaniciId, CancellationToken cancellationToken)
    {
        var uyelik = await db.FirmaKullanicilari
            .IgnoreQueryFilters([ModulDbContext.FirmaFiltresi])
            .AsNoTracking()
            .Where(u => u.FirmaId == firmaId && u.KullaniciId == kullaniciId && u.AktifMi && u.Kullanici.AktifMi)
            .Select(u => new { u.Rol.TamYetkiliMi, u.Rol.Yetkiler, u.EkYetkiler, u.YasakYetkiler, u.Isyerleri, u.VarsayilanIsyeriId })
            .SingleOrDefaultAsync(cancellationToken);

        return uyelik switch
        {
            null => KullaniciYetkileri.Yok,
            { TamYetkiliMi: true } => new KullaniciYetkileri(true, [], [], uyelik.VarsayilanIsyeriId),
            _ => new KullaniciYetkileri(false, YetkiHesaplayici.Hesapla(uyelik.Yetkiler, uyelik.EkYetkiler, uyelik.YasakYetkiler, katalog.Tumu),
                uyelik.Isyerleri, uyelik.VarsayilanIsyeriId)
        };
    }

    private static string Etiket(Guid firmaId) => $"yetki:{firmaId}";
}
