using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.KasaBanka.Domain;
using OnMuhasebe.Modules.KasaBanka.Infrastructure;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.KasaBanka.Features;

// Açılış, tahsilat, ödeme, virman ve gelir/gider. Her işlem bir IslemId döner; iptal de IslemId ile yapılır.
internal static class IslemIslemleri
{
    private const decimal AzamiTutar = 1_000_000_000_000m;

    public static void Map(RouteGroupBuilder grup)
    {
        var islemler = grup.MapGroup("/islemler");
        islemler.MapGet("/{islemId:guid}", GetirAsync);
        islemler.MapPost("/acilis", (AcilisIstegi i, KasaIslemServisi s, KasaBankaDbContext db, ICariSorgusu c, CancellationToken ct) =>
            SonucAsync(s.AcilisAsync(i, ct), db, c, ct)).Dogrula<AcilisIstegi>();
        islemler.MapPost("/tahsilat", (TahsilatOdemeIstegi i, KasaIslemServisi s, KasaBankaDbContext db, ICariSorgusu c, CancellationToken ct) =>
            SonucAsync(s.TahsilatOdemeAsync(i, tahsilatMi: true, ct), db, c, ct)).Dogrula<TahsilatOdemeIstegi>();
        islemler.MapPost("/odeme", (TahsilatOdemeIstegi i, KasaIslemServisi s, KasaBankaDbContext db, ICariSorgusu c, CancellationToken ct) =>
            SonucAsync(s.TahsilatOdemeAsync(i, tahsilatMi: false, ct), db, c, ct)).Dogrula<TahsilatOdemeIstegi>();
        islemler.MapPost("/virman", (VirmanIstegi i, KasaIslemServisi s, KasaBankaDbContext db, ICariSorgusu c, CancellationToken ct) =>
            SonucAsync(s.VirmanAsync(i, ct), db, c, ct)).Dogrula<VirmanIstegi>();
        islemler.MapPost("/gelir-gider", (GelirGiderIstegi i, KasaIslemServisi s, KasaBankaDbContext db, ICariSorgusu c, CancellationToken ct) =>
            SonucAsync(s.GelirGiderAsync(i, ct), db, c, ct)).Dogrula<GelirGiderIstegi>();
        islemler.MapPost("/{islemId:guid}/iptal", (Guid islemId, KasaIslemServisi s, KasaBankaDbContext db, ICariSorgusu c, CancellationToken ct) =>
            SonucAsync(s.IptalAsync(islemId, belgedenMi: false, ct), db, c, ct, olusturuldu: false));
    }

    internal sealed class AcilisDogrulayici : AbstractValidator<AcilisIstegi>
    {
        public AcilisDogrulayici()
        {
            RuleFor(x => x.Tutar).NotEqual(0).ExclusiveBetween(-AzamiTutar, AzamiTutar);
            RuleFor(x => x.Kur).GreaterThan(0).When(x => x.Kur is not null);
            RuleFor(x => x.Aciklama).MaximumLength(500);
        }
    }

    internal sealed class TahsilatOdemeDogrulayici : AbstractValidator<TahsilatOdemeIstegi>
    {
        public TahsilatOdemeDogrulayici()
        {
            RuleFor(x => x.Tutar).GreaterThan(0).LessThan(AzamiTutar);
            RuleFor(x => x.Kur).GreaterThan(0).When(x => x.Kur is not null);
            RuleFor(x => x.CariKuru).GreaterThan(0).When(x => x.CariKuru is not null);
            RuleFor(x => x.BelgeNo).MaximumLength(50);
            RuleFor(x => x.Aciklama).MaximumLength(400);
        }
    }

    internal sealed class VirmanDogrulayici : AbstractValidator<VirmanIstegi>
    {
        public VirmanDogrulayici()
        {
            RuleFor(x => x.Tutar).GreaterThan(0).LessThan(AzamiTutar);
            RuleFor(x => x.HedefTutar).GreaterThan(0).LessThan(AzamiTutar).When(x => x.HedefTutar is not null);
            RuleFor(x => x.HedefHesapId).NotEqual(x => x.KaynakHesapId).WithMessage("Kaynak ve hedef hesap farklı olmalıdır.");
            RuleFor(x => x.Kur).GreaterThan(0).When(x => x.Kur is not null);
            RuleFor(x => x.HedefKur).GreaterThan(0).When(x => x.HedefKur is not null);
            RuleFor(x => x.Aciklama).MaximumLength(500);
        }
    }

    internal sealed class GelirGiderDogrulayici : AbstractValidator<GelirGiderIstegi>
    {
        public GelirGiderDogrulayici()
        {
            RuleFor(x => x.Tur).IsInEnum();
            RuleFor(x => x.Bicim).IsInEnum();
            RuleFor(x => x.Tutar).GreaterThan(0).LessThan(AzamiTutar);
            RuleFor(x => x.Kur).GreaterThan(0).When(x => x.Kur is not null);
            RuleFor(x => x.CariKuru).GreaterThan(0).When(x => x.CariKuru is not null);
            RuleFor(x => x.VadeTarihi).GreaterThanOrEqualTo(x => x.Tarih).When(x => x.VadeTarihi is not null)
                .WithMessage("Vade tarihi işlem tarihinden önce olamaz.");
            RuleFor(x => x.BelgeNo).MaximumLength(50);
            RuleFor(x => x.Aciklama).MaximumLength(450);
        }
    }

    private static async Task<IResult> GetirAsync(Guid islemId, KasaBankaDbContext db, ICariSorgusu cariSorgusu, CancellationToken cancellationToken)
    {
        var cevap = await CevapAsync(islemId, db, cariSorgusu, cancellationToken);
        return cevap.Hareketler.Count == 0 && !await db.GelirGiderKayitlari.AnyAsync(g => g.IslemId == islemId, cancellationToken)
            ? KasaBankaHatalari.IslemBulunamadi.Problem()
            : TypedResults.Ok(cevap);
    }

    private static async Task<IResult> SonucAsync(
        Task<Sonuc<Guid>> islem, KasaBankaDbContext db, ICariSorgusu cariSorgusu, CancellationToken cancellationToken, bool olusturuldu = true)
    {
        var sonuc = await islem;
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        var cevap = await CevapAsync(sonuc.Deger, db, cariSorgusu, cancellationToken);
        return olusturuldu ? TypedResults.Created($"/api/kasabanka/islemler/{sonuc.Deger}", cevap) : TypedResults.Ok(cevap);
    }

    // Bacaklar oluşturulma sırasıyla döner (virmanda önce çıkış, sonra giriş).
    private static async Task<IslemCevabi> CevapAsync(Guid islemId, KasaBankaDbContext db, ICariSorgusu cariSorgusu, CancellationToken cancellationToken)
    {
        var satirlar = await HesapIslemleri.Satirlar(db, db.KasaHareketleri.Where(h => h.IslemId == islemId).OrderBy(h => h.OlusturmaTarihi).ThenBy(h => h.Cikis == 0))
            .ToListAsync(cancellationToken);
        return new IslemCevabi(islemId, await HesapIslemleri.UnvanlariEkleAsync(satirlar, cariSorgusu, cancellationToken));
    }
}
