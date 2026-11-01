using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Arac.Contracts;
using OnMuhasebe.Modules.Arac.Domain;
using OnMuhasebe.Modules.Arac.Infrastructure;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Results;

namespace OnMuhasebe.Modules.Arac.Features;

// Araç başına dönem maliyeti: takip kayıtlarından hesaplanır, muhasebe rakamı değildir.
internal static class AracRaporlari
{
    private const int EnCokGun = 1100;

    public static void Map(RouteGroupBuilder grup) =>
        grup.MapGet("/raporlar/maliyet", MaliyetAsync).Yetki(AracYetkileri.Rapor);

    // aracId verilirse tek araç. Sigorta başlangıç (yoksa bitiş) tarihine, MTV ödeme tarihine, hasar olay tarihine göre döneme girer.
    private static async Task<IResult> MaliyetAsync(DateOnly baslangic, DateOnly bitis, Guid? aracId, Guid? isyeriId, AracDbContext db, CancellationToken ct)
    {
        if (bitis < baslangic || bitis.DayNumber - baslangic.DayNumber > EnCokGun)
        {
            return Hata.Dogrulama("donem", "Bitiş başlangıçtan önce olamaz ve dönem en çok üç yıl olabilir.").Problem();
        }

        var araclar = await db.Araclar.AsNoTracking()
            .Where(a => (aracId == null || a.Id == aracId) && (isyeriId == null || a.IsyeriId == isyeriId))
            .OrderBy(a => a.Plaka).Select(a => new { a.Id, a.Plaka }).ToListAsync(ct);
        var idler = araclar.Select(a => a.Id).ToList();

        var masraflar = await db.Masraflar.AsNoTracking()
            .Where(m => idler.Contains(m.AracId) && m.Tarih >= baslangic && m.Tarih <= bitis)
            .Select(m => new { m.AracId, m.Tur, m.Tutar, m.Km, m.Litre }).ToListAsync(ct);
        var sigortalar = await db.Yukumlulukler.AsNoTracking()
            .Where(y => idler.Contains(y.AracId) && y.Tutar != null && (y.BaslangicTarihi ?? y.BitisTarihi) >= baslangic && (y.BaslangicTarihi ?? y.BitisTarihi) <= bitis)
            .Select(y => new { y.AracId, Tutar = y.Tutar!.Value }).ToListAsync(ct);
        var mtvler = await db.MtvKayitlari.AsNoTracking()
            .Where(m => idler.Contains(m.AracId) && m.OdemeTarihi != null && m.OdemeTarihi >= baslangic && m.OdemeTarihi <= bitis)
            .Select(m => new { m.AracId, m.Tutar }).ToListAsync(ct);
        var hasarlar = await db.Hasarlar.AsNoTracking()
            .Where(h => idler.Contains(h.AracId) && h.Tarih >= baslangic && h.Tarih <= bitis)
            .Select(h => new { h.AracId, h.OnarimTutari, h.SigortaOdemesi }).ToListAsync(ct);

        return TypedResults.Ok(araclar.Select(a =>
        {
            var kendi = masraflar.Where(m => m.AracId == a.Id).ToList();
            var yakit = kendi.Where(m => m.Tur == AracMasrafTuru.Yakit).Sum(m => m.Tutar);
            var masraf = kendi.Sum(m => m.Tutar) - yakit;
            var sigorta = sigortalar.Where(s => s.AracId == a.Id).Sum(s => s.Tutar);
            var mtv = mtvler.Where(m => m.AracId == a.Id).Sum(m => m.Tutar);
            var hasar = hasarlar.Where(h => h.AracId == a.Id).Sum(h => h.OnarimTutari - h.SigortaOdemesi);
            var toplam = masraf + yakit + sigorta + mtv + hasar;
            var km = AracHesaplari.DonemKm(kendi.Where(m => m.Km != null).Select(m => m.Km!.Value).ToList());
            var dolumlar = kendi.Where(m => m.Tur == AracMasrafTuru.Yakit && m.Km != null && m.Litre != null).Select(m => (m.Km!.Value, m.Litre!.Value)).ToList();
            return new AracMaliyeti(a.Id, a.Plaka, masraf, yakit, sigorta, mtv, hasar, toplam, km, AracHesaplari.KmBasina(toplam, km), AracHesaplari.Tuketim(dolumlar));
        }).ToList());
    }
}
