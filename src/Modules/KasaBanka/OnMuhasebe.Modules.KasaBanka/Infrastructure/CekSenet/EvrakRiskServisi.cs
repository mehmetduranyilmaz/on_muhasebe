using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.KasaBanka.Domain.CekSenet;

namespace OnMuhasebe.Modules.KasaBanka.Infrastructure.CekSenet;

// Keşideci/borçlu kara listesi: elle işaretlenenler ve evrakı karşılıksız çıkmış (uyarısı kapatılmamış) olanlar.
internal sealed class EvrakRiskServisi(KasaBankaDbContext db)
{
    // Kimlik numarasıyla, kayıtlı kartla ya da (kimliksiz girişte) adla eşleşen riskli borçlular.
    public async Task<IReadOnlyList<RiskliBorclu>> RiskliBorclularAsync(
        IReadOnlyCollection<Guid> borcluIdleri, IReadOnlyCollection<string> kimlikNolari, IReadOnlyCollection<string> adlar, CancellationToken ct)
    {
        var buyukAdlar = adlar.Select(a => a.Trim().ToUpperInvariant()).Distinct().ToList();
        var adaylar = await db.EvrakBorclulari.AsNoTracking()
            .Where(b => borcluIdleri.Contains(b.Id)
                        || (b.KimlikNo != null && kimlikNolari.Contains(b.KimlikNo))
                        || (b.KimlikNo == null && buyukAdlar.Contains(b.Ad.ToUpper())))
            .ToListAsync(ct);
        if (adaylar.Count == 0)
        {
            return [];
        }

        var karsiliksizlar = await KarsiliksizlarAsync(adaylar.Select(b => b.Id).ToList(), ct);
        return adaylar
            .Select(b => (Borclu: b, Kayitlar: karsiliksizlar.GetValueOrDefault(b.Id) ?? []))
            .Where(x => RiskMesaji.RiskliMi(x.Borclu.KaraListede, x.Borclu.UyariKapali, x.Kayitlar.Count))
            .Select(x => new RiskliBorclu(x.Borclu.Id, x.Borclu.Ad, x.Borclu.KimlikNo, x.Borclu.KaraListede, x.Borclu.RiskNotu, x.Kayitlar))
            .ToList();
    }

    public async Task<Dictionary<Guid, List<KarsiliksizKaydi>>> KarsiliksizlarAsync(IReadOnlyCollection<Guid> borcluIdleri, CancellationToken ct) =>
        (await db.Evraklar.AsNoTracking()
            .Where(e => e.BorcluId != null && borcluIdleri.Contains(e.BorcluId.Value) && e.KarsiliksizTarihi != null && e.Durum != EvrakDurumu.Iptal)
            .Select(e => new { BorcluId = e.BorcluId!.Value, Kayit = new KarsiliksizKaydi(e.Id, e.Kod, e.Tur, e.KarsiliksizTarihi!.Value, e.Tutar, e.ParaBirimi, e.Durum) })
            .ToListAsync(ct))
        .GroupBy(x => x.BorcluId)
        .ToDictionary(g => g.Key, g => g.Select(x => x.Kayit).ToList());
}
