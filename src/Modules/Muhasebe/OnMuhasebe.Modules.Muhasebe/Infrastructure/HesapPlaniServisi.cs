using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Muhasebe.Contracts;
using OnMuhasebe.Modules.Muhasebe.Domain;

namespace OnMuhasebe.Modules.Muhasebe.Infrastructure;

internal sealed class HesapPlaniServisi(MuhasebeDbContext db)
{
    public async Task<(MuhasebeAyari Ayar, HesapPlani Plan, IReadOnlyDictionary<(KartTuru, Guid), string> OzelKodlar)> GetirAsync(CancellationToken ct)
    {
        var ayar = await db.Ayarlar.AsNoTracking().SingleOrDefaultAsync(ct) ?? MuhasebeAyari.Varsayilan();
        var kodlar = await db.HesapKodlari.AsNoTracking().ToDictionaryAsync(k => k.Rol, k => k.Kod, ct);
        var ozel = await db.KodEslemeleri.AsNoTracking().ToDictionaryAsync(k => (k.KartTuru, k.KartId), k => k.Kod, ct);
        return (ayar, new HesapPlani(kodlar, ayar.Ayirac, ayar.CariAltGrup), ozel);
    }
}
