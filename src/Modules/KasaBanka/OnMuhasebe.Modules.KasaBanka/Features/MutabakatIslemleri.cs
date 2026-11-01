using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.KasaBanka.Domain;
using OnMuhasebe.Modules.KasaBanka.Domain.Ekstre;
using OnMuhasebe.Modules.KasaBanka.Infrastructure;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Results;

namespace OnMuhasebe.Modules.KasaBanka.Features;

// Banka mutabakatı: ekstre bakiyesi ile program bakiyesi ve farkı açıklayan hareketler. Kayıt yazmaz.
internal static class MutabakatIslemleri
{
    private const int AzamiGun = 366;

    public static void Map(RouteGroupBuilder modul) =>
        modul.MapGroup("").Yetki(KasaBankaYetkileri.Rapor).MapGet("/hesaplar/{hesapId:guid}/mutabakat", GetirAsync);

    private static async Task<IResult> GetirAsync(Guid hesapId, DateOnly? baslangicTarihi, DateOnly? bitisTarihi, decimal? ekstreBakiyesi, KasaBankaDbContext db, TimeProvider zaman, CancellationToken ct)
    {
        // Varsayılan dönem önceki ayın başından bugüne: ay sonu mutabakatı genelde ertesi ayın ilk günlerinde yapılır.
        var bitis = bitisTarihi ?? TurkiyeSaati.Bugun(zaman);
        var baslangic = baslangicTarihi ?? new DateOnly(bitis.Year, bitis.Month, 1).AddMonths(-1);
        var hesap = await db.Hesaplar.AsNoTracking().SingleOrDefaultAsync(h => h.Id == hesapId, ct);
        if (hesap is null)
        {
            return KasaBankaHatalari.HesapBulunamadi.Problem();
        }

        if (hesap.Tur == HesapTuru.Kasa)
        {
            return KasaBankaHatalari.KasayaEkstreYok.Problem();
        }

        if (bitis < baslangic || bitis.DayNumber - baslangic.DayNumber > AzamiGun)
        {
            return KasaBankaHatalari.MutabakatDonemi.Problem();
        }

        var hareketler = db.KasaHareketleri.AsNoTracking().Where(h => h.HesapId == hesapId && h.Tarih <= bitis);
        var programBakiyesi = await hareketler.SumAsync(h => h.Giris - h.Cikis, ct);

        // Ekstre satırı dönem sonrasındaysa hareket o gün itibarıyla bankada görünmez (zamanlama farkı).
        var yalnizProgramda = await hareketler
            .Where(h => h.Tarih >= baslangic && h.Tur != KasaHareketTuru.Acilis && h.IptalHareketId == null && h.IptalEdilenHareketId == null)
            .Where(h => h.EkstreSatiriId == null || db.EkstreSatirlari.Any(s => s.Id == h.EkstreSatiriId && s.Tarih > bitis))
            .Where(h => !db.EkstreSatirlari.Any(s => s.EslesenHareketId == h.Id && s.Durum == EkstreSatirDurumu.ZatenKayitli && s.Tarih <= bitis))
            .OrderBy(h => h.Tarih)
            .Select(h => new MutabakatHareketi(h.Id, h.Tarih, h.Tur, h.Aciklama, h.Giris - h.Cikis))
            .ToListAsync(ct);

        var satirlar = await db.EkstreSatirlari.AsNoTracking()
            .Where(s => s.HesapId == hesapId && s.Tarih >= baslangic && s.Tarih <= bitis && s.Durum != EkstreSatirDurumu.DahaOnceAktarildi)
            .Select(s => new
            {
                s.Tarih, s.Aciklama, s.Tutar, s.Referans, s.Durum, s.ParmakIzi,
                HareketTarihi = s.EslesenHareketId == null ? null : db.KasaHareketleri.Where(h => h.Id == s.EslesenHareketId).Select(h => (DateOnly?)h.Tarih).FirstOrDefault()
            })
            .ToListAsync(ct);

        // Aynı dosya iki kez yüklenip bırakılmış olabilir: aktarılmış ya da yinelenen satır farka iki kez girmesin.
        var kayitli = satirlar.Where(s => s.Durum is EkstreSatirDurumu.Aktarildi or EkstreSatirDurumu.ZatenKayitli && (s.HareketTarihi is null || s.HareketTarihi <= bitis)).Select(s => s.ParmakIzi).ToHashSet();
        var yalnizEkstrede = satirlar
            .Where(s => !kayitli.Contains(s.ParmakIzi))
            .DistinctBy(s => s.ParmakIzi)
            .OrderBy(s => s.Tarih)
            .Select(s => new MutabakatEkstreSatiri(s.Tarih, s.Aciklama, s.Referans, s.Durum, s.Tutar))
            .ToList();

        var kapanis = await db.EkstreAktarimlari.AsNoTracking()
            .Where(a => a.HesapId == hesapId && a.KapanisBakiyesi != null && a.Bitis != null && a.Bitis <= bitis)
            .OrderByDescending(a => a.Bitis).ThenByDescending(a => a.OlusturmaTarihi)
            .Select(a => new { a.KapanisBakiyesi, a.Bitis, a.DosyaAdi })
            .FirstOrDefaultAsync(ct);

        var ekstre = ekstreBakiyesi ?? kapanis?.KapanisBakiyesi;
        var aciklanamayan = ekstre is { } e
            ? MutabakatHesabi.Aciklanamayan(e, programBakiyesi, yalnizEkstrede.Sum(s => s.Tutar), yalnizProgramda.Sum(h => h.Tutar))
            : (decimal?)null;
        return TypedResults.Ok(new MutabakatRaporu(hesap.Id, hesap.Ad, hesap.ParaBirimi, baslangic, bitis, programBakiyesi, ekstre,
            ekstreBakiyesi is null ? kapanis?.Bitis : null, ekstreBakiyesi is null ? kapanis?.DosyaAdi : null, ekstre - programBakiyesi, aciklanamayan, yalnizProgramda, yalnizEkstrede));
    }
}
