using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.KasaBanka.Domain;
using OnMuhasebe.Modules.KasaBanka.Infrastructure;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Shared.Contracts.Sayfalama;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Domain.Dogrulama;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.KasaBanka.Features;

internal static class HesapIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var hesaplar = grup.MapGroup("/hesaplar");
        hesaplar.MapGet("", ListeleAsync);
        hesaplar.MapGet("/{id:guid}", DetayAsync);
        hesaplar.MapPost("", EkleAsync).Dogrula<HesapKaydetIstegi>();
        hesaplar.MapPut("/{id:guid}", GuncelleAsync).Dogrula<HesapKaydetIstegi>();
        hesaplar.MapPost("/{id:guid}/pasiflestir", (Guid id, KasaBankaDbContext db, CancellationToken ct) =>
            DurumDegistirAsync(id, db, h => h.Pasiflestir(), ct));
        hesaplar.MapPost("/{id:guid}/aktiflestir", (Guid id, KasaBankaDbContext db, CancellationToken ct) =>
            DurumDegistirAsync(id, db, h => h.Aktiflestir(), ct));
        hesaplar.MapDelete("/{id:guid}", SilAsync);
        hesaplar.MapGet("/{id:guid}/hareketler", HareketleriListeleAsync);
        hesaplar.MapGet("/{id:guid}/ekstre", EkstreAsync);
    }

    internal sealed class Dogrulayici : AbstractValidator<HesapKaydetIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.Kod).MaximumLength(20);
            RuleFor(x => x.Ad).NotEmpty().MaximumLength(150);
            RuleFor(x => x.Tur).IsInEnum();
            RuleFor(x => x.ParaBirimi).Must(ParaBirimleri.GecerliMi).WithMessage("Geçersiz para birimi.");
            RuleFor(x => x.BankaAdi).MaximumLength(100);
            RuleFor(x => x.Sube).MaximumLength(100);
            RuleFor(x => x.HesapNo).MaximumLength(50);
            RuleFor(x => x.Iban).Must(Iban.GecerliMi).When(x => !string.IsNullOrWhiteSpace(x.Iban))
                .WithMessage("Geçerli bir IBAN girin (TR ile başlayan 26 karakter).");
            RuleFor(x => x.Limit).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Aciklama).MaximumLength(500);
        }
    }

    private static async Task<IResult> ListeleAsync(
        KasaBankaDbContext db, HesapTuru? tur, bool? aktif, string? arama, CancellationToken cancellationToken)
    {
        var sorgu = db.Hesaplar.AsNoTracking();
        if (tur is not null)
        {
            sorgu = sorgu.Where(h => h.Tur == tur);
        }

        if (aktif is not null)
        {
            sorgu = sorgu.Where(h => h.AktifMi == aktif);
        }

        if (!string.IsNullOrWhiteSpace(arama))
        {
            var desen = $"%{arama.Trim()}%";
            sorgu = sorgu.Where(h => EF.Functions.ILike(h.Ad, desen) || EF.Functions.ILike(h.Kod, desen) || (h.Iban != null && EF.Functions.ILike(h.Iban, desen)));
        }

        return TypedResults.Ok(await Ozetler(db, sorgu).ToListAsync(cancellationToken));
    }

    internal static IQueryable<HesapOzeti> Ozetler(KasaBankaDbContext db, IQueryable<KasaHesabi> hesaplar) =>
        hesaplar
            // Tür sütunu metin olarak tutulur; sıralama alfabetik değil tür sırasıyla (kasa, banka, kart, POS) yapılır.
            .OrderBy(h => h.Tur == HesapTuru.Kasa ? 0 : h.Tur == HesapTuru.Banka ? 1 : h.Tur == HesapTuru.KrediKarti ? 2 : 3).ThenBy(h => h.Ad)
            .Select(h => new HesapOzeti(
                h.Id, h.Kod, h.Ad, h.Tur, h.ParaBirimi, h.BankaAdi, h.Iban, h.Limit, h.AktifMi,
                db.KasaHareketleri.Where(x => x.HesapId == h.Id).Sum(x => x.Giris - x.Cikis),
                db.KasaHareketleri.Where(x => x.HesapId == h.Id).Sum(x => x.GirisTl - x.CikisTl)));

    private static async Task<IResult> DetayAsync(Guid id, KasaBankaDbContext db, CancellationToken cancellationToken) =>
        await DetayGetirAsync(db, id, cancellationToken) is { } detay ? TypedResults.Ok(detay) : KasaBankaHatalari.HesapBulunamadi.Problem();

    private static async Task<IResult> EkleAsync(
        HesapKaydetIstegi istek, KasaBankaDbContext db, KasaKodUretici kodUretici, CancellationToken cancellationToken)
    {
        var kod = string.IsNullOrWhiteSpace(istek.Kod) ? await kodUretici.SonrakiAsync(istek.Tur, cancellationToken) : istek.Kod.Trim();
        if (await KodKullanimdaMiAsync(db, kod, null, cancellationToken))
        {
            return KasaBankaHatalari.KodKullaniliyor.Problem();
        }

        var hesap = KasaHesabi.Olustur(kod, HesapBilgileri.Istekten(istek));
        db.Hesaplar.Add(hesap);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/kasabanka/hesaplar/{hesap.Id}", await DetayGetirAsync(db, hesap.Id, cancellationToken));
    }

    private static async Task<IResult> GuncelleAsync(Guid id, HesapKaydetIstegi istek, KasaBankaDbContext db, CancellationToken cancellationToken)
    {
        var hesap = await db.Hesaplar.SingleOrDefaultAsync(h => h.Id == id, cancellationToken);
        if (hesap is null)
        {
            return KasaBankaHatalari.HesapBulunamadi.Problem();
        }

        // İstemcinin gördüğü sürüm; arada başkası değiştirdiyse kayıt 409 ile reddedilir.
        db.Entry(hesap).Property(h => h.Surum).OriginalValue = istek.Surum;

        var kod = string.IsNullOrWhiteSpace(istek.Kod) ? hesap.Kod : istek.Kod.Trim();
        if (kod != hesap.Kod && await KodKullanimdaMiAsync(db, kod, id, cancellationToken))
        {
            return KasaBankaHatalari.KodKullaniliyor.Problem();
        }

        var sonuc = hesap.Guncelle(kod, HesapBilgileri.Istekten(istek), await db.KasaHareketleri.AnyAsync(h => h.HesapId == id, cancellationToken));
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(await DetayGetirAsync(db, id, cancellationToken));
    }

    private static async Task<IResult> DurumDegistirAsync(Guid id, KasaBankaDbContext db, Action<KasaHesabi> islem, CancellationToken cancellationToken)
    {
        var hesap = await db.Hesaplar.SingleOrDefaultAsync(h => h.Id == id, cancellationToken);
        if (hesap is null)
        {
            return KasaBankaHatalari.HesapBulunamadi.Problem();
        }

        islem(hesap);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> SilAsync(Guid id, KasaBankaDbContext db, CancellationToken cancellationToken)
    {
        var hesap = await db.Hesaplar.SingleOrDefaultAsync(h => h.Id == id, cancellationToken);
        if (hesap is null)
        {
            return KasaBankaHatalari.HesapBulunamadi.Problem();
        }

        if (await db.KasaHareketleri.AnyAsync(h => h.HesapId == id, cancellationToken))
        {
            return KasaBankaHatalari.HareketliHesapSilinemez.Problem();
        }

        db.Hesaplar.Remove(hesap);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> HareketleriListeleAsync(
        Guid id, KasaBankaDbContext db, ICariSorgusu cariSorgusu, int? sayfa, int? boyut, CancellationToken cancellationToken)
    {
        if (!await db.Hesaplar.AnyAsync(h => h.Id == id, cancellationToken))
        {
            return KasaBankaHatalari.HesapBulunamadi.Problem();
        }

        var sonuc = await Satirlar(db, db.KasaHareketleri.Where(h => h.HesapId == id).OrderByDescending(h => h.Tarih).ThenByDescending(h => h.Id))
            .SayfalaAsync(new SayfalamaIstegi(sayfa ?? 1, boyut ?? SayfalamaIstegi.VarsayilanBoyut), cancellationToken);
        return TypedResults.Ok(sonuc with { Kayitlar = await UnvanlariEkleAsync(sonuc.Kayitlar, cariSorgusu, cancellationToken) });
    }

    // Yürüyen bakiyeli hesap dökümü. Tutarlar hesabın dövizindedir.
    private static async Task<IResult> EkstreAsync(
        Guid id, DateOnly? baslangic, DateOnly? bitis, KasaBankaDbContext db, ICariSorgusu cariSorgusu, TimeProvider zaman,
        CancellationToken cancellationToken)
    {
        var hesap = await db.Hesaplar.AsNoTracking().SingleOrDefaultAsync(h => h.Id == id, cancellationToken);
        if (hesap is null)
        {
            return KasaBankaHatalari.HesapBulunamadi.Problem();
        }

        var bugun = TurkiyeSaati.Bugun(zaman);
        var (bas, son) = (baslangic ?? new DateOnly(bugun.Year, bugun.Month, 1), bitis ?? bugun);
        if (son < bas)
        {
            return Hata.Dogrulama("tarih_araligi", "Bitiş tarihi başlangıçtan önce olamaz.").Problem();
        }

        var devir = await db.KasaHareketleri.Where(h => h.HesapId == id && h.Tarih < bas).SumAsync(h => h.Giris - h.Cikis, cancellationToken);
        var satirlar = await Satirlar(db, db.KasaHareketleri.Where(h => h.HesapId == id && h.Tarih >= bas && h.Tarih <= son)
                .OrderBy(h => h.Tarih).ThenBy(h => h.Id))
            .ToListAsync(cancellationToken);
        satirlar = await UnvanlariEkleAsync(satirlar, cariSorgusu, cancellationToken);

        var bakiye = devir;
        var ekstre = new List<KasaEkstreSatiri>(satirlar.Count);
        foreach (var s in satirlar)
        {
            bakiye += s.Giris - s.Cikis;
            ekstre.Add(new KasaEkstreSatiri(s.Id, s.IslemId, s.Tarih, s.Tur, s.BelgeNo, s.Aciklama, s.CariUnvan, s.Giris, s.Cikis, bakiye, s.KaynakBelgeId));
        }

        return TypedResults.Ok(new HesapEkstresi(hesap.Id, hesap.Kod, hesap.Ad, hesap.ParaBirimi, bas, son, devir, ekstre,
            ekstre.Sum(s => s.Giris), ekstre.Sum(s => s.Cikis), bakiye));
    }

    internal static IQueryable<KasaHareketSatiri> Satirlar(KasaBankaDbContext db, IQueryable<KasaHareketi> hareketler) =>
        from h in hareketler.AsNoTracking()
        join k in db.Kategoriler on h.KategoriId equals k.Id into kategoriler
        from k in kategoriler.DefaultIfEmpty()
        select new KasaHareketSatiri(
            h.Id, h.IslemId, h.HesapId, h.Tarih, h.Tur, h.BelgeNo, h.Aciklama, h.Giris, h.Cikis, h.Kur, h.GirisTl, h.CikisTl,
            h.CariHesapId, null, k != null ? k.Ad : null, h.KaynakBelgeId,
            h.IptalHareketId != null, h.IptalEdilenHareketId != null,
            h.IptalHareketId == null && h.IptalEdilenHareketId == null && h.KaynakBelgeId == null);

    // Cari unvanları Cari modülünden tek sorguda alınır.
    internal static async Task<List<KasaHareketSatiri>> UnvanlariEkleAsync(
        IReadOnlyList<KasaHareketSatiri> satirlar, ICariSorgusu cariSorgusu, CancellationToken cancellationToken)
    {
        var idler = satirlar.Where(s => s.CariHesapId is not null).Select(s => s.CariHesapId!.Value).Distinct().ToList();
        IReadOnlyDictionary<Guid, string> unvanlar = idler.Count == 0 ? new Dictionary<Guid, string>() : await cariSorgusu.UnvanlarAsync(idler, cancellationToken);
        return satirlar.Select(s => s.CariHesapId is { } c && unvanlar.TryGetValue(c, out var u) ? s with { CariUnvan = u } : s).ToList();
    }

    private static Task<HesapDetayi?> DetayGetirAsync(KasaBankaDbContext db, Guid id, CancellationToken cancellationToken) =>
        db.Hesaplar.AsNoTracking()
            .Where(h => h.Id == id)
            .Select(h => new HesapDetayi(
                h.Id, h.Kod, h.Ad, h.Tur, h.ParaBirimi, h.BankaAdi, h.Sube, h.HesapNo, h.Iban, h.Limit, h.Aciklama, h.AktifMi,
                db.KasaHareketleri.Where(x => x.HesapId == h.Id).Sum(x => x.Giris - x.Cikis),
                db.KasaHareketleri.Where(x => x.HesapId == h.Id).Sum(x => x.GirisTl - x.CikisTl),
                db.KasaHareketleri.Any(x => x.HesapId == h.Id),
                h.Surum))
            .SingleOrDefaultAsync(cancellationToken);

    private static Task<bool> KodKullanimdaMiAsync(KasaBankaDbContext db, string kod, Guid? haricId, CancellationToken cancellationToken) =>
        db.Hesaplar.IgnoreQueryFilters([ModulDbContext.SilinmeFiltresi]).AnyAsync(h => h.Kod == kod && h.Id != haricId, cancellationToken);
}
