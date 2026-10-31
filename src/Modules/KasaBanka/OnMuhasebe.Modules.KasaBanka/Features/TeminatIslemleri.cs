using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.KasaBanka.Domain;
using OnMuhasebe.Modules.KasaBanka.Infrastructure;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.KasaBanka.Features;

// Teminat takip defteri: alınan/verilen teminatların kaydı, türe özgü bilgileri ve durumu. Kasa/muhasebe hareketi oluşturmaz.
internal static class TeminatIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var teminatlar = grup.MapGroup("/teminatlar").Yetki(KasaBankaYetkileri.Teminat);
        teminatlar.MapGet("", ListeleAsync);
        teminatlar.MapGet("/{id:guid}", DetayAsync);
        teminatlar.MapPost("", EkleAsync).Dogrula<TeminatKaydetIstegi>();
        teminatlar.MapPut("/{id:guid}", GuncelleAsync).Dogrula<TeminatKaydetIstegi>();
        teminatlar.MapPost("/{id:guid}/durum", DurumDegistirAsync).Islem(YetkiIslemleri.Duzenle);
        teminatlar.MapDelete("/{id:guid}", SilAsync);
    }

    internal sealed class Dogrulayici : AbstractValidator<TeminatKaydetIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.Tur).IsInEnum();
            RuleFor(x => x.Yon).IsInEnum();
            RuleFor(x => x.CariHesapId).NotEmpty().WithMessage("Lehtar/muhatap cari seçilmeli.");
            RuleFor(x => x.BankaAdi).MaximumLength(100);
            RuleFor(x => x.ReferansNo).MaximumLength(100);
            RuleFor(x => x.ParaBirimi).Must(ParaBirimleri.GecerliMi).WithMessage("Geçersiz para birimi.");
            RuleFor(x => x.Tutar).GreaterThan(0);
            RuleFor(x => x.VadeTarihi).GreaterThanOrEqualTo(x => x.VerilisTarihi)
                .When(x => x.VadeTarihi is not null).WithMessage("Vade tarihi veriliş tarihinden önce olamaz.");
            RuleFor(x => x.KomisyonOrani).InclusiveBetween(0, 100).When(x => x.KomisyonOrani is not null);
            RuleFor(x => x.KomisyonTutari).GreaterThanOrEqualTo(0).When(x => x.KomisyonTutari is not null);
            RuleFor(x => x.Aciklama).MaximumLength(1000);
        }
    }

    private static async Task<IResult> ListeleAsync(
        KasaBankaDbContext db, ICariSorgusu cariSorgusu, TeminatYonu? yon, TeminatDurumu? durum, string? arama, CancellationToken cancellationToken)
    {
        var sorgu = db.Teminatlar.AsNoTracking();
        if (yon is not null)
        {
            sorgu = sorgu.Where(t => t.Yon == yon);
        }

        if (durum is not null)
        {
            sorgu = sorgu.Where(t => t.Durum == durum);
        }

        if (!string.IsNullOrWhiteSpace(arama))
        {
            var desen = AramaDeseni.Icerir(arama);
            sorgu = sorgu.Where(t => (t.BankaAdi != null && EF.Functions.ILike(t.BankaAdi, desen)) || (t.ReferansNo != null && EF.Functions.ILike(t.ReferansNo, desen)));
        }

        var kayitlar = await sorgu
            .OrderBy(t => t.Durum == TeminatDurumu.Aktif ? 0 : 1)
            .ThenBy(t => t.VadeTarihi ?? DateOnly.MaxValue)
            .ThenByDescending(t => t.VerilisTarihi)
            .ToListAsync(cancellationToken);

        var unvanlar = await UnvanlarAsync(kayitlar.Select(t => t.CariHesapId), cariSorgusu, cancellationToken);
        return TypedResults.Ok(kayitlar.Select(t => new TeminatOzeti(
            t.Id, t.Tur, t.Yon, t.CariHesapId, unvanlar.GetValueOrDefault(t.CariHesapId), t.BankaAdi, t.ReferansNo,
            t.Tutar, t.ParaBirimi, t.VerilisTarihi, t.VadeTarihi, t.Durum)).ToList());
    }

    private static async Task<IResult> DetayAsync(Guid id, KasaBankaDbContext db, ICariSorgusu cariSorgusu, CancellationToken cancellationToken)
    {
        var t = await db.Teminatlar.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (t is null)
        {
            return KasaBankaHatalari.TeminatBulunamadi.Problem();
        }

        var unvanlar = await UnvanlarAsync([t.CariHesapId], cariSorgusu, cancellationToken);
        return TypedResults.Ok(new TeminatDetayi(
            t.Id, t.Tur, t.Yon, t.CariHesapId, unvanlar.GetValueOrDefault(t.CariHesapId), t.BankaAdi, t.ReferansNo,
            t.Tutar, t.ParaBirimi, t.VerilisTarihi, t.VadeTarihi, t.IadeTarihi, t.Durum,
            t.KomisyonOrani, t.KomisyonTutari, t.KomisyonDonemi, t.Detaylar, t.Aciklama, t.Surum));
    }

    private static async Task<IResult> EkleAsync(TeminatKaydetIstegi istek, KasaBankaDbContext db, CancellationToken cancellationToken)
    {
        var teminat = Teminat.Olustur(istek);
        db.Teminatlar.Add(teminat);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/kasabanka/teminatlar/{teminat.Id}", teminat.Id);
    }

    private static async Task<IResult> GuncelleAsync(Guid id, TeminatKaydetIstegi istek, KasaBankaDbContext db, CancellationToken cancellationToken)
    {
        var teminat = await db.Teminatlar.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (teminat is null)
        {
            return KasaBankaHatalari.TeminatBulunamadi.Problem();
        }

        db.Entry(teminat).Property(t => t.Surum).OriginalValue = istek.Surum;
        teminat.Guncelle(istek);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> DurumDegistirAsync(Guid id, TeminatDurumIstegi istek, KasaBankaDbContext db, CancellationToken cancellationToken)
    {
        var teminat = await db.Teminatlar.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (teminat is null)
        {
            return KasaBankaHatalari.TeminatBulunamadi.Problem();
        }

        teminat.DurumDegistir(istek.Durum, istek.IadeTarihi);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> SilAsync(Guid id, KasaBankaDbContext db, CancellationToken cancellationToken)
    {
        var teminat = await db.Teminatlar.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (teminat is null)
        {
            return KasaBankaHatalari.TeminatBulunamadi.Problem();
        }

        db.Teminatlar.Remove(teminat);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IReadOnlyDictionary<Guid, string>> UnvanlarAsync(
        IEnumerable<Guid> cariIdler, ICariSorgusu cariSorgusu, CancellationToken cancellationToken)
    {
        var idler = cariIdler.Distinct().ToList();
        return idler.Count == 0 ? new Dictionary<Guid, string>() : await cariSorgusu.UnvanlarAsync(idler, cancellationToken);
    }
}
