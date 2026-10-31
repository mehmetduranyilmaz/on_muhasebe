using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.Cari.Domain;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Cari.Infrastructure;

internal sealed class CariSorgusu(CariDbContext db, CariKapamaServisi kapama) : ICariSorgusu
{
    public Task<CariBilgisi?> GetirAsync(Guid cariId, CancellationToken cancellationToken = default) =>
        db.CariHesaplar.AsNoTracking()
            .Where(c => c.Id == cariId)
            .Select(c => new CariBilgisi(c.Id, c.Kod, c.Unvan, c.VergiNo, c.VergiDairesi, c.Il, c.Ilce, c.Adres, c.ParaBirimi, c.AktifMi, c.FarkliDovizdeIslem))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, decimal>> BelgeKalanlariAsync(
        IReadOnlyCollection<Guid> belgeIdleri, CancellationToken cancellationToken = default) =>
        await kapama.BelgeKalanlariAsync(belgeIdleri, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> UnvanlarAsync(
        IReadOnlyCollection<Guid> cariIdleri, CancellationToken cancellationToken = default) =>
        await db.CariHesaplar.AsNoTracking()
            .IgnoreQueryFilters([ModulDbContext.SilinmeFiltresi])
            .Where(c => cariIdleri.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Unvan, cancellationToken);

    public async Task<IReadOnlyList<CariEslestirmeAdayi>> EslestirmeAdaylariAsync(CancellationToken cancellationToken = default) =>
        await db.CariHesaplar.AsNoTracking()
            .Where(c => c.AktifMi)
            .Select(c => new CariEslestirmeAdayi(c.Id, c.Unvan, c.VergiNo, c.Iban, c.ParaBirimi))
            .ToListAsync(cancellationToken);

    // Pasif carinin açık kalemi de gerçek alacak/borçtur; listeye alınır.
    public async Task<IReadOnlyList<AcikCariKalemi>> AcikKalemlerAsync(CancellationToken cancellationToken = default)
    {
        var kalemler = await kapama.FirmaAcikKalemleriAsync(DateOnly.MinValue, cancellationToken);
        var cariler = await db.CariHesaplar.AsNoTracking()
            .IgnoreQueryFilters([ModulDbContext.SilinmeFiltresi])
            .Select(c => new { c.Id, c.Unvan, c.ParaBirimi })
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        return kalemler
            .Where(x => !CariHareketTurleri.AvansMi(x.Kalem.Tur, x.Kalem.Yon) && cariler.ContainsKey(x.CariHesapId))
            .Select(x =>
            {
                var c = cariler[x.CariHesapId];
                return new AcikCariKalemi(x.CariHesapId, c.Unvan, x.Kalem.Yon, x.Kalem.VadeTarihi ?? x.Kalem.Tarih, x.Kalem.Kalan, c.ParaBirimi,
                    x.Kalem.Tur, x.Kalem.BelgeNo, x.Kalem.KaynakBelgeId);
            })
            .ToList();
    }
}

// Yazılan her kalem açık kalemlerle eşleştirilir; iptalde kapamalar çözülür ve karşı kalemler yeniden eşleştirilir.
internal sealed class CariHareketYazici(CariDbContext db, CariKapamaServisi kapama) : ICariHareketYazici
{
    public async Task<Sonuc> YazAsync(Guid kaynakBelgeId, BelgeCariHareketi hareket, CancellationToken cancellationToken = default)
    {
        var cari = await db.CariHesaplar.AsNoTracking().SingleOrDefaultAsync(c => c.Id == hareket.CariHesapId, cancellationToken);
        if (cari is null)
        {
            return CariHatalari.CariBulunamadi;
        }

        var sonuc = CariHareket.Olustur(cari, hareket.Tur, hareket.Yon, hareket.Tarih, hareket.VadeTarihi, hareket.Tutar, hareket.Kur,
            hareket.BelgeNo, hareket.Aciklama, kaynakBelgeId);
        if (!sonuc.BasariliMi)
        {
            return sonuc.Hata;
        }

        if (hareket.Avans)
        {
            sonuc.Deger.AvansOlarakIsaretle();
        }

        db.CariHareketler.Add(sonuc.Deger);
        await db.SaveChangesAsync(cancellationToken);
        if (hareket.OtomatikKapama && !hareket.Avans)
        {
            await kapama.KapatAsync(cari.Id, sonuc.Deger.Id, hareket.OncelikliKapamaBelgeId, cancellationToken);
        }

        return Sonuc.Basarili();
    }

    // Belgenin henüz iptal edilmemiş tüm hareketleri ters kayıtla kapatılır.
    public async Task<Sonuc> IptalEtAsync(Guid kaynakBelgeId, CancellationToken cancellationToken = default)
    {
        var hareketler = await db.CariHareketler
            .Where(h => h.KaynakBelgeId == kaynakBelgeId && h.IptalHareketId == null && h.IptalEdilenHareketId == null)
            .ToListAsync(cancellationToken);

        foreach (var hareket in hareketler)
        {
            var sonuc = hareket.IptalEt();
            if (!sonuc.BasariliMi)
            {
                return sonuc.Hata;
            }

            db.CariHareketler.Add(sonuc.Deger);
        }

        await db.SaveChangesAsync(cancellationToken);
        foreach (var cari in hareketler.GroupBy(h => h.CariHesapId))
        {
            await kapama.CozVeKapatAsync(cari.Key, cari.Select(h => h.Id).ToList(), cancellationToken);
        }

        return Sonuc.Basarili();
    }
}
