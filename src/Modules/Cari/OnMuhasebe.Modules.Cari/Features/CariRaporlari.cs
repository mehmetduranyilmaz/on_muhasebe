using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.Cari.Domain;
using OnMuhasebe.Modules.Cari.Infrastructure;
using OnMuhasebe.Modules.Doviz.Contracts;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Shared.Contracts.Sayfalama;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;

namespace OnMuhasebe.Modules.Cari.Features;

internal static class CariRaporlari
{
    public static void Map(RouteGroupBuilder grup)
    {
        grup.MapGet("/hesaplar/{cariId:guid}/ekstre", EkstreAsync);
        grup.MapGet("/raporlar/bakiye-listesi", BakiyeListesiAsync);
    }

    private static async Task<IResult> EkstreAsync(
        Guid cariId,
        DateOnly? baslangic,
        DateOnly? bitis,
        CariDbContext db,
        TimeProvider zaman,
        CancellationToken cancellationToken)
    {
        var bugun = TurkiyeSaati.Bugun(zaman);
        var bas = baslangic ?? new DateOnly(bugun.Year, 1, 1);
        var son = bitis ?? bugun;
        if (bas > son)
        {
            return Hata.Dogrulama("tarih_araligi", "Başlangıç tarihi bitiş tarihinden sonra olamaz.").Problem();
        }

        var cari = await db.CariHesaplar.AsNoTracking()
            .Where(c => c.Id == cariId)
            .Select(c => new { c.Id, c.Kod, c.Unvan, c.ParaBirimi })
            .SingleOrDefaultAsync(cancellationToken);
        if (cari is null)
        {
            return CariHatalari.CariBulunamadi.Problem();
        }

        var hareketler = db.CariHareketler.AsNoTracking().Where(h => h.CariHesapId == cariId);
        var devir = await hareketler
            .Where(h => h.Tarih < bas)
            .GroupBy(_ => 1)
            .Select(g => new { Bakiye = g.Sum(h => h.Borc - h.Alacak), BakiyeTl = g.Sum(h => h.BorcTl - h.AlacakTl) })
            .SingleOrDefaultAsync(cancellationToken);

        var satirlar = await hareketler
            .Where(h => h.Tarih >= bas && h.Tarih <= son)
            .OrderBy(h => h.Tarih).ThenBy(h => h.Id)
            .Select(h => new EkstreSatiri(
                h.Id, h.Tarih, h.VadeTarihi, h.Tur, h.BelgeNo, h.Aciklama, h.Borc, h.Alacak, 0m, h.Kur, h.BorcTl, h.AlacakTl, 0m))
            .ToListAsync(cancellationToken);

        var hesap = CariHesaplamalari.Ekstre(devir?.Bakiye ?? 0m, devir?.BakiyeTl ?? 0m, satirlar);
        return TypedResults.Ok(new EkstreCevabi(
            cari.Id, cari.Kod, cari.Unvan, cari.ParaBirimi, bas, son, devir?.Bakiye ?? 0m, devir?.BakiyeTl ?? 0m,
            hesap.Satirlar, hesap.ToplamBorc, hesap.ToplamAlacak, hesap.SonBakiye,
            hesap.ToplamBorcTl, hesap.ToplamAlacakTl, hesap.SonBakiyeTl));
    }

    private static async Task<IResult> BakiyeListesiAsync(
        CariDbContext db,
        IKurSorgusu kurSorgusu,
        TimeProvider zaman,
        DateOnly? tarih,
        BakiyeDurumu? durum,
        CariTuru? tur,
        Guid? grupId,
        string? paraBirimi,
        string? arama,
        int? sayfa,
        int? boyut,
        CancellationToken cancellationToken)
    {
        var raporTarihi = tarih ?? TurkiyeSaati.Bugun(zaman);

        var cariler = db.CariHesaplar.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(arama))
        {
            var desen = $"%{arama.Trim()}%";
            cariler = cariler.Where(c => EF.Functions.ILike(c.Unvan, desen) || EF.Functions.ILike(c.Kod, desen));
        }

        if (tur is { } secilenTur)
        {
            cariler = cariler.Where(c => c.Tur == secilenTur || c.Tur == CariTuru.MusteriVeTedarikci);
        }

        if (grupId is not null)
        {
            cariler = cariler.Where(c => c.GrupId == grupId);
        }

        if (!string.IsNullOrWhiteSpace(paraBirimi))
        {
            cariler = cariler.Where(c => c.ParaBirimi == paraBirimi);
        }

        // Her cari için toplamlar (firma_id, cari_hesap_id, tarih) covering index'inden karşılanır.
        var hareketler = db.CariHareketler.Where(h => h.Tarih <= raporTarihi);
        var sorgu = cariler.Select(c => new
        {
            c.Id,
            c.Kod,
            c.Unvan,
            c.ParaBirimi,
            Borc = hareketler.Where(h => h.CariHesapId == c.Id).Sum(h => h.Borc),
            Alacak = hareketler.Where(h => h.CariHesapId == c.Id).Sum(h => h.Alacak),
            BakiyeTl = hareketler.Where(h => h.CariHesapId == c.Id).Sum(h => h.BorcTl - h.AlacakTl)
        });

        sorgu = durum switch
        {
            BakiyeDurumu.Borclu => sorgu.Where(x => x.Borc - x.Alacak > 0),
            BakiyeDurumu.Alacakli => sorgu.Where(x => x.Borc - x.Alacak < 0),
            BakiyeDurumu.SifirHaric => sorgu.Where(x => x.Borc - x.Alacak != 0),
            _ => sorgu
        };

        var paraBirimiToplamlari = await sorgu
            .GroupBy(x => x.ParaBirimi)
            .Select(g => new { ParaBirimi = g.Key, Borc = g.Sum(x => x.Borc), Alacak = g.Sum(x => x.Alacak), BakiyeTl = g.Sum(x => x.BakiyeTl) })
            .OrderBy(x => x.ParaBirimi)
            .ToListAsync(cancellationToken);

        var sayfaSonucu = await sorgu
            .OrderBy(x => x.Unvan)
            .SayfalaAsync(new SayfalamaIstegi(sayfa ?? 1, boyut ?? SayfalamaIstegi.VarsayilanBoyut), cancellationToken);

        // Kur farkı için rapor tarihinde geçerli kur; her para birimi için tek sorgu.
        var guncelKurlar = new Dictionary<string, decimal>();
        foreach (var pb in paraBirimiToplamlari.Select(x => x.ParaBirimi).Where(pb => pb != ParaBirimleri.Tl))
        {
            if (await kurSorgusu.IslemKuruAsync(pb, raporTarihi, cancellationToken) is { } kur)
            {
                guncelKurlar[pb] = kur.DovizAlis;
            }
        }

        var satirlar = sayfaSonucu.Kayitlar.Select(x =>
        {
            var bakiye = x.Borc - x.Alacak;
            var (guncelKur, guncelTl, kurFarki) = Degerle(x.ParaBirimi, bakiye, x.BakiyeTl, guncelKurlar);
            return new BakiyeSatiri(x.Id, x.Kod, x.Unvan, x.ParaBirimi, x.Borc, x.Alacak, bakiye, x.BakiyeTl, guncelKur, guncelTl, kurFarki);
        }).ToList();

        var toplamSatirlari = paraBirimiToplamlari.Select(x =>
        {
            var bakiye = x.Borc - x.Alacak;
            var (_, guncelTl, kurFarki) = Degerle(x.ParaBirimi, bakiye, x.BakiyeTl, guncelKurlar);
            return new ParaBirimiToplami(x.ParaBirimi, x.Borc, x.Alacak, bakiye, x.BakiyeTl, guncelTl, kurFarki);
        }).ToList();

        return TypedResults.Ok(new BakiyeListesiCevabi(
            raporTarihi,
            new SayfaliSonuc<BakiyeSatiri>(satirlar, sayfaSonucu.Sayfa, sayfaSonucu.Boyut, sayfaSonucu.ToplamKayit),
            toplamSatirlari));
    }

    private static (decimal? GuncelKur, decimal? GuncelTl, decimal? KurFarki) Degerle(
        string paraBirimi, decimal bakiye, decimal bakiyeTl, Dictionary<string, decimal> guncelKurlar)
    {
        if (paraBirimi == ParaBirimleri.Tl || !guncelKurlar.TryGetValue(paraBirimi, out var kur))
        {
            return (null, null, null);
        }

        var (guncelTl, kurFarki) = CariHesaplamalari.KurFarki(bakiye, bakiyeTl, kur);
        return (kur, guncelTl, kurFarki);
    }
}
