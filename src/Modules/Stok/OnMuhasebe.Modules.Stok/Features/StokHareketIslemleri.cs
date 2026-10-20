using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Modules.Stok.Domain;
using OnMuhasebe.Modules.Stok.Infrastructure;
using OnMuhasebe.Shared.Contracts.Sayfalama;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Stok.Features;

// Elle girilen hareketler (açılış, sayım farkı, fire, sarf), depolar arası transfer ve ters kayıtla iptal.
internal static class StokHareketIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        grup.MapGet("/kartlar/{kartId:guid}/hareketler", ListeleAsync);
        grup.MapPost("/kartlar/{kartId:guid}/hareketler", EkleAsync).Dogrula<StokHareketKaydetIstegi>();
        grup.MapPost("/kartlar/{kartId:guid}/transferler", TransferAsync).Dogrula<TransferIstegi>();
        grup.MapPost("/hareketler/{hareketId:guid}/iptal", IptalEtAsync);
    }

    internal sealed class Dogrulayici : AbstractValidator<StokHareketKaydetIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.Tur).Must(StokHareket.ElleGirilebilirMi)
                .WithMessage("Elle yalnızca açılış, sayım fazlası/eksiği, fire ve sarf girilebilir.");
            RuleFor(x => x.Miktar).GreaterThan(0).LessThan(1_000_000_000m);
            RuleFor(x => x.BirimFiyat).GreaterThanOrEqualTo(0);
            RuleFor(x => x.BelgeNo).MaximumLength(50);
            RuleFor(x => x.Aciklama).MaximumLength(500);
        }
    }

    internal sealed class TransferDogrulayici : AbstractValidator<TransferIstegi>
    {
        public TransferDogrulayici()
        {
            RuleFor(x => x.Miktar).GreaterThan(0).LessThan(1_000_000_000m);
            RuleFor(x => x.HedefDepoId).NotEqual(x => x.KaynakDepoId).WithMessage("Kaynak ve hedef depo farklı olmalıdır.");
            RuleFor(x => x.Aciklama).MaximumLength(500);
        }
    }

    private static async Task<IResult> ListeleAsync(
        Guid kartId, StokDbContext db, int? sayfa, int? boyut, CancellationToken cancellationToken)
    {
        if (!await db.StokKartlari.AnyAsync(k => k.Id == kartId, cancellationToken))
        {
            return StokHatalari.KartBulunamadi.Problem();
        }

        var sonuc = await (
                from h in db.StokHareketleri.AsNoTracking()
                join d in db.Depolar on h.DepoId equals d.Id
                where h.StokKartiId == kartId
                orderby h.Tarih descending, h.Id descending
                select new StokHareketSatiri(
                    h.Id, h.Tarih, h.Tur, d.Ad, h.GirisMiktari, h.CikisMiktari, h.GirilenBirim, h.GirilenMiktar, h.BirimFiyat,
                    h.BelgeNo, h.Aciklama, h.IptalHareketId != null, h.IptalEdilenHareketId != null,
                    h.IptalHareketId == null && h.IptalEdilenHareketId == null
                    && (h.Tur == StokHareketTuru.Acilis || h.Tur == StokHareketTuru.SayimFazlasi || h.Tur == StokHareketTuru.SayimEksigi
                        || h.Tur == StokHareketTuru.Fire || h.Tur == StokHareketTuru.Sarf
                        || h.Tur == StokHareketTuru.TransferGiris || h.Tur == StokHareketTuru.TransferCikis)))
            .SayfalaAsync(new SayfalamaIstegi(sayfa ?? 1, boyut ?? SayfalamaIstegi.VarsayilanBoyut), cancellationToken);

        return TypedResults.Ok(sonuc);
    }

    private static async Task<IResult> EkleAsync(
        Guid kartId, StokHareketKaydetIstegi istek, StokDbContext db, DepoServisi depoServisi, StokCikisKontrolu cikisKontrolu,
        CancellationToken cancellationToken)
    {
        var kart = await db.StokKartlari.Include(k => k.Birimler).SingleOrDefaultAsync(k => k.Id == kartId, cancellationToken);
        if (kart is null)
        {
            return StokHatalari.KartBulunamadi.Problem();
        }

        var depoId = istek.DepoId ?? (await depoServisi.VarsayilanAsync(cancellationToken)).Id;
        if (!await db.Depolar.AnyAsync(d => d.Id == depoId && d.AktifMi, cancellationToken))
        {
            return StokHatalari.DepoBulunamadi.Problem();
        }

        var sonuc = StokHareket.Olustur(kart, depoId, istek.Tur, istek.Tarih, istek.Birim, istek.Miktar, istek.BirimFiyat,
            Temiz(istek.BelgeNo), Temiz(istek.Aciklama));
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        var hareket = sonuc.Deger;
        string? uyari = null;
        if (hareket.CikisMiktari > 0)
        {
            var kontrol = await cikisKontrolu.KontrolEtAsync(kart, depoId, hareket.CikisMiktari, cancellationToken);
            if (!kontrol.BasariliMi)
            {
                return kontrol.HttpSonucu();
            }

            uyari = kontrol.Deger;
        }

        db.StokHareketleri.Add(hareket);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/stok/kartlar/{kartId}/hareketler/{hareket.Id}",
            new StokHareketCevabi(await SatirlarAsync(db, [hareket.Id], cancellationToken), uyari));
    }

    private static async Task<IResult> TransferAsync(
        Guid kartId, TransferIstegi istek, StokDbContext db, StokCikisKontrolu cikisKontrolu, CancellationToken cancellationToken)
    {
        var kart = await db.StokKartlari.Include(k => k.Birimler).SingleOrDefaultAsync(k => k.Id == kartId, cancellationToken);
        if (kart is null)
        {
            return StokHatalari.KartBulunamadi.Problem();
        }

        if (await db.Depolar.CountAsync(d => (d.Id == istek.KaynakDepoId || d.Id == istek.HedefDepoId) && d.AktifMi, cancellationToken) != 2)
        {
            return StokHatalari.DepoBulunamadi.Problem();
        }

        var transferId = Guid.CreateVersion7();
        var aciklama = Temiz(istek.Aciklama);
        var cikis = StokHareket.Olustur(kart, istek.KaynakDepoId, StokHareketTuru.TransferCikis, istek.Tarih, istek.Birim, istek.Miktar,
            null, null, aciklama, transferId);
        if (!cikis.BasariliMi)
        {
            return cikis.HttpSonucu();
        }

        var giris = StokHareket.Olustur(kart, istek.HedefDepoId, StokHareketTuru.TransferGiris, istek.Tarih, istek.Birim, istek.Miktar,
            null, null, aciklama, transferId);
        if (!giris.BasariliMi)
        {
            return giris.HttpSonucu();
        }

        var kontrol = await cikisKontrolu.KontrolEtAsync(kart, istek.KaynakDepoId, cikis.Deger.CikisMiktari, cancellationToken);
        if (!kontrol.BasariliMi)
        {
            return kontrol.HttpSonucu();
        }

        db.StokHareketleri.AddRange(cikis.Deger, giris.Deger);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/stok/kartlar/{kartId}/hareketler",
            new StokHareketCevabi(await SatirlarAsync(db, [cikis.Deger.Id, giris.Deger.Id], cancellationToken), kontrol.Deger));
    }

    // Transferin iki bacağı birlikte iptal edilir; orijinal ve ters kayıtlar tek SaveChanges'ta yazılır.
    private static async Task<IResult> IptalEtAsync(Guid hareketId, StokDbContext db, CancellationToken cancellationToken)
    {
        var hareket = await db.StokHareketleri.SingleOrDefaultAsync(h => h.Id == hareketId, cancellationToken);
        if (hareket is null)
        {
            return StokHatalari.HareketBulunamadi.Problem();
        }

        if (!StokHareket.ElleGirilebilirMi(hareket.Tur) && !StokHareket.TransferMi(hareket.Tur))
        {
            return StokHatalari.ElleIptalEdilemez.Problem();
        }

        var iptalEdilecekler = hareket.TransferId is { } transferId
            ? await db.StokHareketleri.Where(h => h.TransferId == transferId && h.IptalEdilenHareketId == null).OrderByDescending(h => h.CikisMiktari).ToListAsync(cancellationToken)
            : [hareket];

        var tersler = new List<StokHareket>();
        foreach (var h in iptalEdilecekler)
        {
            var sonuc = h.IptalEt();
            if (!sonuc.BasariliMi)
            {
                return sonuc.HttpSonucu();
            }

            tersler.Add(sonuc.Deger);
        }

        db.StokHareketleri.AddRange(tersler);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(new StokHareketCevabi(await SatirlarAsync(db, tersler.Select(t => t.Id).ToList(), cancellationToken), null));
    }

    // Satırlar verilen Id sırasıyla döner. Aynı milisaniyede üretilen Guid v7'ler sıralı olmadığından Id'ye göre sıralanmaz.
    private static async Task<List<StokHareketSatiri>> SatirlarAsync(StokDbContext db, List<Guid> idler, CancellationToken cancellationToken)
    {
        var satirlar = await (
                from h in db.StokHareketleri.AsNoTracking()
                join d in db.Depolar on h.DepoId equals d.Id
                where idler.Contains(h.Id)
                select new StokHareketSatiri(
                    h.Id, h.Tarih, h.Tur, d.Ad, h.GirisMiktari, h.CikisMiktari, h.GirilenBirim, h.GirilenMiktar, h.BirimFiyat,
                    h.BelgeNo, h.Aciklama, h.IptalHareketId != null, h.IptalEdilenHareketId != null, false))
            .ToListAsync(cancellationToken);

        return satirlar.OrderBy(s => idler.IndexOf(s.Id)).ToList();
    }

    private static string? Temiz(string? deger) => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();
}

// Çıkıştan önce firmanın eksi stok ayarını uygular. Başarılıysa varsa uyarı metnini döner. Fatura modülü de kullanacak.
internal sealed class StokCikisKontrolu(StokHesaplayici hesaplayici, StokAyarServisi ayarServisi)
{
    public async Task<Sonuc<string?>> KontrolEtAsync(StokKarti kart, Guid depoId, decimal cikisMiktari, CancellationToken cancellationToken)
    {
        var ayar = await ayarServisi.GetirAsync(cancellationToken);
        if (ayar.EksiStokDavranisi == EksiStokDavranisi.IzinVer)
        {
            return (string?)null;
        }

        var mevcut = await hesaplayici.DepoMiktariAsync(kart.Id, depoId, cancellationToken);
        var sonMiktar = mevcut - cikisMiktari;
        if (sonMiktar >= 0)
        {
            return (string?)null;
        }

        if (ayar.EksiStokDavranisi == EksiStokDavranisi.Engelle)
        {
            return StokHatalari.YetersizStok(mevcut, kart.TemelBirim);
        }

        return StokHatalari.EksiStokUyarisi(sonMiktar, kart.TemelBirim);
    }
}
