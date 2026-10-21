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
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Yonetim.Features;

// Şifre değiştirme (oturum açıkken) ve "şifremi unuttum" (e-postayla tek kullanımlık bağlantı).
// Şifre değişince kullanıcının tüm açık oturumları (refresh token) kapanır.
internal static class SifreIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        grup.MapPost("/sifre-degistir", DegistirAsync)
            .RequireAuthorization()
            .RequireRateLimiting(YonetimModulu.KimlikLimitPolitikasi)
            .Dogrula<SifreDegistirIstegi>();
        grup.MapPost("/sifre-sifirlama-istegi", SifirlamaIstegiAsync)
            .AllowAnonymous()
            .RequireRateLimiting(YonetimModulu.KimlikLimitPolitikasi)
            .Dogrula<SifreSifirlamaIstegi>();
        grup.MapPost("/sifre-sifirla", SifirlaAsync)
            .AllowAnonymous()
            .RequireRateLimiting(YonetimModulu.KimlikLimitPolitikasi)
            .Dogrula<SifreSifirlaIstegi>();
    }

    internal sealed class DegistirDogrulayici : AbstractValidator<SifreDegistirIstegi>
    {
        public DegistirDogrulayici()
        {
            RuleFor(x => x.MevcutSifre).NotEmpty();
            RuleFor(x => x.YeniSifre).Sifre()
                .NotEqual(x => x.MevcutSifre).WithMessage("Yeni şifre mevcut şifreden farklı olmalıdır.");
        }
    }

    internal sealed class SifirlamaIstegiDogrulayici : AbstractValidator<SifreSifirlamaIstegi>
    {
        public SifirlamaIstegiDogrulayici() => RuleFor(x => x.Eposta).Eposta();
    }

    internal sealed class SifirlaDogrulayici : AbstractValidator<SifreSifirlaIstegi>
    {
        public SifirlaDogrulayici()
        {
            RuleFor(x => x.Kod).NotEmpty().MaximumLength(100);
            RuleFor(x => x.YeniSifre).Sifre();
        }
    }

    private static async Task<IResult> DegistirAsync(
        SifreDegistirIstegi istek,
        IMevcutKullanici mevcutKullanici,
        YonetimDbContext db,
        IPasswordHasher<Kullanici> hasher,
        OturumServisi oturumServisi,
        GirisDenemeleri denemeler,
        HttpContext http,
        TimeProvider zaman,
        CancellationToken cancellationToken)
    {
        var simdi = zaman.GetUtcNow().UtcDateTime;
        var kullanici = await db.Kullanicilar.SingleOrDefaultAsync(k => k.Id == mevcutKullanici.KullaniciId && k.AktifMi, cancellationToken);
        if (kullanici is null)
        {
            return YonetimHatalari.OturumGecersiz.Problem();
        }

        var ip = http.Connection.RemoteIpAddress?.ToString();
        if (denemeler.KilitliMi(kullanici.Eposta, ip))
        {
            return YonetimHatalari.HesapKilitli.Problem();
        }

        // Yanlış mevcut şifre girişteki gibi sayılır: ele geçirilmiş bir oturumla şifre denenemesin.
        if (hasher.VerifyHashedPassword(kullanici, kullanici.SifreOzeti, istek.MevcutSifre) == PasswordVerificationResult.Failed)
        {
            denemeler.Basarisiz(kullanici.Eposta, ip);
            return YonetimHatalari.MevcutSifreHatali.Problem();
        }

        denemeler.Basarili(kullanici.Eposta, ip);

        kullanici.SifreBelirle(hasher.HashPassword(kullanici, istek.YeniSifre));
        await OturumServisi.TumunuKapatAsync(db, kullanici.Id, simdi, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        // Bu cihazda oturum yeni token'la sürer; diğer cihazlar yeniden giriş yapmalıdır.
        var sonuc = await oturumServisi.AcAsync(kullanici, mevcutKullanici.FirmaId, eskiToken: null, cancellationToken);
        return sonuc.HttpSonucu();
    }

    // Hesap olsun olmasın aynı yanıt döner: e-postanın kayıtlı olup olmadığı buradan öğrenilemez.
    private static async Task<IResult> SifirlamaIstegiAsync(
        SifreSifirlamaIstegi istek,
        YonetimDbContext db,
        KimlikPostasi posta,
        TimeProvider zaman,
        CancellationToken cancellationToken)
    {
        var simdi = zaman.GetUtcNow().UtcDateTime;
        var eposta = Kullanici.EpostaNormalize(istek.Eposta);
        var kullanici = await db.Kullanicilar.AsNoTracking().SingleOrDefaultAsync(k => k.Eposta == eposta && k.AktifMi, cancellationToken);
        if (kullanici is null || !await KullaniciKodlari.GonderilebilirMiAsync(db, kullanici.Id, KodAmaci.SifreSifirlama, simdi, cancellationToken))
        {
            return TypedResults.Accepted((string?)null);
        }

        var (kod, ozet) = TekKullanimlikKod.Uret();
        db.KullaniciKodlari.Add(KullaniciKodu.Olustur(kullanici.Id, KodAmaci.SifreSifirlama, ozet, simdi));
        await db.SaveChangesAsync(cancellationToken);
        await posta.SifreSifirlamaAsync(kullanici.Eposta, kullanici.AdSoyad, kod, cancellationToken);
        return TypedResults.Accepted((string?)null);
    }

    private static async Task<IResult> SifirlaAsync(
        SifreSifirlaIstegi istek,
        YonetimDbContext db,
        IPasswordHasher<Kullanici> hasher,
        GirisDenemeleri denemeler,
        TimeProvider zaman,
        CancellationToken cancellationToken)
    {
        var simdi = zaman.GetUtcNow().UtcDateTime;
        var ozet = TokenServisi.Ozetle(istek.Kod.Trim());
        var kod = await db.KullaniciKodlari.SingleOrDefaultAsync(s => s.KodOzeti == ozet, cancellationToken);
        var kullanici = kod is null
            ? null
            : await db.Kullanicilar.SingleOrDefaultAsync(k => k.Id == kod.KullaniciId && k.AktifMi, cancellationToken);
        if (kod is null || kullanici is null || !kod.GecerliMi(KodAmaci.SifreSifirlama, simdi))
        {
            return YonetimHatalari.SifirlamaKoduGecersiz.Problem();
        }

        kullanici.SifreBelirle(hasher.HashPassword(kullanici, istek.YeniSifre));
        kullanici.EpostaDogrula();
        denemeler.Sifirla(kullanici.Eposta);

        // Bu kod ve kullanıcının bekleyen diğer sıfırlama kodları da artık kullanılamaz.
        var bekleyenler = await db.KullaniciKodlari
            .Where(s => s.KullaniciId == kullanici.Id && s.Amac == KodAmaci.SifreSifirlama && s.KullanimTarihi == null)
            .ToListAsync(cancellationToken);
        bekleyenler.ForEach(s => s.Kullan(simdi));

        await OturumServisi.TumunuKapatAsync(db, kullanici.Id, simdi, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
