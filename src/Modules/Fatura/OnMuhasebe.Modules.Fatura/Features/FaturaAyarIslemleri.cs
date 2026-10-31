using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Modules.Fatura.Domain;
using OnMuhasebe.Modules.Fatura.Infrastructure;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Fatura.Features;

internal static class FaturaAyarIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        grup.MapGet("/ayarlar", GetirAsync).YetkiGerekmez();
        grup.MapPut("/ayarlar", KaydetAsync).Dogrula<FaturaAyarlari>().Yetki(YonetimYetkileri.Firma, YetkiIslemleri.Duzenle);
        grup.MapGet("/isyeri-serileri", SerileriGetirAsync).YetkiGerekmez();
        grup.MapPut("/isyeri-serileri/{isyeriId:guid}", SeriKaydetAsync).Dogrula<IsyeriSerileri>().Yetki(YonetimYetkileri.Firma, YetkiIslemleri.Duzenle);
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

    internal sealed class SeriDogrulayici : AbstractValidator<IsyeriSerileri>
    {
        public SeriDogrulayici()
        {
            const string desen = "^[A-Za-z0-9]{3}$";
            const string mesaj = "Seri 3 harf/rakamdan oluşmalıdır ya da boş bırakılmalıdır (firma serisi).";
            RuleFor(x => x.ToptanSeri).Matches(desen).WithMessage(mesaj).When(x => !string.IsNullOrWhiteSpace(x.ToptanSeri));
            RuleFor(x => x.PerakendeSeri).Matches(desen).WithMessage(mesaj).When(x => !string.IsNullOrWhiteSpace(x.PerakendeSeri));
            RuleFor(x => x.IadeSeri).Matches(desen).WithMessage(mesaj).When(x => !string.IsNullOrWhiteSpace(x.IadeSeri));
            RuleFor(x => x.GiderPusulasiSeri).Matches(desen).WithMessage(mesaj).When(x => !string.IsNullOrWhiteSpace(x.GiderPusulasiSeri));
            RuleFor(x => x.IrsaliyeSeri).Matches(desen).WithMessage(mesaj).When(x => !string.IsNullOrWhiteSpace(x.IrsaliyeSeri));
        }
    }

    private static async Task<IResult> SerileriGetirAsync(FaturaDbContext db, CancellationToken cancellationToken) =>
        TypedResults.Ok(await db.IsyeriSerileri.AsNoTracking().Select(s => s.Sozlesme()).ToListAsync(cancellationToken));

    // Adresteki işyeri esastır; tüm seriler boşaltılırsa kayıt silinir (işyeri firma serilerine döner).
    private static async Task<IResult> SeriKaydetAsync(
        Guid isyeriId, IsyeriSerileri istek, FaturaDbContext db, IIsyeriSorgusu isyeriSorgusu, IMevcutKullanici mevcutKullanici,
        CancellationToken cancellationToken)
    {
        if ((await isyeriSorgusu.FirmaninAsync(mevcutKullanici.FirmaId!.Value, cancellationToken)).All(i => i.Id != isyeriId))
        {
            return IsyeriHatalari.Bulunamadi.Problem();
        }

        var seri = await db.IsyeriSerileri.SingleOrDefaultAsync(s => s.IsyeriId == isyeriId, cancellationToken);
        if (seri is null)
        {
            seri = IsyeriSerisi.Olustur(isyeriId);
            db.IsyeriSerileri.Add(seri);
        }

        seri.Guncelle(istek);
        if (seri.BosMu)
        {
            db.IsyeriSerileri.Remove(seri);
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(seri.Sozlesme());
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
