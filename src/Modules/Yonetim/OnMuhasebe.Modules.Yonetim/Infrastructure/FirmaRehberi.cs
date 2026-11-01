using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Shared.Contracts.Kimlik;
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
            .Select(u => new FirmaAlicisi(u.KullaniciId, u.Kullanici.Ad + " " + u.Kullanici.Soyad, u.Kullanici.Eposta,
                u.Rol.TamYetkiliMi ? Roller.Yonetici : Roller.Kullanici))
            .ToListAsync(cancellationToken);

    public async Task<string?> VergiNoAsync(Guid firmaId, CancellationToken cancellationToken = default) =>
        await db.Firmalar.IgnoreQueryFilters().AsNoTracking().Where(f => f.Id == firmaId).Select(f => f.VergiNo).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<FirmaRolOzeti>> RollerAsync(Guid firmaId, CancellationToken cancellationToken = default) =>
        await db.Roller
            .IgnoreQueryFilters([ModulDbContext.FirmaFiltresi])
            .AsNoTracking()
            .Where(r => r.FirmaId == firmaId)
            .Select(r => new FirmaRolOzeti(r.Id, r.Ad, r.TamYetkiliMi))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FirmaKullaniciRolu>> KullaniciRolleriAsync(Guid firmaId, CancellationToken cancellationToken = default) =>
        await db.FirmaKullanicilari
            .IgnoreQueryFilters([ModulDbContext.FirmaFiltresi])
            .AsNoTracking()
            .Where(u => u.FirmaId == firmaId && u.AktifMi && u.Kullanici.AktifMi)
            .Select(u => new FirmaKullaniciRolu(u.KullaniciId, u.RolId, u.Rol.TamYetkiliMi))
            .ToListAsync(cancellationToken);
}
