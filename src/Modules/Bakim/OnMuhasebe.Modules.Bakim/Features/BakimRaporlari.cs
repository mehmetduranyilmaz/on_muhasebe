using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Bakim.Contracts;
using OnMuhasebe.Modules.Bakim.Domain;
using OnMuhasebe.Modules.Bakim.Infrastructure;
using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Results;

namespace OnMuhasebe.Modules.Bakim.Features;

// Ekipman başına dönem maliyeti ve duruşu: dönemde kapanan iş emirlerinden. Takip rakamıdır, muhasebe rakamı değildir.
internal static class BakimRaporlari
{
    private const int EnCokGun = 1100;

    public static void Map(RouteGroupBuilder grup) =>
        grup.MapGet("/raporlar/maliyet", MaliyetAsync).Yetki(BakimYetkileri.Rapor);

    private static async Task<IResult> MaliyetAsync(
        DateOnly baslangic, DateOnly bitis, Guid? ekipmanId, Guid? isyeriId, BakimDbContext db, IStokSorgusu stok, IYetkiDenetimi yetki, ILisansSorgusu lisans, IMevcutKullanici kullanici,
        CancellationToken ct)
    {
        if (bitis < baslangic || bitis.DayNumber - baslangic.DayNumber > EnCokGun)
        {
            return Hata.Dogrulama("donem", "Bitiş başlangıçtan önce olamaz ve dönem en çok üç yıl olabilir.").Problem();
        }

        var bas = DateTime.SpecifyKind(baslangic.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime();
        var son = DateTime.SpecifyKind(bitis.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime();
        var ekipmanlar = await db.Ekipmanlar.AsNoTracking()
            .Where(e => (ekipmanId == null || e.Id == ekipmanId) && (isyeriId == null || e.IsyeriId == isyeriId))
            .OrderBy(e => e.Kod).Select(e => new { e.Id, e.Kod, e.Ad }).ToListAsync(ct);
        var idler = ekipmanlar.Select(e => e.Id).ToList();
        var emirler = await db.IsEmirleri.AsNoTracking().Include(i => i.Parcalar)
            .Where(i => idler.Contains(i.EkipmanId) && i.Durum == IsEmriDurumu.Kapandi && i.BitisZamani >= bas && i.BitisZamani < son)
            .ToListAsync(ct);

        // Stoktan düşen parçanın maliyeti stok modülündeki gerçek sarf maliyetidir; görme yetkisi ayrıdır.
        var moduller = (await lisans.GetirAsync(kullanici.FirmaId!.Value, ct))?.Moduller ?? [];
        var stokMaliyetiGorur = moduller.Contains(ModulKodlari.Stok) && await yetki.VarMiAsync("stok.maliyet.goruntule", ct);
        var stokMaliyetleri = new Dictionary<Guid, decimal>();
        if (stokMaliyetiGorur)
        {
            var belgeler = emirler.Where(e => e.Parcalar.Any(p => p.StokKartiId is not null)).Select(e => e.Id).ToList();
            if (belgeler.Count > 0)
            {
                stokMaliyetleri = (await stok.BelgeMaliyetleriAsync(belgeler, ct)).GroupBy(m => m.KaynakBelgeId).ToDictionary(g => g.Key, g => g.Sum(m => m.CikisMaliyetiTl));
            }
        }

        return TypedResults.Ok(ekipmanlar.Select(e =>
        {
            var kendi = emirler.Where(i => i.EkipmanId == e.Id).ToList();
            var parca = kendi.Sum(i => BakimHesaplari.ElleParcaTutari(i.Parcalar) + stokMaliyetleri.GetValueOrDefault(i.Id));
            var iscilik = kendi.Sum(i => i.IscilikTutari);
            var disServis = kendi.Sum(i => i.DisServisTutari);
            return new BakimMaliyeti(e.Id, e.Kod, e.Ad, Math.Round(parca, 2, MidpointRounding.AwayFromZero), iscilik, disServis,
                Math.Round(parca + iscilik + disServis, 2, MidpointRounding.AwayFromZero), kendi.Count(i => i.Tur == IsEmriTuru.Ariza), kendi.Count(i => i.Tur != IsEmriTuru.Ariza),
                kendi.Sum(i => i.DurusSaati ?? 0), stokMaliyetiGorur);
        }).ToList());
    }
}
