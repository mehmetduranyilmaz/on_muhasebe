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
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Yonetim.Features;

internal static class Kayit
{
    public const int DenemeSuresiGun = 14;

    public static void Map(RouteGroupBuilder grup) =>
        grup.MapPost("/kayit", IsleAsync)
            .AllowAnonymous()
            .RequireRateLimiting(YonetimModulu.KimlikLimitPolitikasi)
            .Dogrula<KayitIstegi>();

    internal sealed class Dogrulayici : AbstractValidator<KayitIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.FirmaUnvani).NotEmpty().MaximumLength(250);
            RuleFor(x => x.VergiNo).VergiNo();
            RuleFor(x => x.Ad).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Soyad).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Eposta).Eposta();
            RuleFor(x => x.Sifre).Sifre();
        }
    }

    private static async Task<IResult> IsleAsync(
        KayitIstegi istek,
        YonetimDbContext db,
        IPasswordHasher<Kullanici> hasher,
        OturumServisi oturumServisi,
        TimeProvider zaman,
        CancellationToken cancellationToken)
    {
        var eposta = Kullanici.EpostaNormalize(istek.Eposta);
        if (await db.Kullanicilar.AnyAsync(k => k.Eposta == eposta, cancellationToken))
        {
            return YonetimHatalari.EpostaKayitli.Problem();
        }

        var simdi = zaman.GetUtcNow().UtcDateTime;
        var denemePaketi = await db.Paketler.SingleAsync(p => p.Kod == PaketKodlari.Deneme, cancellationToken);

        var firma = Firma.Olustur(istek.FirmaUnvani, istek.VergiNo, vergiDairesi: null);
        var kullanici = Kullanici.Olustur(istek.Ad, istek.Soyad, istek.Eposta);
        kullanici.SifreBelirle(hasher.HashPassword(kullanici, istek.Sifre));

        db.AddRange(
            firma,
            kullanici,
            FirmaKullanicisi.Olustur(kullanici.Id, Roller.Yonetici, firma.Id),
            FirmaLisansi.Olustur(firma.Id, denemePaketi, simdi, simdi.AddDays(DenemeSuresiGun)));
        await db.SaveChangesAsync(cancellationToken);

        var sonuc = await oturumServisi.AcAsync(kullanici, firma.Id, eskiToken: null, cancellationToken);
        return sonuc.HttpSonucu(token => TypedResults.Created("/api/yonetim/ben", token));
    }
}
