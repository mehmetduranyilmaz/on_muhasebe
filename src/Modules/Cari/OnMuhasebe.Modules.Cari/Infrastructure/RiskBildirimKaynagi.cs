using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Shared.Contracts.Bildirimler;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Infrastructure.Bildirimler;

namespace OnMuhasebe.Modules.Cari.Infrastructure;

// Bakiyesi risk limitini aşan cariler; aynı cari için ayda bir hatırlatılır.
internal sealed class RiskBildirimKaynagi(CariDbContext db) : IBildirimKaynagi
{
    private static readonly CultureInfo _tr = CultureInfo.GetCultureInfo("tr-TR");

    public string? ModulKodu => ModulKodlari.Cari;

    public async Task<IReadOnlyList<BildirimAdayi>> AdaylarAsync(BildirimBaglami baglam, CancellationToken cancellationToken)
    {
        var asanlar = await db.CariHesaplar.AsNoTracking()
            .Where(c => c.AktifMi && c.RiskLimiti != null)
            .Select(c => new
            {
                c.Id,
                c.Unvan,
                c.ParaBirimi,
                Limit = c.RiskLimiti!.Value,
                Bakiye = db.CariHareketler.Where(h => h.CariHesapId == c.Id).Sum(h => h.Borc - h.Alacak)
            })
            .Where(c => c.Bakiye > c.Limit)
            .ToListAsync(cancellationToken);

        var ay = baglam.Bugun.ToString("yyyyMM", CultureInfo.InvariantCulture);
        return asanlar.Select(c => new BildirimAdayi(
            BildirimTuru.RiskLimiti, $"risk:{c.Id}:{ay}", $"Risk limiti aşıldı: {c.Unvan}",
            $"Bakiye {c.Bakiye.ToString("N2", _tr)} {c.ParaBirimi}, limit {c.Limit.ToString("N2", _tr)} {c.ParaBirimi}.",
            $"cari/{c.Id}", BildirimOnemi.Uyari)).ToList();
    }
}
