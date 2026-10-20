using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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

// Oturumdaki firmanın kendi bilgileri. Firma tablosu firma filtresi dışında olduğundan Id oturumdan alınır.
internal static class FirmaBilgileriIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        grup.MapGet("/firma", GetirAsync).RequireAuthorization();
        grup.MapPut("/firma", KaydetAsync).Dogrula<FirmaBilgileri>().RequireAuthorization(p => p.RequireRole(Roller.Yonetici));
    }

    internal sealed class Dogrulayici : AbstractValidator<FirmaBilgileri>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.Unvan).NotEmpty().MaximumLength(250);
            RuleFor(x => x.VergiNo).VergiNo();
            RuleFor(x => x.VergiDairesi).MaximumLength(100);
            RuleFor(x => x.Il).MaximumLength(50);
            RuleFor(x => x.Ilce).MaximumLength(50);
            RuleFor(x => x.Adres).MaximumLength(500);
            RuleFor(x => x.Telefon).MaximumLength(30);
            RuleFor(x => x.Eposta).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.Eposta));
        }
    }

    private static async Task<IResult> GetirAsync(IMevcutKullanici mevcutKullanici, YonetimDbContext db, CancellationToken cancellationToken)
    {
        var firma = await db.Firmalar.AsNoTracking().SingleOrDefaultAsync(f => f.Id == mevcutKullanici.FirmaId, cancellationToken);
        return firma is null ? YonetimHatalari.FirmaBulunamadi.Problem() : TypedResults.Ok(Bilgiler(firma));
    }

    private static async Task<IResult> KaydetAsync(
        FirmaBilgileri istek, IMevcutKullanici mevcutKullanici, YonetimDbContext db, CancellationToken cancellationToken)
    {
        var firma = await db.Firmalar.SingleOrDefaultAsync(f => f.Id == mevcutKullanici.FirmaId, cancellationToken);
        if (firma is null)
        {
            return YonetimHatalari.FirmaBulunamadi.Problem();
        }

        firma.BilgileriGuncelle(istek.Unvan, istek.VergiNo, istek.VergiDairesi, istek.Il, istek.Ilce, istek.Adres, istek.Telefon, istek.Eposta);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(Bilgiler(firma));
    }

    private static FirmaBilgileri Bilgiler(Firma f) =>
        new(f.Unvan, f.VergiNo, f.VergiDairesi, f.Il, f.Ilce, f.Adres, f.Telefon, f.Eposta);
}
