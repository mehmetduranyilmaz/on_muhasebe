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
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Yonetim.Features;

internal static class Giris
{
    // Kullanıcı bulunamadığında da şifre özeti hesaplanır; yanıt süresinden e-postanın kayıtlı olup olmadığı anlaşılmasın.
    private static readonly Kullanici _sahteKullanici = Kullanici.Olustur("-", "-", "-");
    private static readonly string _sahteOzet = new PasswordHasher<Kullanici>().HashPassword(_sahteKullanici, "sahte-sifre-1");

    public static void Map(RouteGroupBuilder grup) =>
        grup.MapPost("/giris", IsleAsync)
            .AllowAnonymous()
            .RequireRateLimiting(YonetimModulu.KimlikLimitPolitikasi)
            .Dogrula<GirisIstegi>();

    internal sealed class Dogrulayici : AbstractValidator<GirisIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.Eposta).NotEmpty();
            RuleFor(x => x.Sifre).NotEmpty();
        }
    }

    private static async Task<IResult> IsleAsync(
        GirisIstegi istek,
        YonetimDbContext db,
        IPasswordHasher<Kullanici> hasher,
        OturumServisi oturumServisi,
        TimeProvider zaman,
        CancellationToken cancellationToken)
    {
        var simdi = zaman.GetUtcNow().UtcDateTime;
        var eposta = Kullanici.EpostaNormalize(istek.Eposta);
        var kullanici = await db.Kullanicilar.SingleOrDefaultAsync(k => k.Eposta == eposta, cancellationToken);

        if (kullanici is null || !kullanici.AktifMi)
        {
            hasher.VerifyHashedPassword(_sahteKullanici, _sahteOzet, istek.Sifre);
            return YonetimHatalari.GirisBasarisiz.Problem();
        }

        if (kullanici.KilitliMi(simdi))
        {
            return YonetimHatalari.HesapKilitli.Problem();
        }

        var dogrulama = hasher.VerifyHashedPassword(kullanici, kullanici.SifreOzeti, istek.Sifre);
        if (dogrulama == PasswordVerificationResult.Failed)
        {
            kullanici.HataliGirisYapildi(simdi);
            await db.SaveChangesAsync(cancellationToken);
            return YonetimHatalari.GirisBasarisiz.Problem();
        }

        if (dogrulama == PasswordVerificationResult.SuccessRehashNeeded)
        {
            kullanici.SifreBelirle(hasher.HashPassword(kullanici, istek.Sifre));
        }

        // Şifre doğruysa söylenir; yanlış şifrede hesabın doğrulanıp doğrulanmadığı belli edilmez.
        if (!kullanici.EpostaDogrulandiMi)
        {
            await db.SaveChangesAsync(cancellationToken);
            return YonetimHatalari.EpostaDogrulanmadi.Problem();
        }

        var sonuc = await oturumServisi.AcAsync(kullanici, kullanici.SonFirmaId, eskiToken: null, cancellationToken);
        return sonuc.HttpSonucu();
    }
}
