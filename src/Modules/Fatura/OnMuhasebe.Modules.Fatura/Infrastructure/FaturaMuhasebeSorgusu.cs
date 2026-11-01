using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Shared.Contracts.Muhasebe;

namespace OnMuhasebe.Modules.Fatura.Infrastructure;

// Kullanıcının işyeri kısıtı geçerlidir: kısıtlı kullanıcı yalnız kendi işyerlerinin faturalarını aktarır.
internal sealed class FaturaMuhasebeSorgusu(FaturaDbContext db) : IFaturaMuhasebeSorgusu
{
    public async Task<IReadOnlyList<MuhasebeFaturasi>> OnayliAsync(
        DateOnly baslangic, DateOnly bitis, Guid? isyeriId, CancellationToken cancellationToken = default) =>
        await db.Faturalar.AsNoTracking()
            .Where(f => f.Durum == FaturaDurumu.Onayli && f.Tarih >= baslangic && f.Tarih <= bitis && (isyeriId == null || f.IsyeriId == isyeriId))
            .OrderBy(f => f.Tarih).ThenBy(f => f.Numara)
            .Select(f => new MuhasebeFaturasi(f.Id, f.Tur.ToString(), f.Numara, f.Tarih, f.CariHesapId, f.CariUnvan, f.Kur,
                f.Satirlar.Where(s => s.StokluMu).Sum(s => s.Matrah), f.Satirlar.Where(s => !s.StokluMu).Sum(s => s.Matrah),
                f.KdvToplami, f.TevkifEdilenKdvToplami, f.StopajTutari, f.IsyeriId, f.Aciklama))
            .ToListAsync(cancellationToken);
}
