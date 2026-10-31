using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Modules.Stok.Infrastructure;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Contracts.Sayfalama;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Stok.Features;

// Lot/SKT ve seri sorguları: belgede lot/seri seçimi, raf ömrü raporu ve seri geçmişi (geri çağırma, garanti, iade).
internal static class IzlenebilirlikIslemleri
{
    public static void Map(RouteGroupBuilder modul)
    {
        // Kartın lot/seri bakiyeleri belge formlarında seçim içindir; firma geneli lot ve seri izleri rapordur.
        var grup = modul.MapGroup("").Yetki(StokYetkileri.Kart);
        grup.MapGet("/kartlar/{kartId:guid}/lotlar", (Guid kartId, Guid? depoId, IzlenebilirlikServisi s, CancellationToken ct) =>
            s.LotBakiyeleri(kartId, depoId).ToListAsync(ct));
        grup.MapGet("/kartlar/{kartId:guid}/seriler", (Guid kartId, Guid? depoId, IzlenebilirlikServisi s, CancellationToken ct) =>
            s.StoktakiSerilerAsync(kartId, depoId, null, ct));
        grup.MapGet("/lotlar", LotlarAsync).Yetki(StokYetkileri.Rapor);
        grup.MapGet("/seri-gecmisi", SeriGecmisiAsync).Yetki(StokYetkileri.Rapor);
    }

    // Stoktaki lotlar SKT sırasıyla; gun verilirse yalnızca SKT'si o kadar gün içinde dolan ya da geçmiş lotlar.
    private static async Task<IResult> LotlarAsync(
        Guid? depoId, int? gun, string? arama, int? sayfa, int? boyut, IzlenebilirlikServisi servis, TimeProvider zaman,
        CancellationToken cancellationToken)
    {
        var sorgu = servis.LotBakiyeleri(null, depoId);
        if (gun is { } g)
        {
            var sinir = TurkiyeSaati.Bugun(zaman).AddDays(g);
            sorgu = sorgu.Where(l => l.SonKullanmaTarihi != null && l.SonKullanmaTarihi <= sinir);
        }

        if (!string.IsNullOrWhiteSpace(arama))
        {
            var desen = AramaDeseni.Icerir(arama);
            sorgu = sorgu.Where(l => EF.Functions.ILike(l.StokAdi, desen) || EF.Functions.ILike(l.StokKodu, desen) || EF.Functions.ILike(l.LotNo, desen));
        }

        return TypedResults.Ok(await sorgu.SayfalaAsync(new SayfalamaIstegi(sayfa ?? 1, boyut ?? SayfalamaIstegi.VarsayilanBoyut), cancellationToken));
    }

    private static async Task<IResult> SeriGecmisiAsync(string no, StokDbContext db, CancellationToken cancellationToken)
    {
        var seri = no.Trim();
        return TypedResults.Ok(await (
                from s in db.StokHareketSerileri.AsNoTracking()
                join h in db.StokHareketleri on s.HareketId equals h.Id
                join k in db.StokKartlari on h.StokKartiId equals k.Id
                join d in db.Depolar on h.DepoId equals d.Id
                where s.SeriNo == seri
                orderby h.Tarih, h.OlusturmaTarihi
                select new SeriHareketi(h.Tarih, h.Tur, k.Id, k.Kod, k.Ad, d.Ad, h.GirisMiktari > 0, h.LotNo, h.BelgeNo, h.Aciklama, h.KaynakBelgeId,
                    h.IptalHareketId != null || h.IptalEdilenHareketId != null))
            .ToListAsync(cancellationToken));
    }
}
