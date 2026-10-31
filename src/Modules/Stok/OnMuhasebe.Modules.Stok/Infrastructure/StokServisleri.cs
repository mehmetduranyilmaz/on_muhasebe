using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Modules.Stok.Domain;
using OnMuhasebe.Modules.Stok.Domain.Maliyet;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using Depo = OnMuhasebe.Modules.Stok.Domain.Depo;

namespace OnMuhasebe.Modules.Stok.Infrastructure;

internal sealed class StokKodUretici(StokDbContext db, IMevcutKullanici mevcutKullanici)
{
    public Task<string> SonrakiAsync(CancellationToken cancellationToken) =>
        KodUretici.SonrakiAsync(db, "stok.stok_karti", "S", mevcutKullanici.FirmaId!.Value, cancellationToken);
}

internal sealed class DepoServisi(StokDbContext db, IsyeriSecici isyeriSecici)
{
    // Firmanın ilk stok işleminde merkez işyerinde "Merkez Depo" kendiliğinden açılır; depo kavramını bilmeyen kullanıcı fark etmez.
    // Varsayılan depo firma genelidir: işyeri kısıtlı kullanıcı onu görmese de yeniden açılmaz, başka depo varsayılan yapılmaz.
    public async Task<Depo> VarsayilanAsync(CancellationToken cancellationToken)
    {
        var depolar = await db.Depolar.IgnoreQueryFilters([ModulDbContext.IsyeriFiltresi]).OrderBy(d => d.OlusturmaTarihi).ToListAsync(cancellationToken);
        if (depolar.Find(d => d.VarsayilanMi) is { } varsayilan)
        {
            return varsayilan;
        }

        if (depolar.FirstOrDefault() is { } ilk)
        {
            ilk.VarsayilanYap(true);
            await db.SaveChangesAsync(cancellationToken);
            return ilk;
        }

        var yeni = Depo.Olustur(Depo.VarsayilanAd, await isyeriSecici.MerkezAsync(cancellationToken), varsayilanMi: true);
        db.Depolar.Add(yeni);
        await db.SaveChangesAsync(cancellationToken);
        return yeni;
    }
}

internal sealed class StokAyarServisi(StokDbContext db)
{
    public async Task<StokAyari> GetirAsync(CancellationToken cancellationToken) =>
        await db.StokAyarlari.SingleOrDefaultAsync(cancellationToken) ?? StokAyari.Varsayilan();
}

internal sealed record KartHesabi(
    decimal Miktar,
    IReadOnlyDictionary<Guid, decimal> DepoMiktarlari,
    decimal BirimMaliyet,
    decimal Deger,
    IReadOnlyDictionary<Guid, decimal> CikisBirimMaliyetleri);

// Stok miktarı ve maliyetini hareket geçmişinden hesaplar. Maliyet firma genelidir (depo bazlı değil);
// depo filtresinde değer = depodaki miktar × firma birim maliyeti.
internal sealed class StokHesaplayici(StokDbContext db)
{
    public async Task<Dictionary<Guid, KartHesabi>> HesaplaAsync(
        IQueryable<Guid> kartIdleri, DateOnly? tarih, MaliyetYontemi yontem, CancellationToken cancellationToken)
    {
        var sorgu = db.StokHareketleri.AsNoTracking().Where(h => kartIdleri.Contains(h.StokKartiId));
        if (tarih is { } t)
        {
            sorgu = sorgu.Where(h => h.Tarih <= t);
        }

        var hareketler = await sorgu
            .OrderBy(h => h.Tarih).ThenBy(h => h.Id)
            .Select(h => new
            {
                h.Id,
                h.StokKartiId,
                h.DepoId,
                h.Tur,
                h.GirisMiktari,
                h.CikisMiktari,
                h.BirimFiyat,
                IptalliMi = h.IptalHareketId != null || h.IptalEdilenHareketId != null
            })
            .ToListAsync(cancellationToken);

        var hesaplayici = MaliyetHesaplayicilari.Getir(yontem);
        return hareketler
            .GroupBy(h => h.StokKartiId)
            .ToDictionary(g => g.Key, g =>
            {
                var depoMiktarlari = g.GroupBy(h => h.DepoId).ToDictionary(d => d.Key, d => d.Sum(h => h.GirisMiktari - h.CikisMiktari));
                var maliyet = hesaplayici.Hesapla(g
                    .Where(h => !h.IptalliMi && !StokHareket.TransferMi(h.Tur))
                    .Select(h => new MaliyetHareketi(h.Id, h.GirisMiktari, h.CikisMiktari, h.BirimFiyat)));
                var miktar = depoMiktarlari.Values.Sum();
                return new KartHesabi(miktar, depoMiktarlari, maliyet.BirimMaliyet, miktar * maliyet.BirimMaliyet, maliyet.CikisBirimMaliyetleri);
            });
    }

    public async Task<decimal> DepoMiktariAsync(Guid kartId, Guid depoId, CancellationToken cancellationToken) =>
        await db.StokHareketleri
            .Where(h => h.StokKartiId == kartId && h.DepoId == depoId)
            .SumAsync(h => h.GirisMiktari - h.CikisMiktari, cancellationToken);
}

// "Maliyet, alış fiyatı ve kârlılık" yetkisi olmayana birim maliyet, stok değeri ve alış fiyatı gösterilmez (sıfır/boş döner).
internal static class MaliyetYetkisi
{
    public static Task<bool> GorurMuAsync(this IYetkiDenetimi yetki, CancellationToken cancellationToken) =>
        yetki.VarMiAsync(StokYetkileri.Maliyet.Yetki(YetkiIslemleri.Goruntule), cancellationToken);

    public static StokKartiDetayi Gizle(StokKartiDetayi d) => d with { AlisFiyati = null, BirimMaliyet = 0m, StokDegeri = 0m };
}
