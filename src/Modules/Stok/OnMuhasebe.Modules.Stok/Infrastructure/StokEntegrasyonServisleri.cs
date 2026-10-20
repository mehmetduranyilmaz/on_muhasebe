using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Modules.Stok.Domain;
using OnMuhasebe.Modules.Stok.Features;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Stok.Infrastructure;

internal sealed class StokSorgusu(StokDbContext db, DepoServisi depoServisi) : IStokSorgusu
{
    public async Task<IReadOnlyList<StokKartiBilgisi>> KartlariGetirAsync(
        IReadOnlyCollection<Guid> kartIdleri, CancellationToken cancellationToken = default)
    {
        var kartlar = await db.StokKartlari.AsNoTracking().Include(k => k.Birimler)
            .Where(k => kartIdleri.Contains(k.Id))
            .ToListAsync(cancellationToken);

        return kartlar.Select(k => new StokKartiBilgisi(
                k.Id, k.Kod, k.Ad, k.Tur, k.TemelBirim,
                k.Birimler.Select(b => new EkBirim(b.BirimKodu, b.Katsayi, b.Barkod)).ToList(), k.KdvOrani, k.AktifMi))
            .ToList();
    }

    public async Task<Guid?> DepoCozAsync(Guid? depoId, CancellationToken cancellationToken = default)
    {
        if (depoId is null)
        {
            return (await depoServisi.VarsayilanAsync(cancellationToken)).Id;
        }

        return await db.Depolar.AnyAsync(d => d.Id == depoId && d.AktifMi, cancellationToken) ? depoId : null;
    }
}

internal sealed class StokHareketYazici(
    StokDbContext db, StokHesaplayici hesaplayici, StokAyarServisi ayarServisi, StokCikisKontrolu cikisKontrolu) : IStokHareketYazici
{
    public async Task<Sonuc<string?>> YazAsync(
        Guid kaynakBelgeId, IReadOnlyList<BelgeStokHareketi> hareketler, CancellationToken cancellationToken = default)
    {
        if (hareketler.Count == 0)
        {
            return (string?)null;
        }

        var kartIdleri = hareketler.Select(h => h.StokKartiId).Distinct().ToList();
        var kartlar = await db.StokKartlari.Include(k => k.Birimler)
            .Where(k => kartIdleri.Contains(k.Id))
            .ToDictionaryAsync(k => k.Id, cancellationToken);
        if (kartlar.Count != kartIdleri.Count)
        {
            return StokHatalari.KartBulunamadi;
        }

        var depoIdleri = hareketler.Select(h => h.DepoId).Distinct().ToList();
        if (await db.Depolar.CountAsync(d => depoIdleri.Contains(d.Id) && d.AktifMi, cancellationToken) != depoIdleri.Count)
        {
            return StokHatalari.DepoBulunamadi;
        }

        var iadeMaliyetleri = await IadeMaliyetleriAsync(hareketler, cancellationToken);
        var yeniler = new List<StokHareket>();
        foreach (var h in hareketler)
        {
            var kart = kartlar[h.StokKartiId];
            var birimFiyat = h.BirimFiyat;
            if (birimFiyat is null && h.Tur == StokHareketTuru.SatistanIade)
            {
                birimFiyat = iadeMaliyetleri.GetValueOrDefault(kart.Id) * (kart.Katsayi(h.Birim) ?? 1m);
            }

            var sonuc = StokHareket.Olustur(kart, h.DepoId, h.Tur, h.Tarih, h.Birim, h.Miktar, birimFiyat, h.BelgeNo, h.Aciklama,
                kaynakBelgeId: kaynakBelgeId);
            if (!sonuc.BasariliMi)
            {
                return KartAdiyla(kart, sonuc.Hata);
            }

            yeniler.Add(sonuc.Deger);
        }

        // Aynı ürün aynı depodan birden çok satırda çıkabilir; eksi stok kontrolü toplam miktarla yapılır.
        var uyarilar = new List<string>();
        foreach (var cikis in yeniler.Where(h => h.CikisMiktari > 0).GroupBy(h => (h.StokKartiId, h.DepoId)))
        {
            var kart = kartlar[cikis.Key.StokKartiId];
            var kontrol = await cikisKontrolu.KontrolEtAsync(kart, cikis.Key.DepoId, cikis.Sum(h => h.CikisMiktari), cancellationToken);
            if (!kontrol.BasariliMi)
            {
                return KartAdiyla(kart, kontrol.Hata);
            }

            if (kontrol.Deger is { } uyari)
            {
                uyarilar.Add($"{kart.Ad}: {uyari}");
            }
        }

        db.StokHareketleri.AddRange(yeniler);
        await db.SaveChangesAsync(cancellationToken);
        return uyarilar.Count == 0 ? (string?)null : string.Join(" ", uyarilar);
    }

    public async Task<Sonuc> IptalEtAsync(Guid kaynakBelgeId, CancellationToken cancellationToken = default)
    {
        var hareketler = await db.StokHareketleri
            .Where(h => h.KaynakBelgeId == kaynakBelgeId && h.IptalHareketId == null && h.IptalEdilenHareketId == null)
            .ToListAsync(cancellationToken);

        foreach (var hareket in hareketler)
        {
            var sonuc = hareket.IptalEt();
            if (!sonuc.BasariliMi)
            {
                return sonuc.Hata;
            }

            db.StokHareketleri.Add(sonuc.Deger);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Sonuc.Basarili();
    }

    // Satıştan iade, satış fiyatıyla değil iade tarihindeki birim maliyetle (temel birim, TL) stoğa girer;
    // aksi halde ortalama maliyet satış kârı kadar şişer.
    private async Task<Dictionary<Guid, decimal>> IadeMaliyetleriAsync(
        IReadOnlyList<BelgeStokHareketi> hareketler, CancellationToken cancellationToken)
    {
        var iadeler = hareketler.Where(h => h.Tur == StokHareketTuru.SatistanIade && h.BirimFiyat is null).ToList();
        if (iadeler.Count == 0)
        {
            return [];
        }

        var kartIdleri = iadeler.Select(h => h.StokKartiId).Distinct().ToList();
        var ayar = await ayarServisi.GetirAsync(cancellationToken);
        var hesaplar = await hesaplayici.HesaplaAsync(
            db.StokKartlari.Where(k => kartIdleri.Contains(k.Id)).Select(k => k.Id), iadeler.Max(h => h.Tarih), ayar.MaliyetYontemi,
            cancellationToken);
        return hesaplar.ToDictionary(h => h.Key, h => h.Value.BirimMaliyet);
    }

    private static Hata KartAdiyla(StokKarti kart, Hata hata) => hata with { Mesaj = $"{kart.Ad}: {hata.Mesaj}" };
}
