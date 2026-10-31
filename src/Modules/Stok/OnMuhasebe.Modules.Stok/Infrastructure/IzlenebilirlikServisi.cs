using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Modules.Stok.Domain;
using OnMuhasebe.Shared.Contracts.Birimler;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Stok.Infrastructure;

// Lot ve seri bakiyeleri hareketlerden hesaplanır (ters kayıtlar giriş/çıkışı tersine çevirdiği için toplam tutarlıdır).
internal sealed class IzlenebilirlikServisi(StokDbContext db, StokAyarServisi ayarServisi)
{
    private static readonly CultureInfo _tr = CultureInfo.GetCultureInfo("tr-TR");

    public IQueryable<LotBakiyesi> LotBakiyeleri(Guid? kartId, Guid? depoId) =>
        from g in db.StokHareketleri.AsNoTracking()
            .Where(h => h.LotNo != null && (kartId == null || h.StokKartiId == kartId) && (depoId == null || h.DepoId == depoId))
            .GroupBy(h => new { h.StokKartiId, h.DepoId, h.LotNo })
            .Select(g => new
            {
                g.Key.StokKartiId,
                g.Key.DepoId,
                LotNo = g.Key.LotNo!,
                Skt = g.Max(h => h.SonKullanmaTarihi),
                IlkGiris = g.Min(h => h.Tarih),
                Miktar = g.Sum(h => h.GirisMiktari - h.CikisMiktari)
            })
        where g.Miktar > 0
        join k in db.StokKartlari on g.StokKartiId equals k.Id
        join d in db.Depolar on g.DepoId equals d.Id
        orderby g.Skt == null, g.Skt, g.IlkGiris, g.LotNo
        select new LotBakiyesi(k.Id, k.Kod, k.Ad, k.TemelBirim, d.Id, d.Ad, g.LotNo, g.Skt, g.IlkGiris, g.Miktar);

    // Stoktaki seriler: seri başına depo bazında giriş sayısı çıkıştan fazlaysa seri o depodadır.
    public async Task<List<StoktakiSeri>> StoktakiSerilerAsync(
        Guid kartId, Guid? depoId, IReadOnlyCollection<string>? seriler, CancellationToken cancellationToken)
    {
        var sorgu = from s in db.StokHareketSerileri.AsNoTracking()
                    join h in db.StokHareketleri on s.HareketId equals h.Id
                    where s.StokKartiId == kartId && (depoId == null || h.DepoId == depoId)
                    select new { s.SeriNo, h.DepoId, Isaret = h.GirisMiktari > 0 ? 1 : -1, h.LotNo, h.SonKullanmaTarihi };
        if (seriler is not null)
        {
            sorgu = sorgu.Where(x => seriler.Contains(x.SeriNo));
        }

        var bakiyeler = await sorgu
            .GroupBy(x => new { x.SeriNo, x.DepoId })
            .Select(g => new { g.Key.SeriNo, g.Key.DepoId, Net = g.Sum(x => x.Isaret), LotNo = g.Max(x => x.LotNo), Skt = g.Max(x => x.SonKullanmaTarihi) })
            .Where(x => x.Net > 0)
            .ToListAsync(cancellationToken);
        var depolar = await db.Depolar.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Ad, cancellationToken);
        return bakiyeler.OrderBy(b => b.SeriNo, StringComparer.Ordinal)
            .Select(b => new StoktakiSeri(b.SeriNo, b.DepoId, depolar.GetValueOrDefault(b.DepoId, "?"), b.LotNo, b.Skt))
            .ToList();
    }

    // Kaydedilmeden önce yeni hareketlerin lot/seri tutarlılığı denetlenir; çıkış ve transfer girişine lotun SKT'si işlenir.
    // Seri kesin kuraldır (olmayan seri çıkamaz, stoktaki seri yeniden giremez); lot yetersizliğinde eksi stok ayarı uygulanır.
    public async Task<Sonuc<List<string>>> KontrolEtAsync(
        IReadOnlyList<StokHareket> yeniler, IReadOnlyDictionary<Guid, StokKarti> kartlar, CancellationToken cancellationToken)
    {
        var uyarilar = new List<string>();
        var lotlular = yeniler.Where(h => h.LotNo is not null).ToList();
        foreach (var grup in lotlular.GroupBy(h => (h.StokKartiId, h.LotNo)))
        {
            if (kartlar[grup.Key.StokKartiId].SktTakibi && grup.Any(h => h.SonKullanmaTarihi is null))
            {
                var skt = await db.StokHareketleri.AsNoTracking()
                    .Where(h => h.StokKartiId == grup.Key.StokKartiId && h.LotNo == grup.Key.LotNo && h.SonKullanmaTarihi != null)
                    .Select(h => h.SonKullanmaTarihi).FirstOrDefaultAsync(cancellationToken);
                foreach (var h in grup)
                {
                    h.LotSktAta(skt ?? grup.Max(x => x.SonKullanmaTarihi));
                }
            }
        }

        var ayar = await ayarServisi.GetirAsync(cancellationToken);
        foreach (var cikis in lotlular.Where(h => h.CikisMiktari > 0).GroupBy(h => (h.StokKartiId, h.DepoId, h.LotNo)))
        {
            var kart = kartlar[cikis.Key.StokKartiId];
            var istenen = cikis.Sum(h => h.CikisMiktari);
            var mevcut = await db.StokHareketleri
                .Where(h => h.StokKartiId == cikis.Key.StokKartiId && h.DepoId == cikis.Key.DepoId && h.LotNo == cikis.Key.LotNo)
                .SumAsync(h => h.GirisMiktari - h.CikisMiktari, cancellationToken);
            if (mevcut < istenen && ayar.EksiStokDavranisi == EksiStokDavranisi.Engelle)
            {
                return KartAdiyla(kart, StokHatalari.YetersizLot(cikis.Key.LotNo!, mevcut, kart.TemelBirim));
            }

            if (mevcut < istenen && ayar.EksiStokDavranisi == EksiStokDavranisi.Uyar)
            {
                uyarilar.Add(string.Create(_tr, $"{kart.Ad}: {cikis.Key.LotNo} lotunda {mevcut:#,0.####} {Birimler.Ad(kart.TemelBirim)} vardı, lot eksiye düştü."));
            }
        }

        foreach (var h in yeniler.Where(h => h.Tur == StokHareketTuru.SatisFaturasi && h.CikisMiktari > 0 && h.SonKullanmaTarihi < h.Tarih))
        {
            uyarilar.Add($"{kartlar[h.StokKartiId].Ad}: {h.LotNo} lotunun son kullanma tarihi ({h.SonKullanmaTarihi:dd.MM.yyyy}) geçmiş.");
        }

        foreach (var kartGrubu in yeniler.Where(h => h.Seriler.Count > 0).GroupBy(h => h.StokKartiId))
        {
            var kart = kartlar[kartGrubu.Key];
            var tumu = kartGrubu.SelectMany(h => h.Seriler.Select(s => s.SeriNo)).Distinct().ToList();
            var stoktakiler = await StoktakiSerilerAsync(kart.Id, null, tumu, cancellationToken);
            foreach (var h in kartGrubu)
            {
                foreach (var seri in h.Seriler.Select(s => s.SeriNo))
                {
                    if (h.CikisMiktari > 0 && !stoktakiler.Any(s => s.SeriNo == seri && s.DepoId == h.DepoId))
                    {
                        return KartAdiyla(kart, StokHatalari.SeriStoktaYok(seri));
                    }

                    if (h.GirisMiktari > 0 && h.Tur != StokHareketTuru.TransferGiris && stoktakiler.Any(s => s.SeriNo == seri))
                    {
                        return KartAdiyla(kart, StokHatalari.SeriZatenStokta(seri));
                    }
                }
            }

            // Aynı belgede aynı seri iki kez çıkamaz/giremez (transferin iki bacağı hariç).
            var tekrar = kartGrubu.Where(h => h.Tur != StokHareketTuru.TransferGiris)
                .SelectMany(h => h.Seriler.Select(s => s.SeriNo)).GroupBy(s => s).FirstOrDefault(g => g.Count() > 1);
            if (tekrar is not null)
            {
                return KartAdiyla(kart, StokHatalari.SeriZatenStokta(tekrar.Key));
            }
        }

        return uyarilar;
    }

    private static Hata KartAdiyla(StokKarti kart, Hata hata) => hata with { Mesaj = $"{kart.Ad}: {hata.Mesaj}" };
}
