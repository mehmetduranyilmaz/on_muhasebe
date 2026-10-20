using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.KasaBanka.Domain;
using OnMuhasebe.Modules.KasaBanka.Infrastructure;
using OnMuhasebe.Modules.KasaBanka.Infrastructure.Ekstre;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.KasaBanka.Features;

// Banka/kart/POS ekstresi içe aktarma: yükle → (kolon eşle) → satırları gözden geçir → aktar.
internal static class EkstreIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        grup.MapPost("/hesaplar/{hesapId:guid}/ekstre-aktarimlari", YukleAsync).DisableAntiforgery();
        grup.MapGet("/hesaplar/{hesapId:guid}/ekstre-aktarimlari", ListeleAsync);
        grup.MapGet("/ekstre-aktarimlari/{id:guid}", GetirAsync);
        grup.MapPut("/ekstre-aktarimlari/{id:guid}/esleme", EslemeAsync).Dogrula<EslemeIstegi>();
        grup.MapPut("/ekstre-aktarimlari/{id:guid}/satirlar/{satirId:guid}", SatirGuncelleAsync).Dogrula<SatirGuncelleIstegi>();
        grup.MapPost("/ekstre-aktarimlari/{id:guid}/aktar", AktarAsync);
        grup.MapDelete("/ekstre-aktarimlari/{id:guid}", SilAsync);

        var kurallar = grup.MapGroup("/ekstre-kurallari");
        kurallar.MapGet("", KurallariListeleAsync);
        kurallar.MapPost("", KuralEkleAsync).Dogrula<KuralKaydetIstegi>();
        kurallar.MapPut("/{id:guid}", KuralGuncelleAsync).Dogrula<KuralKaydetIstegi>();
        kurallar.MapDelete("/{id:guid}", KuralSilAsync);
    }

    internal sealed class EslemeDogrulayici : AbstractValidator<EslemeIstegi>
    {
        public EslemeDogrulayici()
        {
            RuleFor(x => x.Esleme.BaslikSatiri).GreaterThanOrEqualTo(-1);
            RuleFor(x => x.Esleme.TarihKolonu).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Esleme.AciklamaKolonu).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Esleme).Must(e => e.TutarKolonu is not null || (e.AlacakKolonu is not null && e.BorcKolonu is not null))
                .WithMessage("Tutar kolonu ya da giriş (alacak) ve çıkış (borç) kolonları seçilmelidir.");
        }
    }

    internal sealed class SatirDogrulayici : AbstractValidator<SatirGuncelleIstegi>
    {
        public SatirDogrulayici()
        {
            RuleFor(x => x.IslemTuru).IsInEnum();
            RuleFor(x => x.HatirlanacakKelime).MaximumLength(100);
            RuleFor(x => x.HatirlanacakKelime).Must(k => k!.Trim().Length >= 3).When(x => !string.IsNullOrWhiteSpace(x.HatirlanacakKelime))
                .WithMessage("Hatırlanacak kelime en az 3 karakter olmalıdır.");
        }
    }

    internal sealed class KuralDogrulayici : AbstractValidator<KuralKaydetIstegi>
    {
        public KuralDogrulayici()
        {
            RuleFor(x => x.AnahtarKelime).NotEmpty().MinimumLength(3).MaximumLength(100);
            RuleFor(x => x.Yon).IsInEnum();
            RuleFor(x => x.IslemTuru).IsInEnum();
        }
    }

    private static async Task<IResult> YukleAsync(
        Guid hesapId, IFormFile? dosya, EkstreServisi servis, KasaBankaDbContext db, ICariSorgusu cariSorgusu, CancellationToken ct)
    {
        if (dosya is null || dosya.Length == 0)
        {
            return KasaBankaHatalari.DosyaOkunamadi.Problem();
        }

        if (dosya.Length > EkstreAktarimi.AzamiDosyaBoyutu)
        {
            return KasaBankaHatalari.DosyaCokBuyuk.Problem();
        }

        using var bellek = new MemoryStream();
        await dosya.CopyToAsync(bellek, ct);
        var sonuc = await servis.YukleAsync(hesapId, dosya.FileName, bellek.ToArray(), ct);
        return sonuc.BasariliMi
            ? TypedResults.Created($"/api/kasabanka/ekstre-aktarimlari/{sonuc.Deger.Id}", await DetayAsync(sonuc.Deger, db, cariSorgusu, ct))
            : sonuc.HttpSonucu();
    }

    private static async Task<IResult> ListeleAsync(Guid hesapId, KasaBankaDbContext db, CancellationToken ct) =>
        TypedResults.Ok(await db.EkstreAktarimlari.AsNoTracking()
            .Where(a => a.HesapId == hesapId)
            .OrderByDescending(a => a.OlusturmaTarihi)
            .Select(a => new AktarimOzeti(a.Id, a.DosyaAdi, a.Bicim, a.Durum, a.OlusturmaTarihi, a.Baslangic, a.Bitis,
                a.Satirlar.Count, a.Satirlar.Count(s => s.Durum == EkstreSatirDurumu.Aktarildi || s.Durum == EkstreSatirDurumu.ZatenKayitli)))
            .ToListAsync(ct));

    private static async Task<IResult> GetirAsync(Guid id, KasaBankaDbContext db, ICariSorgusu cariSorgusu, CancellationToken ct)
    {
        var aktarim = await db.EkstreAktarimlari.AsNoTracking().Include(a => a.Satirlar).SingleOrDefaultAsync(a => a.Id == id, ct);
        return aktarim is null ? KasaBankaHatalari.AktarimBulunamadi.Problem() : TypedResults.Ok(await DetayAsync(aktarim, db, cariSorgusu, ct));
    }

    private static async Task<IResult> EslemeAsync(
        Guid id, EslemeIstegi istek, EkstreServisi servis, KasaBankaDbContext db, ICariSorgusu cariSorgusu, CancellationToken ct)
    {
        var sonuc = await servis.EslemeUygulaAsync(id, istek, ct);
        return sonuc.BasariliMi ? TypedResults.Ok(await DetayAsync(sonuc.Deger, db, cariSorgusu, ct)) : sonuc.HttpSonucu();
    }

    private static async Task<IResult> SatirGuncelleAsync(
        Guid id, Guid satirId, SatirGuncelleIstegi istek, EkstreServisi servis, KasaBankaDbContext db, ICariSorgusu cariSorgusu, CancellationToken ct)
    {
        var sonuc = await servis.SatirGuncelleAsync(id, satirId, istek, ct);
        return sonuc.BasariliMi ? TypedResults.Ok(await DetayAsync(sonuc.Deger, db, cariSorgusu, ct)) : sonuc.HttpSonucu();
    }

    private static async Task<IResult> AktarAsync(Guid id, EkstreServisi servis, KasaBankaDbContext db, ICariSorgusu cariSorgusu, CancellationToken ct)
    {
        var sonuc = await servis.AktarAsync(id, ct);
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        var (aktarim, aktarilan, zatenKayitli, atlanan, hatali) = sonuc.Deger;
        return TypedResults.Ok(new AktarimSonucu(aktarilan, zatenKayitli, atlanan, hatali, await DetayAsync(aktarim, db, cariSorgusu, ct)));
    }

    // Satırı aktarılmamış aktarım (yanlış dosya, yanlış hesap) silinebilir.
    private static async Task<IResult> SilAsync(Guid id, KasaBankaDbContext db, CancellationToken ct)
    {
        var aktarim = await db.EkstreAktarimlari.Include(a => a.Satirlar).SingleOrDefaultAsync(a => a.Id == id, ct);
        if (aktarim is null)
        {
            return KasaBankaHatalari.AktarimBulunamadi.Problem();
        }

        if (aktarim.Satirlar.Any(s => s.Durum is EkstreSatirDurumu.Aktarildi || (s.Durum == EkstreSatirDurumu.ZatenKayitli && aktarim.Durum == AktarimDurumu.Tamamlandi)))
        {
            return KasaBankaHatalari.AktarimSilinemez.Problem();
        }

        db.EkstreAktarimlari.Remove(aktarim);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<AktarimDetayi> DetayAsync(EkstreAktarimi a, KasaBankaDbContext db, ICariSorgusu cariSorgusu, CancellationToken ct)
    {
        var hesap = await db.Hesaplar.AsNoTracking().SingleAsync(h => h.Id == a.HesapId, ct);
        var cariIdleri = a.Satirlar.Where(s => s.CariHesapId is not null).Select(s => s.CariHesapId!.Value).Distinct().ToList();
        IReadOnlyDictionary<Guid, string> unvanlar = cariIdleri.Count == 0 ? new Dictionary<Guid, string>() : await cariSorgusu.UnvanlarAsync(cariIdleri, ct);
        var hareketIdleri = a.Satirlar.Where(s => s.EslesenHareketId is not null).Select(s => s.EslesenHareketId!.Value).ToList();
        var hareketTarihleri = await db.KasaHareketleri.AsNoTracking().Where(h => hareketIdleri.Contains(h.Id)).ToDictionaryAsync(h => h.Id, h => h.Tarih, ct);

        var tablo = a.Durum == AktarimDurumu.Tamamlandi ? null : EkstreServisi.Tablo(a);
        var esleme = a.Esleme ?? (a.Durum == AktarimDurumu.EslemeBekliyor && tablo is not null ? Domain.Ekstre.KolonTahmincisi.Tahmin(tablo) : null);
        return new AktarimDetayi(
            a.Id, a.HesapId, hesap.Ad, hesap.ParaBirimi, a.DosyaAdi, a.Bicim, a.Durum, a.OlusturmaTarihi, a.Baslangic, a.Bitis, a.BakiyeUyarisi, esleme,
            tablo?.Take(EkstreServisi.OnizlemeSatirSayisi).Select(r => (IReadOnlyList<string>)r).ToList() ?? [],
            a.Satirlar.OrderBy(s => s.Sira).Select(s => new EkstreSatiriDetayi(
                s.Id, s.Sira, s.Tarih, s.Aciklama, s.Tutar, s.Bakiye, s.Referans, s.Durum, s.IslemTuru,
                s.CariHesapId, s.CariHesapId is { } c ? unvanlar.GetValueOrDefault(c) : null, s.KategoriId, s.KarsiHesapId,
                s.EslesenHareketId, s.EslesenHareketId is { } h && hareketTarihleri.TryGetValue(h, out var t) ? t : null,
                s.IslemId, s.OneriKaynagi, s.Hata, s.ElleDuzenlendi)).ToList());
    }

    private static async Task<IResult> KurallariListeleAsync(KasaBankaDbContext db, ICariSorgusu cariSorgusu, CancellationToken ct)
    {
        var kurallar = await db.EkstreKurallari.AsNoTracking().OrderByDescending(k => k.KullanimSayisi).ThenBy(k => k.AnahtarKelime).ToListAsync(ct);
        var cariIdleri = kurallar.Where(k => k.CariHesapId is not null).Select(k => k.CariHesapId!.Value).Distinct().ToList();
        IReadOnlyDictionary<Guid, string> unvanlar = cariIdleri.Count == 0 ? new Dictionary<Guid, string>() : await cariSorgusu.UnvanlarAsync(cariIdleri, ct);
        var kategoriler = await db.Kategoriler.AsNoTracking().ToDictionaryAsync(k => k.Id, k => k.Ad, ct);
        var hesaplar = await db.Hesaplar.AsNoTracking().ToDictionaryAsync(h => h.Id, h => h.Ad, ct);
        return TypedResults.Ok(kurallar.Select(k => new EslestirmeKurali(
            k.Id, k.AnahtarKelime, k.Yon, k.IslemTuru,
            k.CariHesapId, k.CariHesapId is { } c ? unvanlar.GetValueOrDefault(c) : null,
            k.KategoriId, k.KategoriId is { } kt ? kategoriler.GetValueOrDefault(kt) : null,
            k.KarsiHesapId, k.KarsiHesapId is { } h ? hesaplar.GetValueOrDefault(h) : null,
            k.KullanimSayisi)).ToList());
    }

    private static async Task<IResult> KuralEkleAsync(KuralKaydetIstegi istek, KasaBankaDbContext db, CancellationToken ct)
    {
        var kural = EkstreKurali.Olustur(istek.AnahtarKelime, istek.Yon);
        kural.Ayarla(istek.IslemTuru, istek.CariHesapId, istek.KategoriId, istek.KarsiHesapId);
        db.EkstreKurallari.Add(kural);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/kasabanka/ekstre-kurallari/{kural.Id}", new { kural.Id });
    }

    private static async Task<IResult> KuralGuncelleAsync(Guid id, KuralKaydetIstegi istek, KasaBankaDbContext db, CancellationToken ct)
    {
        var kural = await db.EkstreKurallari.SingleOrDefaultAsync(k => k.Id == id, ct);
        if (kural is null)
        {
            return KasaBankaHatalari.KuralBulunamadi.Problem();
        }

        kural.AnahtarDegistir(istek.AnahtarKelime, istek.Yon);
        kural.Ayarla(istek.IslemTuru, istek.CariHesapId, istek.KategoriId, istek.KarsiHesapId);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> KuralSilAsync(Guid id, KasaBankaDbContext db, CancellationToken ct)
    {
        var kural = await db.EkstreKurallari.SingleOrDefaultAsync(k => k.Id == id, ct);
        if (kural is null)
        {
            return KasaBankaHatalari.KuralBulunamadi.Problem();
        }

        db.EkstreKurallari.Remove(kural);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
}
