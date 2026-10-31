using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Modules.Stok.Domain;
using OnMuhasebe.Modules.Stok.Infrastructure;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Stok.Features;

internal static class FiyatListesiIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var g = grup.MapGroup("/fiyat-listeleri").Yetki(StokYetkileri.Fiyat);
        g.MapGet("", ListeleAsync);
        g.MapGet("/uygulanan", UygulananAsync);
        g.MapGet("/{id:guid}", GetirAsync);
        g.MapPost("", OlusturAsync).Dogrula<FiyatListesiKaydetIstegi>();
        g.MapPut("/{id:guid}", GuncelleAsync).Dogrula<FiyatListesiKaydetIstegi>();
        g.MapDelete("/{id:guid}", SilAsync);
    }

    internal sealed class Dogrulayici : AbstractValidator<FiyatListesiKaydetIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.Ad).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Tur).IsInEnum();
            RuleFor(x => x.ParaBirimi).NotEmpty().Must(ParaBirimleri.GecerliMi).WithMessage("Geçersiz para birimi.");
            RuleFor(x => x.Aciklama).MaximumLength(500);
            RuleFor(x => x.Hedefler).NotNull().Must(h => h.Count <= 1000);
            RuleForEach(x => x.Hedefler).ChildRules(h =>
            {
                h.RuleFor(x => x.Tur).IsInEnum();
                h.RuleFor(x => x.Ad).NotEmpty().MaximumLength(250);
            });
            RuleFor(x => x.Satirlar).NotNull().Must(s => s.Count <= FiyatListesiKurallari.AzamiSatirSayisi);
            RuleForEach(x => x.Satirlar).ChildRules(s =>
            {
                s.RuleFor(x => x.MinMiktar).GreaterThanOrEqualTo(0).LessThan(1_000_000_000m);
                s.RuleFor(x => x.Fiyat).GreaterThanOrEqualTo(0).LessThan(1_000_000_000_000m);
                s.RuleFor(x => x.Iskonto).InclusiveBetween(0, 100);
            });
        }
    }

    private static async Task<IResult> ListeleAsync(StokDbContext db, TimeProvider zaman, FiyatListesiTuru? tur, CancellationToken cancellationToken)
    {
        var sorgu = db.FiyatListeleri.AsNoTracking();
        if (tur is not null)
        {
            sorgu = sorgu.Where(l => l.Tur == tur);
        }

        var bugun = TurkiyeSaati.Bugun(zaman);
        var listeler = await sorgu.OrderBy(l => l.Tur).ThenBy(l => l.Ad)
            .Select(l => new { l.Id, l.Ad, l.Tur, l.ParaBirimi, l.KdvDahilMi, l.BaslangicTarihi, l.BitisTarihi, l.AktifMi, Hedef = l.Atamalar.Count, Satir = l.Satirlar.Count })
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(listeler.Select(l => new FiyatListesiOzeti(
            l.Id, l.Ad, l.Tur, l.ParaBirimi, l.KdvDahilMi, l.BaslangicTarihi, l.BitisTarihi, l.AktifMi, l.Hedef, l.Satir,
            FiyatListesiKurallari.GecerliMi(l.AktifMi, l.BaslangicTarihi, l.BitisTarihi, bugun))).ToList());
    }

    private static async Task<IResult> GetirAsync(Guid id, StokDbContext db, CancellationToken cancellationToken) =>
        await DetayAsync(db, id, cancellationToken) is { } detay ? TypedResults.Ok(detay) : FiyatListesiHatalari.Bulunamadi.Problem();

    private static async Task<IResult> OlusturAsync(FiyatListesiKaydetIstegi istek, StokDbContext db, CancellationToken cancellationToken)
    {
        if (await DenetleAsync(db, istek, null, cancellationToken) is { } hata)
        {
            return hata.Problem();
        }

        var olusturma = FiyatListesi.Olustur(istek);
        if (!olusturma.BasariliMi)
        {
            return olusturma.Hata.Problem();
        }

        db.FiyatListeleri.Add(olusturma.Deger);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/stok/fiyat-listeleri/{olusturma.Deger.Id}", await DetayAsync(db, olusturma.Deger.Id, cancellationToken));
    }

    private static async Task<IResult> GuncelleAsync(Guid id, FiyatListesiKaydetIstegi istek, StokDbContext db, CancellationToken cancellationToken)
    {
        var liste = await db.FiyatListeleri.Include(l => l.Satirlar).Include(l => l.Atamalar).SingleOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (liste is null)
        {
            return FiyatListesiHatalari.Bulunamadi.Problem();
        }

        db.Entry(liste).Property(l => l.Surum).OriginalValue = istek.Surum;
        if (await DenetleAsync(db, istek, id, cancellationToken) is { } hata)
        {
            return hata.Problem();
        }

        var sonuc = liste.Ayarla(istek);
        if (!sonuc.BasariliMi)
        {
            return sonuc.Hata.Problem();
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(await DetayAsync(db, id, cancellationToken));
    }

    private static async Task<IResult> SilAsync(Guid id, StokDbContext db, CancellationToken cancellationToken)
    {
        var liste = await db.FiyatListeleri.SingleOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (liste is null)
        {
            return FiyatListesiHatalari.Bulunamadi.Problem();
        }

        db.FiyatListeleri.Remove(liste);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    // Kartların bu cari için uygulanacak listesi: tarihte geçerli, türü uygun listeler öncelik sırasıyla (cariye özel > grup > genel,
    // eşitlikte en geç başlayan) taranır; kartın satırı bulunan ilk liste kullanılır. Listede olmayan kart için kart fiyatı geçerlidir.
    private static async Task<IResult> UygulananAsync(
        Guid[] stokKartiId, Guid? cariId, Guid? cariGrupId, DateOnly tarih, FiyatListesiTuru tur, StokDbContext db, CancellationToken cancellationToken)
    {
        var idler = stokKartiId.Distinct().Take(500).ToList();
        if (idler.Count == 0)
        {
            return TypedResults.Ok(Array.Empty<UygulananFiyat>());
        }

        var listeler = await db.FiyatListeleri.AsNoTracking().Include(l => l.Atamalar)
            .Where(l => l.AktifMi && l.Tur == tur
                        && (l.BaslangicTarihi == null || l.BaslangicTarihi <= tarih) && (l.BitisTarihi == null || tarih <= l.BitisTarihi)
                        && l.Satirlar.Any(s => idler.Contains(s.StokKartiId)))
            .ToListAsync(cancellationToken);
        var sirali = listeler
            .Select(l => (Liste: l, Oncelik: FiyatListesiKurallari.Oncelik(l.Atamalar.Select(a => new FiyatListesiHedefi(a.Tur, a.HedefId, a.Ad)).ToList(), cariId, cariGrupId)))
            .Where(x => x.Oncelik is not null)
            .OrderByDescending(x => x.Oncelik).ThenByDescending(x => x.Liste.BaslangicTarihi).ThenByDescending(x => x.Liste.OlusturmaTarihi)
            .Select(x => x.Liste)
            .ToList();
        if (sirali.Count == 0)
        {
            return TypedResults.Ok(Array.Empty<UygulananFiyat>());
        }

        var listeIdleri = sirali.Select(l => l.Id).ToList();
        var satirlar = await db.FiyatListesiSatirlari.AsNoTracking()
            .Where(s => listeIdleri.Contains(s.FiyatListesiId) && idler.Contains(s.StokKartiId))
            .ToListAsync(cancellationToken);

        var sonuc = new List<UygulananFiyat>();
        foreach (var kartId in idler)
        {
            foreach (var liste in sirali)
            {
                var kademeler = satirlar.Where(s => s.FiyatListesiId == liste.Id && s.StokKartiId == kartId)
                    .OrderBy(s => s.MinMiktar).Select(s => new FiyatKademesi(s.MinMiktar, s.Fiyat, s.Iskonto)).ToList();
                if (kademeler.Count > 0)
                {
                    sonuc.Add(new UygulananFiyat(kartId, liste.Id, liste.Ad, liste.ParaBirimi, liste.KdvDahilMi, kademeler));
                    break;
                }
            }
        }

        return TypedResults.Ok(sonuc);
    }

    private static async Task<Shared.Domain.Results.Hata?> DenetleAsync(StokDbContext db, FiyatListesiKaydetIstegi istek, Guid? id, CancellationToken cancellationToken)
    {
        var ad = istek.Ad.Trim();
        if (await db.FiyatListeleri.AnyAsync(l => l.Ad == ad && l.Id != id, cancellationToken))
        {
            return StokHatalari.AdKullaniliyor;
        }

        var kartIdleri = istek.Satirlar.Select(s => s.StokKartiId).Distinct().ToList();
        var bulunan = await db.StokKartlari.CountAsync(k => kartIdleri.Contains(k.Id), cancellationToken);
        return bulunan == kartIdleri.Count ? null : FiyatListesiHatalari.KartBulunamadi;
    }

    private static async Task<FiyatListesiDetayi?> DetayAsync(StokDbContext db, Guid id, CancellationToken cancellationToken)
    {
        var liste = await db.FiyatListeleri.AsNoTracking().Include(l => l.Atamalar).SingleOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (liste is null)
        {
            return null;
        }

        var satirlar = await (
                from s in db.FiyatListesiSatirlari.AsNoTracking()
                join k in db.StokKartlari on s.StokKartiId equals k.Id
                where s.FiyatListesiId == id
                orderby k.Kod, s.MinMiktar
                select new FiyatListesiSatiriDetayi(s.StokKartiId, k.Kod, k.Ad, k.TemelBirim, s.MinMiktar, s.Fiyat, s.Iskonto))
            .ToListAsync(cancellationToken);

        return new FiyatListesiDetayi(
            liste.Id, liste.Ad, liste.Tur, liste.ParaBirimi, liste.KdvDahilMi, liste.BaslangicTarihi, liste.BitisTarihi, liste.AktifMi, liste.Aciklama,
            liste.Atamalar.OrderBy(a => a.Tur).ThenBy(a => a.Ad).Select(a => new FiyatListesiHedefi(a.Tur, a.HedefId, a.Ad)).ToList(),
            satirlar, liste.Surum);
    }
}
