using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Shared.Contracts.Ayarlar;
using OnMuhasebe.Shared.Contracts.Kimlik;

namespace OnMuhasebe.Shared.Infrastructure.Persistence;

public static class DegisiklikGecmisi
{
    public const int AzamiSatir = 200;

    // GET {modül}/degisiklikler?kayitId= : kaydın ve alt satırlarının değişiklik geçmişi, yeniden eskiye (yalnız yönetici).
    public static RouteHandlerBuilder MapDegisiklikGecmisi<TContext>(this RouteGroupBuilder grup) where TContext : ModulDbContext
    {
        Func<Guid, TContext, CancellationToken, Task<Ok<List<DegisiklikSatiri>>>> islem = GetirAsync;
        return grup.MapGet("/degisiklikler", islem).RequireAuthorization(p => p.RequireRole(Roller.Yonetici));
    }

    private static async Task<Ok<List<DegisiklikSatiri>>> GetirAsync<TContext>(Guid kayitId, TContext db, CancellationToken ct)
        where TContext : ModulDbContext
    {
        var kayitlar = await db.Set<DegisiklikKaydi>()
            .AsNoTracking()
            .Where(k => k.KayitId == kayitId || k.UstKayitId == kayitId)
            .OrderByDescending(k => k.Zaman).ThenByDescending(k => k.Id)
            .Take(AzamiSatir)
            .ToListAsync(ct);
        return TypedResults.Ok(kayitlar
            .Select(k => new DegisiklikSatiri(
                k.Zaman, k.KullaniciAdi, k.Tablo, k.KayitId, k.Islem,
                JsonSerializer.Deserialize<List<AlanDegisikligi>>(k.Alanlar) ?? []))
            .ToList());
    }
}
