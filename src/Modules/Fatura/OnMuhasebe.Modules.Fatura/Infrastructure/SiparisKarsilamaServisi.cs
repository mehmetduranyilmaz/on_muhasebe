using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Modules.Fatura.Domain;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Fatura.Infrastructure;

internal sealed record SiparisBagi(Guid SiparisSatiriId, Guid? StokKartiId, string Birim);

// Sipariş satırının karşılanan miktarı tek kaynaktan yeniden hesaplanır: onaylı irsaliyelerdeki bağlı satırlar ile onaylı
// faturalardaki irsaliyesiz bağlı satırların toplamı. İrsaliye/fatura onaylanınca, düzenlenince ya da iptal edilince çağrılır.
internal sealed class SiparisKarsilamaServisi(FaturaDbContext db)
{
    // Onaylı belgenin bağları denetlenir (açık sipariş, aynı cari ve tür, aynı ürün ve birim); eski bağlar (düzenleme ya da
    // iptalden önceki hâl) yalnızca yeniden hesaplanır.
    public async Task<Sonuc> EsitleAsync(
        Guid? cariHesapId, TeklifSiparisTuru? siparisTuru, IReadOnlyList<SiparisBagi> baglar, IEnumerable<Guid> eskiBaglar,
        CancellationToken cancellationToken)
    {
        var idler = baglar.Select(b => b.SiparisSatiriId).Concat(eskiBaglar).Distinct().ToList();
        if (idler.Count == 0)
        {
            return Sonuc.Basarili();
        }

        var siparisler = await db.TeklifSiparisler.Include(t => t.Satirlar)
            .Where(t => t.Satirlar.Any(s => idler.Contains(s.Id)))
            .ToListAsync(cancellationToken);
        var satirlar = siparisler
            .SelectMany(t => t.Satirlar.Select(s => (Siparis: t, Satir: s)))
            .Where(x => idler.Contains(x.Satir.Id))
            .ToDictionary(x => x.Satir.Id);

        foreach (var b in baglar)
        {
            if (!satirlar.TryGetValue(b.SiparisSatiriId, out var x)
                || x.Siparis.Durum != TeklifSiparisDurumu.Acik
                || x.Siparis.Tur != siparisTuru
                || x.Siparis.CariHesapId != cariHesapId
                || x.Satir.StokKartiId != b.StokKartiId || x.Satir.Birim != b.Birim)
            {
                return TeklifSiparisHatalari.SiparisBagiGecersiz;
            }
        }

        var irsaliyeden = await (
                from s in db.IrsaliyeSatirlari
                join i in db.Irsaliyeler on s.IrsaliyeId equals i.Id
                where i.Durum == IrsaliyeDurumu.Onayli && s.SiparisSatiriId != null && idler.Contains(s.SiparisSatiriId.Value)
                select new { Id = s.SiparisSatiriId!.Value, s.Miktar })
            .ToListAsync(cancellationToken);
        var faturadan = await (
                from s in db.FaturaSatirlari
                join f in db.Faturalar on s.FaturaId equals f.Id
                where f.Durum == FaturaDurumu.Onayli && s.SiparisSatiriId != null && idler.Contains(s.SiparisSatiriId.Value)
                select new { Id = s.SiparisSatiriId!.Value, s.Miktar })
            .ToListAsync(cancellationToken);
        var toplamlar = irsaliyeden.Concat(faturadan).GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.Sum(x => x.Miktar));

        foreach (var (id, x) in satirlar)
        {
            var ayar = x.Siparis.KarsilananAyarla(id, toplamlar.GetValueOrDefault(id));
            if (!ayar.BasariliMi)
            {
                return ayar;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Sonuc.Basarili();
    }

    public static IReadOnlyList<SiparisBagi> Baglar(FaturaBelgesi f) => f.Durum == FaturaDurumu.Onayli
        ? f.Satirlar.Where(s => s.SiparisSatiriId is not null).Select(s => new SiparisBagi(s.SiparisSatiriId!.Value, s.StokKartiId, s.Birim)).ToList()
        : [];

    public static IReadOnlyList<SiparisBagi> Baglar(Irsaliye i) => i.Durum == IrsaliyeDurumu.Onayli
        ? i.Satirlar.Where(s => s.SiparisSatiriId is not null).Select(s => new SiparisBagi(s.SiparisSatiriId!.Value, s.StokKartiId, s.Birim)).ToList()
        : [];

    public static List<Guid> BagIdleri(FaturaBelgesi f) => f.Satirlar.Where(s => s.SiparisSatiriId is not null).Select(s => s.SiparisSatiriId!.Value).ToList();

    public static List<Guid> BagIdleri(Irsaliye i) => i.Satirlar.Where(s => s.SiparisSatiriId is not null).Select(s => s.SiparisSatiriId!.Value).ToList();

    public static TeklifSiparisTuru? SiparisTuru(IrsaliyeTuru tur) =>
        IrsaliyeTurleri.FaturaTuru(tur) is { } faturaTuru ? TeklifSiparisTurleri.SiparisTuru(faturaTuru) : null;
}
