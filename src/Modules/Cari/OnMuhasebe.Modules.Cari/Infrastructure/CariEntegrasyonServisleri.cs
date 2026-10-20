using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.Cari.Domain;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Cari.Infrastructure;

internal sealed class CariSorgusu(CariDbContext db) : ICariSorgusu
{
    public Task<CariBilgisi?> GetirAsync(Guid cariId, CancellationToken cancellationToken = default) =>
        db.CariHesaplar.AsNoTracking()
            .Where(c => c.Id == cariId)
            .Select(c => new CariBilgisi(c.Id, c.Kod, c.Unvan, c.VergiNo, c.VergiDairesi, c.Il, c.Ilce, c.Adres, c.ParaBirimi, c.AktifMi))
            .SingleOrDefaultAsync(cancellationToken);
}

internal sealed class CariHareketYazici(CariDbContext db) : ICariHareketYazici
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

        db.CariHareketler.Add(sonuc.Deger);
        await db.SaveChangesAsync(cancellationToken);
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
        return Sonuc.Basarili();
    }
}
