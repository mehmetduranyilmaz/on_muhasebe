using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Yonetim.Contracts;
using OnMuhasebe.Modules.Yonetim.Domain;
using OnMuhasebe.Modules.Yonetim.Infrastructure;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Yonetim.Features;

// Firma yöneticisinin kendi firmasının kullanıcılarını yönetmesi. Sorgular firma filtresiyle otomatik olarak oturumdaki firmaya sınırlanır.
internal static class KullaniciIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var kullanicilar = grup.MapGroup("/kullanicilar")
            .RequireAuthorization(p => p.RequireRole(Roller.Yonetici));

        kullanicilar.MapGet("", ListeleAsync);
        kullanicilar.MapPost("", EkleAsync).Dogrula<KullaniciEkleIstegi>();
        kullanicilar.MapPost("/{kullaniciId:guid}/pasiflestir", PasiflestirAsync);
    }

    internal sealed class EkleDogrulayici : AbstractValidator<KullaniciEkleIstegi>
    {
        public EkleDogrulayici()
        {
            RuleFor(x => x.Ad).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Soyad).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Eposta).Eposta();
            RuleFor(x => x.Sifre).Sifre().When(x => x.Sifre is not null);
            RuleFor(x => x.Rol).FirmaRolu();
        }
    }

    private static async Task<IResult> ListeleAsync(YonetimDbContext db, CancellationToken cancellationToken)
    {
        var liste = await db.FirmaKullanicilari
            .AsNoTracking()
            .OrderBy(u => u.Kullanici.Ad).ThenBy(u => u.Kullanici.Soyad)
            .Select(u => new FirmaKullanicisiCevabi(
                u.KullaniciId, u.Kullanici.Ad, u.Kullanici.Soyad, u.Kullanici.Eposta, u.Rol, u.AktifMi))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(liste);
    }

    private static async Task<IResult> EkleAsync(
        KullaniciEkleIstegi istek,
        YonetimDbContext db,
        IPasswordHasher<Kullanici> hasher,
        CancellationToken cancellationToken)
    {
        var eposta = Kullanici.EpostaNormalize(istek.Eposta);
        var kullanici = await db.Kullanicilar.SingleOrDefaultAsync(k => k.Eposta == eposta, cancellationToken);

        if (kullanici is null)
        {
            if (string.IsNullOrEmpty(istek.Sifre))
            {
                return YonetimHatalari.SifreGerekli.Problem();
            }

            kullanici = Kullanici.Olustur(istek.Ad, istek.Soyad, istek.Eposta);
            kullanici.SifreBelirle(hasher.HashPassword(kullanici, istek.Sifre));
            db.Kullanicilar.Add(kullanici);
        }
        else if (await db.FirmaKullanicilari.AnyAsync(u => u.KullaniciId == kullanici.Id, cancellationToken))
        {
            return YonetimHatalari.ZatenFirmaUyesi.Problem();
        }

        if (await db.FirmaLisanslari.Select(l => l.Paket.KullaniciLimiti).SingleOrDefaultAsync(cancellationToken) is { } limit
            && await db.FirmaKullanicilari.CountAsync(u => u.AktifMi, cancellationToken) >= limit)
        {
            return YonetimHatalari.KullaniciLimiti(limit).Problem();
        }

        db.FirmaKullanicilari.Add(FirmaKullanicisi.Olustur(kullanici.Id, istek.Rol));
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created(
            $"/api/yonetim/kullanicilar/{kullanici.Id}",
            new FirmaKullanicisiCevabi(kullanici.Id, kullanici.Ad, kullanici.Soyad, kullanici.Eposta, istek.Rol, AktifMi: true));
    }

    private static async Task<IResult> PasiflestirAsync(
        Guid kullaniciId,
        IMevcutKullanici mevcutKullanici,
        YonetimDbContext db,
        TimeProvider zaman,
        CancellationToken cancellationToken)
    {
        var uyelik = await db.FirmaKullanicilari.SingleOrDefaultAsync(u => u.KullaniciId == kullaniciId, cancellationToken);
        if (uyelik is null)
        {
            return YonetimHatalari.KullaniciBulunamadi.Problem();
        }

        var sonuc = uyelik.Pasiflestir(mevcutKullanici.KullaniciId!.Value);
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        await db.SaveChangesAsync(cancellationToken);

        // Bu firmadaki açık oturumları kapat; access token en geç süresi dolunca (15 dk) geçersizleşir.
        var simdi = zaman.GetUtcNow().UtcDateTime;
        await db.RefreshTokenlar
            .Where(t => t.KullaniciId == kullaniciId && t.FirmaId == uyelik.FirmaId && t.IptalTarihi == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IptalTarihi, simdi), cancellationToken);

        return TypedResults.NoContent();
    }
}
