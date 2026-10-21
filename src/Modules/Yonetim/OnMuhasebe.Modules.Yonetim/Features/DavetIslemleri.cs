using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Yonetim.Contracts;
using OnMuhasebe.Modules.Yonetim.Domain;
using OnMuhasebe.Modules.Yonetim.Infrastructure;
using OnMuhasebe.Modules.Yonetim.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Yonetim.Features;

// Davet bağlantısının kabulü (oturumsuz). Bağlantıdaki kod e-postanın sahibi olunduğunu kanıtlar:
// doğrulanmış hesabı olan kişi şifresiyle katılır, olmayan ya da doğrulanmamış hesabı olan kişi hesabını kurar.
internal static class DavetIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        grup.MapPost("/davet-bilgi", BilgiAsync)
            .AllowAnonymous()
            .RequireRateLimiting(YonetimModulu.KimlikLimitPolitikasi)
            .Dogrula<DavetBilgiIstegi>();
        grup.MapPost("/davet-kabul", KabulAsync)
            .AllowAnonymous()
            .RequireRateLimiting(YonetimModulu.KimlikLimitPolitikasi)
            .Dogrula<DavetKabulIstegi>();
    }

    internal sealed class BilgiDogrulayici : AbstractValidator<DavetBilgiIstegi>
    {
        public BilgiDogrulayici() => RuleFor(x => x.Kod).NotEmpty().MaximumLength(100);
    }

    internal sealed class KabulDogrulayici : AbstractValidator<DavetKabulIstegi>
    {
        public KabulDogrulayici()
        {
            RuleFor(x => x.Kod).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Sifre).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Ad).MaximumLength(100);
            RuleFor(x => x.Soyad).MaximumLength(100);
        }
    }

    // Yeni hesap kurulurken ad, soyad ve şifre kuralları ayrıca uygulanır. Ayrı tip: istek tipine bağlansaydı otomatik
    // kayıtla uç filtresinde de çalışır, şifresiyle katılan mevcut kullanıcıdan da ad/soyad isterdi.
    private sealed record YeniHesap(string? Ad, string? Soyad, string Sifre);

    private sealed class YeniHesapDogrulayici : AbstractValidator<YeniHesap>
    {
        public YeniHesapDogrulayici()
        {
            RuleFor(x => x.Ad).NotEmpty();
            RuleFor(x => x.Soyad).NotEmpty();
            RuleFor(x => x.Sifre).Sifre();
        }
    }

    private static readonly YeniHesapDogrulayici _yeniHesap = new();

    private static async Task<IResult> BilgiAsync(DavetBilgiIstegi istek, YonetimDbContext db, TimeProvider zaman, CancellationToken cancellationToken)
    {
        var davet = await BekleyenDavetAsync(db, istek.Kod, zaman.GetUtcNow().UtcDateTime, cancellationToken);
        var kullanici = davet is null
            ? null
            : await db.Kullanicilar.AsNoTracking().SingleOrDefaultAsync(k => k.Eposta == davet.Eposta, cancellationToken);
        if (davet is null || kullanici is { SistemYoneticisiMi: true } or { AktifMi: false })
        {
            return YonetimHatalari.DavetGecersiz.Problem();
        }

        return TypedResults.Ok(new DavetBilgisi(davet.Firma.Unvan, davet.Eposta, davet.Rol, kullanici is { EpostaDogrulandiMi: true }));
    }

    private static async Task<IResult> KabulAsync(
        DavetKabulIstegi istek,
        YonetimDbContext db,
        IPasswordHasher<Kullanici> hasher,
        OturumServisi oturumServisi,
        GirisDenemeleri denemeler,
        HttpContext http,
        TimeProvider zaman,
        CancellationToken cancellationToken)
    {
        var simdi = zaman.GetUtcNow().UtcDateTime;
        var davet = await BekleyenDavetAsync(db, istek.Kod, simdi, cancellationToken);
        var kullanici = davet is null
            ? null
            : await db.Kullanicilar.SingleOrDefaultAsync(k => k.Eposta == davet.Eposta, cancellationToken);
        if (davet is null || kullanici is { SistemYoneticisiMi: true } or { AktifMi: false })
        {
            return YonetimHatalari.DavetGecersiz.Problem();
        }

        if (kullanici is { EpostaDogrulandiMi: true })
        {
            var ip = http.Connection.RemoteIpAddress?.ToString();
            if (denemeler.KilitliMi(kullanici.Eposta, ip))
            {
                return YonetimHatalari.HesapKilitli.Problem();
            }

            var dogrulama = hasher.VerifyHashedPassword(kullanici, kullanici.SifreOzeti, istek.Sifre);
            if (dogrulama == PasswordVerificationResult.Failed)
            {
                denemeler.Basarisiz(kullanici.Eposta, ip);
                return YonetimHatalari.GirisBasarisiz.Problem();
            }

            denemeler.Basarili(kullanici.Eposta, ip);

            if (dogrulama == PasswordVerificationResult.SuccessRehashNeeded)
            {
                kullanici.SifreBelirle(hasher.HashPassword(kullanici, istek.Sifre));
            }
        }
        else
        {
            var dogrulamaSonucu = await _yeniHesap.ValidateAsync(new YeniHesap(istek.Ad, istek.Soyad, istek.Sifre), cancellationToken);
            if (!dogrulamaSonucu.IsValid)
            {
                return TypedResults.ValidationProblem(dogrulamaSonucu.ToDictionary(), title: "Doğrulama hatası.");
            }

            if (kullanici is null)
            {
                kullanici = Kullanici.Olustur(istek.Ad!, istek.Soyad!, davet.Eposta);
                db.Kullanicilar.Add(kullanici);
            }
            else
            {
                // Bu adresle doğrulanmadan açılmış hesap (başkası açmış olabilir) e-postanın sahibine geçer.
                kullanici.BekleyenHesabiGuncelle(istek.Ad!, istek.Soyad!);
                await OturumServisi.TumunuKapatAsync(db, kullanici.Id, simdi, cancellationToken);
            }

            kullanici.SifreBelirle(hasher.HashPassword(kullanici, istek.Sifre));
            kullanici.EpostaDogrula();
        }

        var uyelik = await db.FirmaKullanicilari.IgnoreQueryFilters([ModulDbContext.FirmaFiltresi])
            .SingleOrDefaultAsync(u => u.FirmaId == davet.FirmaId && u.KullaniciId == kullanici.Id, cancellationToken);
        if (uyelik is not { AktifMi: true })
        {
            var limit = await db.FirmaLisanslari.IgnoreQueryFilters([ModulDbContext.FirmaFiltresi])
                .Where(l => l.FirmaId == davet.FirmaId).Select(l => l.Paket.KullaniciLimiti).SingleOrDefaultAsync(cancellationToken);
            if (limit is not null && await db.FirmaKullanicilari.IgnoreQueryFilters([ModulDbContext.FirmaFiltresi])
                    .CountAsync(u => u.FirmaId == davet.FirmaId && u.AktifMi, cancellationToken) >= limit)
            {
                return YonetimHatalari.KullaniciLimiti(limit.Value).Problem();
            }

            if (uyelik is null)
            {
                db.FirmaKullanicilari.Add(FirmaKullanicisi.Olustur(kullanici.Id, davet.Rol, davet.FirmaId));
            }
            else
            {
                uyelik.Aktiflestir(davet.Rol);
            }
        }

        davet.KabulEt(simdi);
        // Oturum, üyeliği veritabanından okur; önce kaydedilir.
        await db.SaveChangesAsync(cancellationToken);
        var sonuc = await oturumServisi.AcAsync(kullanici, davet.FirmaId, eskiToken: null, cancellationToken);
        return sonuc.HttpSonucu();
    }

    private static async Task<FirmaDaveti?> BekleyenDavetAsync(YonetimDbContext db, string kod, DateTime simdi, CancellationToken cancellationToken)
    {
        var ozet = TokenServisi.Ozetle(kod.Trim());
        var davet = await db.FirmaDavetleri.IgnoreQueryFilters([ModulDbContext.FirmaFiltresi])
            .Include(d => d.Firma)
            .SingleOrDefaultAsync(d => d.KodOzeti == ozet, cancellationToken);
        return davet is not null && davet.BekliyorMu(simdi) && davet.Firma.AktifMi ? davet : null;
    }
}
