using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Yonetim.Domain;
using OnMuhasebe.Modules.Yonetim.Infrastructure;
using OnMuhasebe.Shared.Contracts.Isyerleri;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Yonetim.Features;

// Ayarlar > İşyerleri. Aktif işyeri sayısı paketin işyeri limitini aşamaz; merkez her zaman vardır ve kapatılamaz.
internal static class IsyeriIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        // Belge formları işyeri seçtirdiği için liste firmanın her üyesine açıktır (kısıtlı kullanıcıya yalnız girebildikleri).
        grup.MapGet("/isyerleri", ListeleAsync).RequireAuthorization(p => p.RequireClaim(ClaimTipleri.FirmaId));

        var isyerleri = grup.MapGroup("/isyerleri").RequireYetki(YonetimYetkileri.Firma, YetkiIslemleri.Duzenle);
        isyerleri.MapPost("", EkleAsync).Dogrula<IsyeriKaydetIstegi>();
        isyerleri.MapPut("/{id:guid}", GuncelleAsync).Dogrula<IsyeriKaydetIstegi>();
        isyerleri.MapPost("/{id:guid}/pasiflestir", (Guid id, YonetimDbContext db, IIsyeriSorgusu sorgu, CancellationToken ct) =>
            DurumAsync(id, db, sorgu, i => i.Pasiflestir(), ct));
        isyerleri.MapPost("/{id:guid}/aktiflestir", AktiflestirAsync);
        isyerleri.MapPost("/{id:guid}/merkez-yap", MerkezYapAsync);
    }

    internal sealed class Dogrulayici : AbstractValidator<IsyeriKaydetIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.Kod).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9-]+$").WithMessage("Kod yalnızca harf, rakam ve tire içerebilir.");
            RuleFor(x => x.Ad).NotEmpty().MaximumLength(150);
            RuleFor(x => x.Adres).MaximumLength(500);
            RuleFor(x => x.Il).MaximumLength(50);
            RuleFor(x => x.Ilce).MaximumLength(50);
            RuleFor(x => x.Telefon).MaximumLength(30);
            RuleFor(x => x.SgkSicilNo).Must(s => s is null || s.Trim().Length == 0 || (s.Trim().Length == Isyeri.SgkSicilUzunlugu && s.Trim().All(char.IsAsciiDigit)))
                .WithMessage($"SGK işyeri sicil numarası {Isyeri.SgkSicilUzunlugu} haneli olmalıdır.");
        }
    }

    private static async Task<IResult> ListeleAsync(
        IIsyeriSorgusu sorgu, IYetkiDenetimi yetki, IMevcutKullanici mevcutKullanici, CancellationToken ct)
    {
        var tumu = await sorgu.FirmaninAsync(mevcutKullanici.FirmaId!.Value, ct);
        var yetkiler = await yetki.GetirAsync(ct);
        if (yetkiler.TamYetkiliMi || yetkiler.VarMi(YonetimYetkileri.Firma.Yetki(YetkiIslemleri.Duzenle)))
        {
            return TypedResults.Ok(tumu);
        }

        return TypedResults.Ok(tumu.Where(i => i.AktifMi && yetkiler.IsyerineGirebilirMi(i.Id)).ToList());
    }

    private static async Task<IResult> EkleAsync(IsyeriKaydetIstegi istek, YonetimDbContext db, IIsyeriSorgusu sorgu, CancellationToken ct)
    {
        if (await LimitAsimiAsync(db, ct) is { } hata)
        {
            return hata.Problem();
        }

        var bilgiler = Bilgiler(istek);
        if (await db.Isyerleri.AnyAsync(i => i.Kod == bilgiler.Kod.ToUpperInvariant(), ct))
        {
            return YonetimHatalari.IsyeriKoduKayitli.Problem();
        }

        var isyeri = Isyeri.Olustur(bilgiler);
        db.Isyerleri.Add(isyeri);
        await db.SaveChangesAsync(ct);
        await sorgu.OnbellektenKaldirAsync(isyeri.FirmaId, ct);
        return TypedResults.Created($"/api/yonetim/isyerleri/{isyeri.Id}", Ozet(isyeri));
    }

    private static async Task<IResult> GuncelleAsync(Guid id, IsyeriKaydetIstegi istek, YonetimDbContext db, IIsyeriSorgusu sorgu, CancellationToken ct)
    {
        var isyeri = await db.Isyerleri.SingleOrDefaultAsync(i => i.Id == id, ct);
        if (isyeri is null)
        {
            return IsyeriHatalari.Bulunamadi.Problem();
        }

        var bilgiler = Bilgiler(istek);
        if (await db.Isyerleri.AnyAsync(i => i.Id != id && i.Kod == bilgiler.Kod.ToUpperInvariant(), ct))
        {
            return YonetimHatalari.IsyeriKoduKayitli.Problem();
        }

        isyeri.Guncelle(bilgiler);
        await db.SaveChangesAsync(ct);
        await sorgu.OnbellektenKaldirAsync(isyeri.FirmaId, ct);
        return TypedResults.Ok(Ozet(isyeri));
    }

    private static async Task<IResult> AktiflestirAsync(Guid id, YonetimDbContext db, IIsyeriSorgusu sorgu, CancellationToken ct)
    {
        if (await db.Isyerleri.AnyAsync(i => i.Id == id && !i.AktifMi, ct) && await LimitAsimiAsync(db, ct) is { } hata)
        {
            return hata.Problem();
        }

        return await DurumAsync(id, db, sorgu, i =>
        {
            i.Aktiflestir();
            return Sonuc.Basarili();
        }, ct);
    }

    // Merkez tektir: yeni merkez seçilince eskisi sıradan işyeri olur. Kapalı işyeri merkez yapılamaz.
    private static async Task<IResult> MerkezYapAsync(Guid id, YonetimDbContext db, IIsyeriSorgusu sorgu, CancellationToken ct)
    {
        var isyerleri = await db.Isyerleri.ToListAsync(ct);
        if (isyerleri.Find(i => i.Id == id) is not { } yeni)
        {
            return IsyeriHatalari.Bulunamadi.Problem();
        }

        if (!yeni.AktifMi)
        {
            return IsyeriHatalari.Pasif.Problem();
        }

        // Benzersiz "tek merkez" indeksi araya ara kayıt ister: önce eskisi kaldırılıp kaydedilir.
        foreach (var eski in isyerleri.Where(i => i.MerkezMi && i.Id != id))
        {
            eski.MerkezYap(false);
        }

        await db.SaveChangesAsync(ct);
        yeni.MerkezYap(true);
        await db.SaveChangesAsync(ct);
        await sorgu.OnbellektenKaldirAsync(yeni.FirmaId, ct);
        return TypedResults.Ok(Ozet(yeni));
    }

    private static async Task<IResult> DurumAsync(Guid id, YonetimDbContext db, IIsyeriSorgusu sorgu, Func<Isyeri, Sonuc> islem, CancellationToken ct)
    {
        var isyeri = await db.Isyerleri.SingleOrDefaultAsync(i => i.Id == id, ct);
        if (isyeri is null)
        {
            return IsyeriHatalari.Bulunamadi.Problem();
        }

        var sonuc = islem(isyeri);
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        await db.SaveChangesAsync(ct);
        await sorgu.OnbellektenKaldirAsync(isyeri.FirmaId, ct);
        return TypedResults.Ok(Ozet(isyeri));
    }

    private static async Task<Hata?> LimitAsimiAsync(YonetimDbContext db, CancellationToken ct) =>
        await db.FirmaLisanslari.Select(l => l.Paket.IsyeriLimiti).SingleOrDefaultAsync(ct) is { } limit
        && await db.Isyerleri.CountAsync(i => i.AktifMi, ct) >= limit
            ? YonetimHatalari.IsyeriLimiti(limit)
            : null;

    private static IsyeriBilgileri Bilgiler(IsyeriKaydetIstegi i) =>
        new(i.Kod, i.Ad, i.Adres, i.Il, i.Ilce, i.Telefon, i.SgkSicilNo);

    internal static IsyeriOzeti Ozet(Isyeri i) => new(i.Id, i.Kod, i.Ad, i.MerkezMi, i.AktifMi, i.Adres, i.Il, i.Ilce, i.Telefon, i.SgkSicilNo);
}
