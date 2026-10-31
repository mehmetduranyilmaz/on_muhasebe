using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.KasaBanka.Domain.CekSenet;
using OnMuhasebe.Modules.KasaBanka.Infrastructure;
using OnMuhasebe.Modules.KasaBanka.Infrastructure.CekSenet;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.KasaBanka.Features;

// Kamerayla ya da fotoğraftan okunan çek karekodunun çözümü: banka adı, kayıtlı keşideci ve aynı çekin portföyde olup olmadığı.
// Okuma tarayıcıda yapılır; sunucuya yalnızca okunan metin gelir.
internal static class KarekodIslemleri
{
    public const int AzamiUzunluk = 500;

    public static void Map(RouteGroupBuilder grup) =>
        grup.MapPost("/karekod-coz", CozAsync).Dogrula<KarekodCozIstegi>().Islem(YetkiIslemleri.Goruntule);

    internal sealed class Dogrulayici : AbstractValidator<KarekodCozIstegi>
    {
        public Dogrulayici() => RuleFor(x => x.Metin).NotEmpty().MaximumLength(AzamiUzunluk);
    }

    private static async Task<IResult> CozAsync(KarekodCozIstegi istek, KasaBankaDbContext db, CekSenetServisi servis, CancellationToken ct)
    {
        var ham = istek.Metin.Trim();
        var c = CekKarekodu.Coz(ham);
        var borclu = c.KimlikNo is { } kimlik
            ? await db.EvrakBorclulari.AsNoTracking().Where(b => b.KimlikNo == kimlik).Select(b => new { b.Id, b.Ad }).FirstOrDefaultAsync(ct)
            : null;

        return TypedResults.Ok(new KarekodBilgisi(
            ham, c.SeriNo, c.BankaKodu, BankaKodlari.Ad(c.BankaKodu), c.SubeKodu, c.HesapNo, c.Iban, c.KimlikNo,
            borclu?.Id, borclu?.Ad, await servis.KarekodKayitliAsync([ham], ct)));
    }
}
