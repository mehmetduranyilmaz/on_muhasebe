using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Yonetim.Domain;
using OnMuhasebe.Modules.Yonetim.Infrastructure;
using OnMuhasebe.Shared.Contracts.Ayarlar;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Yonetim.Features;

// Ayarlar > Dönem kilidi. Kapanan ayın (beyanname verilmiş dönemin) kayıtlarının sonradan değiştirilmesini önler.
internal static class DonemKilidiIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        grup.MapGet("/donem-kilidi", GetirAsync).RequireAuthorization();
        grup.MapPut("/donem-kilidi", KaydetAsync).Dogrula<DonemKilidiAyari>().RequireYetki(YonetimYetkileri.Firma, YetkiIslemleri.Duzenle);
    }

    internal sealed class Dogrulayici : AbstractValidator<DonemKilidiAyari>
    {
        public Dogrulayici(TimeProvider zaman)
        {
            RuleFor(x => x.KilitTarihi)
                .Must(t => t <= TurkiyeSaati.Bugun(zaman))
                .When(x => x.KilitTarihi is not null)
                .WithMessage("Kilit tarihi bugünden ileri olamaz.");
            RuleFor(x => x.Aciklama).MaximumLength(DonemKilidi.AciklamaUzunlugu);
        }
    }

    private static async Task<IResult> GetirAsync(YonetimDbContext db, CancellationToken ct)
    {
        var kilit = await db.DonemKilitleri.AsNoTracking().FirstOrDefaultAsync(ct);
        return TypedResults.Ok(new DonemKilidiAyari(kilit?.KilitTarihi, kilit?.Aciklama));
    }

    private static async Task<IResult> KaydetAsync(
        DonemKilidiAyari istek, YonetimDbContext db, DonemKilidiSorgusu sorgu, IMevcutKullanici mevcutKullanici, CancellationToken ct)
    {
        var kilit = await db.DonemKilitleri.FirstOrDefaultAsync(ct);
        if (kilit is null)
        {
            kilit = DonemKilidi.Olustur();
            db.DonemKilitleri.Add(kilit);
        }

        kilit.Guncelle(istek.KilitTarihi, istek.Aciklama);
        await db.SaveChangesAsync(ct);
        await sorgu.OnbellektenKaldirAsync(mevcutKullanici.FirmaId!.Value, ct);
        return TypedResults.Ok(new DonemKilidiAyari(kilit.KilitTarihi, kilit.Aciklama));
    }
}
