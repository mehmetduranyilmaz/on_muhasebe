using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Yonetim.Domain;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Yonetim.Infrastructure;

// Paketin kullanıcı limitine sayılanlar: aktif üyeler ve süresi geçmemiş bekleyen davetler. Mali müşavir rolündekiler sayılmaz.
internal static class KullaniciLimiti
{
    // eklenecek: işlemin limite katacağı kişi sayısı. haricEposta: yeniden davet edilen kişinin eski daveti iki kez sayılmasın.
    public static async Task<Hata?> AsimAsync(
        YonetimDbContext db, Guid firmaId, int eklenecek, DateTime simdi, CancellationToken ct, string? haricEposta = null, bool davetleriSay = true)
    {
        var limit = await db.FirmaLisanslari.IgnoreQueryFilters([ModulDbContext.FirmaFiltresi])
            .Where(l => l.FirmaId == firmaId).Select(l => l.Paket.KullaniciLimiti).SingleOrDefaultAsync(ct);
        if (limit is null)
        {
            return null;
        }

        var uyeler = await db.FirmaKullanicilari.IgnoreQueryFilters([ModulDbContext.FirmaFiltresi])
            .CountAsync(u => u.FirmaId == firmaId && u.AktifMi && !u.Rol.MaliMusavirMi, ct);
        var davetler = davetleriSay
            ? await db.FirmaDavetleri.IgnoreQueryFilters([ModulDbContext.FirmaFiltresi])
                .CountAsync(d => d.FirmaId == firmaId && d.Eposta != haricEposta && !d.Rol.MaliMusavirMi
                                 && d.KabulTarihi == null && d.IptalTarihi == null && d.BitisTarihi > simdi, ct)
            : 0;
        return uyeler + davetler + eklenecek > limit ? YonetimHatalari.KullaniciLimiti(limit.Value) : null;
    }
}
