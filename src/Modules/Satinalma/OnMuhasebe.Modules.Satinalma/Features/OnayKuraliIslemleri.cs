using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Satinalma.Contracts;
using OnMuhasebe.Modules.Satinalma.Domain;
using OnMuhasebe.Modules.Satinalma.Infrastructure;
using OnMuhasebe.Modules.Yonetim.Contracts;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Satinalma.Features;

// Firmanın onay basamakları. Liste bütün olarak kaydedilir; yoldaki talepler gönderildikleri andaki basamaklarla sürer.
internal static class OnayKuraliIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var g = grup.MapGroup("/onay-kurali").Yetki(SatinalmaYetkileri.Kural);
        g.MapGet("", GetirAsync);
        g.MapPut("", KaydetAsync).Dogrula<OnayKuraliIstegi>();
    }

    internal sealed class Dogrulayici : AbstractValidator<OnayKuraliIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.Basamaklar).NotNull().Must(b => b is null || b.Count <= 10).WithMessage("En çok 10 onay basamağı olabilir.");
            RuleForEach(x => x.Basamaklar).ChildRules(b => b.RuleFor(x => x.EnAzTutar).GreaterThanOrEqualTo(0));
        }
    }

    private static async Task<IResult> GetirAsync(SatinalmaDbContext db, IFirmaRehberi rehber, IMevcutKullanici kullanici, CancellationToken ct) =>
        TypedResults.Ok(await CevapAsync(db, rehber, kullanici, ct));

    private static async Task<IResult> KaydetAsync(OnayKuraliIstegi istek, SatinalmaDbContext db, IFirmaRehberi rehber, IMevcutKullanici kullanici, CancellationToken ct)
    {
        var firmaId = kullanici.FirmaId!.Value;
        var roller = (await rehber.RollerAsync(firmaId, ct)).ToDictionary(r => r.Id, r => r.Ad);
        var kisiler = (await rehber.KullanicilarAsync(firmaId, ct)).ToDictionary(k => k.KullaniciId, k => k.AdSoyad);
        var yeni = new List<OnayBasamagi>();
        foreach (var b in istek.Basamaklar.OrderBy(b => b.EnAzTutar))
        {
            if ((b.RolId is null) == (b.KullaniciId is null))
            {
                return TalepHatalari.BasamakGecersiz.Problem();
            }

            string? ad;
            if (b.RolId is { } rolId)
            {
                if (!roller.TryGetValue(rolId, out ad))
                {
                    return TalepHatalari.RolGecersiz.Problem();
                }
            }
            else if (!kisiler.TryGetValue(b.KullaniciId!.Value, out ad))
            {
                return TalepHatalari.KullaniciGecersiz.Problem();
            }

            yeni.Add(OnayBasamagi.Olustur(yeni.Count + 1, b.EnAzTutar, b.RolId, b.KullaniciId, ad));
        }

        // Benzersiz sıra indeksi çakışmasın: eskiler önce silinir.
        db.OnayBasamaklari.RemoveRange(await db.OnayBasamaklari.ToListAsync(ct));
        await db.SaveChangesAsync(ct);
        db.OnayBasamaklari.AddRange(yeni);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await CevapAsync(db, rehber, kullanici, ct));
    }

    private static async Task<OnayKuraliCevabi> CevapAsync(SatinalmaDbContext db, IFirmaRehberi rehber, IMevcutKullanici kullanici, CancellationToken ct)
    {
        var firmaId = kullanici.FirmaId!.Value;
        var basamaklar = await db.OnayBasamaklari.AsNoTracking().OrderBy(b => b.Sira)
            .Select(b => new OnayBasamagiCevabi(b.Sira, b.EnAzTutar, b.RolId, b.KullaniciId, b.Ad)).ToListAsync(ct);
        return new OnayKuraliCevabi(basamaklar,
            (await rehber.RollerAsync(firmaId, ct)).OrderBy(r => r.Ad).Select(r => new OnaySecenegi(r.Id, r.Ad)).ToList(),
            (await rehber.KullanicilarAsync(firmaId, ct)).OrderBy(k => k.AdSoyad).Select(k => new OnaySecenegi(k.KullaniciId, k.AdSoyad)).ToList());
    }
}
