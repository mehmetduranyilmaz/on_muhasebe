using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Modules.Fatura.Domain;
using OnMuhasebe.Modules.Fatura.Infrastructure;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Fatura.Features;

internal static class FaturaAyarIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        grup.MapGet("/ayarlar", GetirAsync);
        grup.MapPut("/ayarlar", KaydetAsync).Dogrula<FaturaAyarlari>().RequireAuthorization(p => p.RequireRole(Roller.Yonetici));
    }

    internal sealed class Dogrulayici : AbstractValidator<FaturaAyarlari>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.KayitAkisi).IsInEnum();
            RuleFor(x => x.ToptanSeri).NotEmpty().Matches("^[A-Za-z0-9]{3}$").WithMessage("Seri 3 harf/rakamdan oluşmalıdır.");
            RuleFor(x => x.PerakendeSeri).NotEmpty().Matches("^[A-Za-z0-9]{3}$").WithMessage("Seri 3 harf/rakamdan oluşmalıdır.");
            RuleFor(x => x.IadeSeri).NotEmpty().Matches("^[A-Za-z0-9]{3}$").WithMessage("Seri 3 harf/rakamdan oluşmalıdır.");
            RuleFor(x => x.GiderPusulasiSeri).NotEmpty().Matches("^[A-Za-z0-9]{3}$").WithMessage("Seri 3 harf/rakamdan oluşmalıdır.");
            RuleFor(x => x.IrsaliyeSeri).NotEmpty().Matches("^[A-Za-z0-9]{3}$").WithMessage("Seri 3 harf/rakamdan oluşmalıdır.");
            RuleFor(x => x.VarsayilanVadeGunu).InclusiveBetween(0, 365);
        }
    }

    private static async Task<IResult> GetirAsync(FaturaAyarServisi ayarServisi, CancellationToken cancellationToken) =>
        TypedResults.Ok((await ayarServisi.GetirAsync(cancellationToken)).Sozlesme());

    private static async Task<IResult> KaydetAsync(FaturaAyarlari istek, FaturaDbContext db, CancellationToken cancellationToken)
    {
        var ayar = await db.FaturaAyarlari.SingleOrDefaultAsync(cancellationToken);
        if (ayar is null)
        {
            ayar = FaturaAyari.Varsayilan();
            db.FaturaAyarlari.Add(ayar);
        }

        ayar.Guncelle(istek);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ayar.Sozlesme());
    }
}
