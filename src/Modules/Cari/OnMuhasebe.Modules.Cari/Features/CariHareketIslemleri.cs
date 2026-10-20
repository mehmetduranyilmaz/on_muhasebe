using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.Cari.Domain;
using OnMuhasebe.Modules.Cari.Infrastructure;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Shared.Contracts.Sayfalama;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Cari.Features;

// Elle girilen hareketler: açılış bakiyesi ve borç/alacak dekontu. Fatura ve tahsilat hareketlerini kendi modülleri yazar.
internal static class CariHareketIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        grup.MapGet("/hesaplar/{cariId:guid}/hareketler", ListeleAsync);
        grup.MapPost("/hesaplar/{cariId:guid}/hareketler", EkleAsync).Dogrula<CariHareketKaydetIstegi>();
        grup.MapPost("/hareketler/{hareketId:guid}/iptal", IptalEtAsync);
    }

    internal sealed class Dogrulayici : AbstractValidator<CariHareketKaydetIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.Tur).Must(CariHareket.ElleGirilebilirMi)
                .WithMessage("Elle yalnızca açılış bakiyesi veya dekont girilebilir.");
            RuleFor(x => x.Yon).IsInEnum();
            RuleFor(x => x.Tutar).GreaterThan(0).LessThan(1_000_000_000_000m);
            RuleFor(x => x.Kur).GreaterThan(0).When(x => x.Kur is not null);
            RuleFor(x => x.VadeTarihi).GreaterThanOrEqualTo(x => x.Tarih).When(x => x.VadeTarihi is not null)
                .WithMessage("Vade tarihi işlem tarihinden önce olamaz.");
            RuleFor(x => x.BelgeNo).MaximumLength(50);
            RuleFor(x => x.Aciklama).MaximumLength(500);
        }
    }

    private static async Task<IResult> ListeleAsync(
        Guid cariId, CariDbContext db, int? sayfa, int? boyut, CancellationToken cancellationToken)
    {
        if (!await db.CariHesaplar.AnyAsync(c => c.Id == cariId, cancellationToken))
        {
            return CariHatalari.CariBulunamadi.Problem();
        }

        var sonuc = await db.CariHareketler.AsNoTracking()
            .Where(h => h.CariHesapId == cariId)
            .OrderByDescending(h => h.Tarih).ThenByDescending(h => h.Id)
            .Select(h => new CariHareketSatiri(
                h.Id, h.Tarih, h.VadeTarihi, h.Tur, h.BelgeNo, h.Aciklama, h.Borc, h.Alacak, h.Kur, h.BorcTl, h.AlacakTl,
                h.IptalHareketId != null,
                h.IptalEdilenHareketId != null,
                (h.Tur == CariHareketTuru.Acilis || h.Tur == CariHareketTuru.Dekont)
                    && h.IptalHareketId == null && h.IptalEdilenHareketId == null,
                h.KaynakBelgeId))
            .SayfalaAsync(new SayfalamaIstegi(sayfa ?? 1, boyut ?? SayfalamaIstegi.VarsayilanBoyut), cancellationToken);

        return TypedResults.Ok(sonuc);
    }

    private static async Task<IResult> EkleAsync(
        Guid cariId, CariHareketKaydetIstegi istek, CariDbContext db, CariKapamaServisi kapama, CancellationToken cancellationToken)
    {
        var cari = await db.CariHesaplar.AsNoTracking().SingleOrDefaultAsync(c => c.Id == cariId, cancellationToken);
        if (cari is null)
        {
            return CariHatalari.CariBulunamadi.Problem();
        }

        decimal kur;
        if (cari.ParaBirimi == ParaBirimleri.Tl)
        {
            kur = 1m;
        }
        else if (istek.Kur is { } girilenKur)
        {
            kur = girilenKur;
        }
        else
        {
            return CariHatalari.KurGerekli.Problem();
        }

        if (istek.Tur == CariHareketTuru.Acilis && await db.CariHareketler.AnyAsync(
                h => h.CariHesapId == cariId && h.Tur == CariHareketTuru.Acilis
                     && h.IptalHareketId == null && h.IptalEdilenHareketId == null,
                cancellationToken))
        {
            return CariHatalari.AcilisZatenVar.Problem();
        }

        var sonuc = CariHareket.Olustur(
            cari, istek.Tur, istek.Yon, istek.Tarih, istek.VadeTarihi, istek.Tutar, kur,
            Temiz(istek.BelgeNo), Temiz(istek.Aciklama));
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        var hareket = sonuc.Deger;
        db.CariHareketler.Add(hareket);
        await db.SaveChangesAsync(cancellationToken);
        await kapama.KapatAsync(cariId, hareket.Id, null, cancellationToken);

        return TypedResults.Created($"/api/cari/hesaplar/{cariId}/hareketler/{hareket.Id}", Satir(hareket));
    }

    private static async Task<IResult> IptalEtAsync(
        Guid hareketId, CariDbContext db, CariKapamaServisi kapama, CancellationToken cancellationToken)
    {
        var hareket = await db.CariHareketler.SingleOrDefaultAsync(h => h.Id == hareketId, cancellationToken);
        if (hareket is null)
        {
            return CariHatalari.HareketBulunamadi.Problem();
        }

        if (!CariHareket.ElleGirilebilirMi(hareket.Tur))
        {
            return CariHatalari.ElleIptalEdilemez.Problem();
        }

        var sonuc = hareket.IptalEt();
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        // Orijinal ve ters kayıt aynı SaveChanges'ta: ikisi birlikte yazılır ya da hiçbiri yazılmaz.
        db.CariHareketler.Add(sonuc.Deger);
        await db.SaveChangesAsync(cancellationToken);
        await kapama.CozVeKapatAsync(hareket.CariHesapId, [hareket.Id], cancellationToken);
        return TypedResults.Ok(Satir(sonuc.Deger));
    }

    private static CariHareketSatiri Satir(CariHareket h) => new(
        h.Id, h.Tarih, h.VadeTarihi, h.Tur, h.BelgeNo, h.Aciklama, h.Borc, h.Alacak, h.Kur, h.BorcTl, h.AlacakTl,
        h.IptalEdildiMi, h.IptalKaydiMi,
        CariHareket.ElleGirilebilirMi(h.Tur) && !h.IptalEdildiMi && !h.IptalKaydiMi, h.KaynakBelgeId);

    private static string? Temiz(string? deger) => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();
}
