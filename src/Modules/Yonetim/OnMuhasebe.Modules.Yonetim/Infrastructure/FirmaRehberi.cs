using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Yonetim.Contracts;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Yonetim.Infrastructure;

// Yönetim şeması satır güvenliği dışındadır; firma açıkça verilir, oturum filtresi kapatılır.
internal sealed class FirmaRehberi(YonetimDbContext db) : IFirmaRehberi
{
    public async Task<IReadOnlyList<Guid>> AktifFirmalarAsync(CancellationToken cancellationToken = default) =>
        await db.Firmalar.AsNoTracking().Where(f => f.AktifMi).Select(f => f.Id).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FirmaAlicisi>> KullanicilarAsync(Guid firmaId, CancellationToken cancellationToken = default) =>
        await db.FirmaKullanicilari
            .IgnoreQueryFilters([ModulDbContext.FirmaFiltresi])
            .AsNoTracking()
            .Where(u => u.FirmaId == firmaId && u.AktifMi && u.Kullanici.AktifMi)
            .Select(u => new FirmaAlicisi(u.KullaniciId, u.Kullanici.Ad + " " + u.Kullanici.Soyad, u.Kullanici.Eposta, u.Rol))
            .ToListAsync(cancellationToken);
}
