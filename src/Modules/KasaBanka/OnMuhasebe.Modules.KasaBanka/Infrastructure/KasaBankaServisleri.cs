using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.KasaBanka.Domain;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.KasaBanka.Infrastructure;

internal sealed class KasaKodUretici(KasaBankaDbContext db, IMevcutKullanici mevcutKullanici)
{
    public Task<string> SonrakiAsync(HesapTuru tur, CancellationToken cancellationToken) =>
        KodUretici.SonrakiAsync(db, "kasabanka.kasa_hesabi", KasaHesabi.KodOneki(tur), mevcutKullanici.FirmaId!.Value, cancellationToken);
}

internal sealed class KategoriServisi(KasaBankaDbContext db)
{
    // Firmanın hiç kategorisi yoksa başlangıç kategorileri açılır; kullanıcı sonra düzenler.
    public async Task VarsayilanlariHazirlaAsync(CancellationToken cancellationToken)
    {
        if (await db.Kategoriler.AnyAsync(cancellationToken))
        {
            return;
        }

        db.Kategoriler.AddRange(GelirGiderKategorisi.Varsayilanlar.Select(k => GelirGiderKategorisi.Olustur(k.Ad, k.Tur)));
        await db.SaveChangesAsync(cancellationToken);
    }
}

internal sealed class KasaBankaSorgusu(KasaBankaDbContext db) : IKasaBankaSorgusu
{
    public Task<KasaHesapBilgisi?> HesapGetirAsync(Guid hesapId, CancellationToken cancellationToken = default) =>
        db.Hesaplar.AsNoTracking()
            .Where(h => h.Id == hesapId)
            .Select(h => new KasaHesapBilgisi(h.Id, h.Kod, h.Ad, h.Tur, h.ParaBirimi, h.AktifMi))
            .SingleOrDefaultAsync(cancellationToken);
}

// Faturanın peşin tahsilatı/ödemesi. Carili belgede cari hareketi de yazılır ve önce belgenin kendi kalemini kapatır.
internal sealed class KasaBankaYazici(KasaBankaDbContext db, KasaIslemServisi islemServisi) : IKasaBankaYazici
{
    public async Task<Sonuc> BelgeTahsilatiYazAsync(Guid kaynakBelgeId, BelgeTahsilati t, CancellationToken cancellationToken = default)
    {
        if (t.CariHesapId is { } cariId)
        {
            var sonuc = await islemServisi.TahsilatOdemeAsync(
                new TahsilatOdemeIstegi(t.HesapId, cariId, t.Tarih, t.Tutar, t.Kur, null, t.BelgeNo, t.Aciklama), t.GirisMi, cancellationToken,
                kaynakBelgeId);
            return sonuc.BasariliMi ? Sonuc.Basarili() : sonuc.Hata;
        }

        // Carisiz (nihai tüketici) satış: para doğrudan kasaya/bankaya girer.
        var hesap = await db.Hesaplar.SingleOrDefaultAsync(h => h.Id == t.HesapId, cancellationToken);
        if (hesap is null)
        {
            return KasaBankaHatalari.HesapBulunamadi;
        }

        var hareket = KasaHareketi.Olustur(hesap, Guid.CreateVersion7(), t.GirisMi ? KasaHareketTuru.Tahsilat : KasaHareketTuru.Odeme, t.GirisMi,
            t.Tarih, t.Tutar, t.Kur, t.BelgeNo, t.Aciklama, kaynakBelgeId: kaynakBelgeId);
        if (!hareket.BasariliMi)
        {
            return hareket.Hata;
        }

        db.KasaHareketleri.Add(hareket.Deger);
        await db.SaveChangesAsync(cancellationToken);
        return Sonuc.Basarili();
    }

    public async Task<Sonuc> BelgeIslemleriniIptalEtAsync(Guid kaynakBelgeId, CancellationToken cancellationToken = default)
    {
        var islemler = await db.KasaHareketleri
            .Where(h => h.KaynakBelgeId == kaynakBelgeId && h.IptalHareketId == null && h.IptalEdilenHareketId == null)
            .Select(h => h.IslemId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var islemId in islemler)
        {
            var sonuc = await islemServisi.IptalAsync(islemId, belgedenMi: true, cancellationToken);
            if (!sonuc.BasariliMi)
            {
                return sonuc.Hata;
            }
        }

        return Sonuc.Basarili();
    }
}
