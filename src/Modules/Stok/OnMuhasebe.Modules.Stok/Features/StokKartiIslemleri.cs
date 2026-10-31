using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Modules.Stok.Domain;
using OnMuhasebe.Modules.Stok.Infrastructure;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Contracts.Birimler;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Shared.Contracts.Sayfalama;
using OnMuhasebe.Shared.Contracts.Vergi;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Stok.Features;

internal static class StokKartiIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var kartlar = grup.MapGroup("/kartlar").Yetki(StokYetkileri.Kart);
        kartlar.MapGet("", ListeleAsync);
        kartlar.MapGet("/{id:guid}", DetayAsync);
        kartlar.MapGet("/barkod/{barkod}", BarkodlaBulAsync);
        // GS1 karekodundaki GS ayıracı (0x1D) yol parçasında taşınamaz; okuyucu çıktısı sorgu parametresiyle gönderilir.
        kartlar.MapGet("/barkod", (string kod, StokDbContext db, CancellationToken ct) => BarkodlaBulAsync(kod, db, ct));
        kartlar.MapPost("", EkleAsync).Dogrula<StokKartiKaydetIstegi>();
        kartlar.MapPut("/{id:guid}", GuncelleAsync).Dogrula<StokKartiKaydetIstegi>();
        kartlar.MapPost("/{id:guid}/pasiflestir", (Guid id, StokDbContext db, CancellationToken ct) => DurumAsync(id, db, k => k.Pasiflestir(), ct)).Islem(YetkiIslemleri.Duzenle);
        kartlar.MapPost("/{id:guid}/aktiflestir", (Guid id, StokDbContext db, CancellationToken ct) => DurumAsync(id, db, k => k.Aktiflestir(), ct)).Islem(YetkiIslemleri.Duzenle);
        kartlar.MapDelete("/{id:guid}", SilAsync);
    }

    internal sealed class Dogrulayici : AbstractValidator<StokKartiKaydetIstegi>
    {
        private static readonly AlanKontrolu<StokKartiKaydetIstegi>[] _zorunluKontroller =
        [
            new(StokZorunluAlanlari.Barkod, nameof(StokKartiKaydetIstegi.Barkod), x => ZorunluAlanKurali.Dolu(x.Barkod)),
            new(StokZorunluAlanlari.Grup, nameof(StokKartiKaydetIstegi.GrupId), x => x.GrupId is not null),
            new(StokZorunluAlanlari.AlisFiyati, nameof(StokKartiKaydetIstegi.AlisFiyati), x => x.AlisFiyati is not null),
            new(StokZorunluAlanlari.SatisFiyati, nameof(StokKartiKaydetIstegi.SatisFiyati), x => x.SatisFiyati is not null),
            new(StokZorunluAlanlari.PerakendeFiyati, nameof(StokKartiKaydetIstegi.PerakendeFiyati), x => x.PerakendeFiyati is not null),
            new(StokZorunluAlanlari.KritikSeviye, nameof(StokKartiKaydetIstegi.KritikSeviye), x => x.KritikSeviye is not null),
            new(StokZorunluAlanlari.Aciklama, nameof(StokKartiKaydetIstegi.Aciklama), x => ZorunluAlanKurali.Dolu(x.Aciklama))
        ];

        public Dogrulayici(IZorunluAlanSorgusu zorunlu)
        {
            this.ZorunluAlanlar(zorunlu, StokZorunluAlanlari.Tumu, _zorunluKontroller);
            RuleFor(x => x.Kod).MaximumLength(30).Matches("^[A-Za-z0-9._-]*$")
                .WithMessage("Stok kodu yalnızca harf, rakam, nokta, tire ve alt çizgi içerebilir.");
            RuleFor(x => x.Ad).NotEmpty().MaximumLength(250);
            RuleFor(x => x.Tur).IsInEnum();
            RuleFor(x => x.TemelBirim).Must(Birimler.GecerliMi).WithMessage("Geçersiz birim.");
            RuleFor(x => x.Barkod).MaximumLength(50);
            RuleFor(x => x.KdvOrani).Must(KdvOranlari.GecerliMi).WithMessage("KDV oranı %0, %1, %10 veya %20 olmalıdır.");
            RuleFor(x => x.AlisFiyati).GreaterThanOrEqualTo(0);
            RuleFor(x => x.SatisFiyati).GreaterThanOrEqualTo(0);
            RuleFor(x => x.PerakendeFiyati).GreaterThanOrEqualTo(0);
            RuleFor(x => x.ParaBirimi).Must(ParaBirimleri.GecerliMi).WithMessage("Geçersiz para birimi.");
            RuleFor(x => x.KritikSeviye).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Aciklama).MaximumLength(1000);
            RuleForEach(x => x.EkBirimler).ChildRules(b =>
            {
                b.RuleFor(x => x.BirimKodu).Must(Birimler.GecerliMi).WithMessage("Geçersiz ek birim.");
                b.RuleFor(x => x.Katsayi).GreaterThan(0).WithMessage("Birim katsayısı sıfırdan büyük olmalıdır.");
                b.RuleFor(x => x.Barkod).MaximumLength(50);
            });
        }
    }

    private static async Task<IResult> ListeleAsync(
        StokDbContext db, string? arama, StokTuru? tur, Guid? grupId, bool? aktif, int? sayfa, int? boyut,
        CancellationToken cancellationToken)
    {
        var sorgu = db.StokKartlari.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(arama))
        {
            var metin = arama.Trim();
            var desen = AramaDeseni.Icerir(metin);
            sorgu = sorgu.Where(k => EF.Functions.ILike(k.Ad, desen) || EF.Functions.ILike(k.Kod, desen)
                                     || k.Barkod == metin || k.Birimler.Any(b => b.Barkod == metin));
        }

        if (tur is not null)
        {
            sorgu = sorgu.Where(k => k.Tur == tur);
        }

        if (grupId is not null)
        {
            sorgu = sorgu.Where(k => k.GrupId == grupId);
        }

        if (aktif is not null)
        {
            sorgu = sorgu.Where(k => k.AktifMi == aktif);
        }

        var sonuc = await sorgu
            .OrderBy(k => k.Ad)
            .Select(k => new StokKartiOzeti(
                k.Id, k.Kod, k.Ad, k.Tur, k.TemelBirim, k.Barkod, k.Grup != null ? k.Grup.Ad : null,
                db.StokHareketleri.Where(h => h.StokKartiId == k.Id).Sum(h => h.GirisMiktari - h.CikisMiktari),
                k.KritikSeviye, k.SatisFiyati, k.PerakendeFiyati, k.ParaBirimi, k.AktifMi))
            .SayfalaAsync(new SayfalamaIstegi(sayfa ?? 1, boyut ?? SayfalamaIstegi.VarsayilanBoyut), cancellationToken);

        return TypedResults.Ok(sonuc);
    }

    private static async Task<IResult> DetayAsync(
        Guid id, StokDbContext db, StokHesaplayici hesaplayici, StokAyarServisi ayarServisi, IYetkiDenetimi yetki, CancellationToken cancellationToken)
    {
        var detay = await DetayGetirAsync(db, hesaplayici, ayarServisi, id, cancellationToken);
        if (detay is null)
        {
            return StokHatalari.KartBulunamadi.Problem();
        }

        return TypedResults.Ok(await yetki.GorurMuAsync(cancellationToken) ? detay : MaliyetYetkisi.Gizle(detay));
    }

    // Düz barkod (EAN/UPC) ya da GS1 karekod/barkod: GS1'de GTIN ile kart bulunur, lot/SKT/seri de döner.
    private static async Task<IResult> BarkodlaBulAsync(string barkod, StokDbContext db, CancellationToken cancellationToken)
    {
        var gs1 = Gs1Ayrastirici.Ayristir(barkod);
        IReadOnlyList<string> adaylar = gs1 is null ? [barkod] : Gs1Ayrastirici.BarkodAdaylari(gs1.Gtin);
        var kart = await db.StokKartlari.AsNoTracking().Include(k => k.Birimler)
            .FirstOrDefaultAsync(k => adaylar.Contains(k.Barkod!) || k.Birimler.Any(b => adaylar.Contains(b.Barkod!)), cancellationToken);
        if (kart is null)
        {
            return StokHatalari.BarkodBulunamadi.Problem();
        }

        var birim = kart.Birimler.FirstOrDefault(b => b.Barkod is not null && adaylar.Contains(b.Barkod));
        return TypedResults.Ok(new BarkodSonucu(kart.Id, kart.Kod, kart.Ad, birim?.BirimKodu ?? kart.TemelBirim, birim?.Katsayi ?? 1m,
            gs1?.LotNo, gs1?.SonKullanmaTarihi, gs1?.SeriNo));
    }

    private static async Task<IResult> EkleAsync(
        StokKartiKaydetIstegi istek, StokDbContext db, StokKodUretici kodUretici, StokHesaplayici hesaplayici,
        StokAyarServisi ayarServisi, IYetkiDenetimi yetki, CancellationToken cancellationToken)
    {
        if (await OnKontrolAsync(db, istek, haricId: null, cancellationToken) is { } hata)
        {
            return hata.Problem();
        }

        var kod = string.IsNullOrWhiteSpace(istek.Kod) ? await kodUretici.SonrakiAsync(cancellationToken) : istek.Kod.Trim();
        if (await KodKullanimdaMiAsync(db, kod, null, cancellationToken))
        {
            return StokHatalari.KodKullaniliyor.Problem();
        }

        var maliyetGorur = await yetki.GorurMuAsync(cancellationToken);
        var bilgiler = StokKartiBilgileri.Istekten(istek);
        var sonuc = StokKarti.Olustur(kod, maliyetGorur ? bilgiler : bilgiler with { AlisFiyati = null }, istek.EkBirimler ?? []);
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        db.StokKartlari.Add(sonuc.Deger);
        await db.SaveChangesAsync(cancellationToken);
        var detay = (await DetayGetirAsync(db, hesaplayici, ayarServisi, sonuc.Deger.Id, cancellationToken))!;
        return TypedResults.Created($"/api/stok/kartlar/{sonuc.Deger.Id}", maliyetGorur ? detay : MaliyetYetkisi.Gizle(detay));
    }

    private static async Task<IResult> GuncelleAsync(
        Guid id, StokKartiKaydetIstegi istek, StokDbContext db, StokHesaplayici hesaplayici, StokAyarServisi ayarServisi,
        IYetkiDenetimi yetki, CancellationToken cancellationToken)
    {
        var kart = await db.StokKartlari.Include(k => k.Birimler).SingleOrDefaultAsync(k => k.Id == id, cancellationToken);
        if (kart is null)
        {
            return StokHatalari.KartBulunamadi.Problem();
        }

        // İstemcinin gördüğü sürüm; arada başkası değiştirdiyse kayıt 409 ile reddedilir.
        db.Entry(kart).Property(k => k.Surum).OriginalValue = istek.Surum;

        if (await OnKontrolAsync(db, istek, id, cancellationToken) is { } hata)
        {
            return hata.Problem();
        }

        var kod = string.IsNullOrWhiteSpace(istek.Kod) ? kart.Kod : istek.Kod.Trim();
        if (kod != kart.Kod && await KodKullanimdaMiAsync(db, kod, id, cancellationToken))
        {
            return StokHatalari.KodKullaniliyor.Problem();
        }

        var hareketVarMi = await db.StokHareketleri.AnyAsync(h => h.StokKartiId == id, cancellationToken);
        // Alış fiyatını göremeyen kullanıcının formu onu boş gönderir; mevcut değer korunur.
        var maliyetGorur = await yetki.GorurMuAsync(cancellationToken);
        var bilgiler = StokKartiBilgileri.Istekten(istek);
        var sonuc = kart.Guncelle(kod, maliyetGorur ? bilgiler : bilgiler with { AlisFiyati = kart.AlisFiyati }, istek.EkBirimler ?? [], hareketVarMi);
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        await db.SaveChangesAsync(cancellationToken);
        var detay = (await DetayGetirAsync(db, hesaplayici, ayarServisi, id, cancellationToken))!;
        return TypedResults.Ok(maliyetGorur ? detay : MaliyetYetkisi.Gizle(detay));
    }

    private static async Task<IResult> DurumAsync(Guid id, StokDbContext db, Action<StokKarti> islem, CancellationToken cancellationToken)
    {
        var kart = await db.StokKartlari.SingleOrDefaultAsync(k => k.Id == id, cancellationToken);
        if (kart is null)
        {
            return StokHatalari.KartBulunamadi.Problem();
        }

        islem(kart);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> SilAsync(Guid id, StokDbContext db, CancellationToken cancellationToken)
    {
        var kart = await db.StokKartlari.SingleOrDefaultAsync(k => k.Id == id, cancellationToken);
        if (kart is null)
        {
            return StokHatalari.KartBulunamadi.Problem();
        }

        if (await db.StokHareketleri.AnyAsync(h => h.StokKartiId == id, cancellationToken))
        {
            return StokHatalari.HareketliKartSilinemez.Problem();
        }

        db.StokKartlari.Remove(kart);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    internal static async Task<StokKartiDetayi?> DetayGetirAsync(
        StokDbContext db, StokHesaplayici hesaplayici, StokAyarServisi ayarServisi, Guid id, CancellationToken cancellationToken)
    {
        var kart = await db.StokKartlari.AsNoTracking().Include(k => k.Birimler).Include(k => k.Grup)
            .SingleOrDefaultAsync(k => k.Id == id, cancellationToken);
        if (kart is null)
        {
            return null;
        }

        var ayar = await ayarServisi.GetirAsync(cancellationToken);
        var hesaplar = await hesaplayici.HesaplaAsync(db.StokKartlari.Where(k => k.Id == id).Select(k => k.Id), null, ayar.MaliyetYontemi, cancellationToken);
        var hesap = hesaplar.GetValueOrDefault(id);
        var depolar = await db.Depolar.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Ad, cancellationToken);

        return new StokKartiDetayi(
            kart.Id, kart.Kod, kart.Ad, kart.Tur, kart.TemelBirim, kart.Barkod, kart.KdvOrani, kart.AlisFiyati, kart.SatisFiyati, kart.PerakendeFiyati,
            kart.ParaBirimi, kart.GrupId, kart.Grup?.Ad, kart.KritikSeviye, kart.Aciklama, kart.AktifMi,
            kart.Birimler.OrderBy(b => b.Katsayi).Select(b => new EkBirim(b.BirimKodu, b.Katsayi, b.Barkod)).ToList(),
            hesap?.DepoMiktarlari.Select(d => new DepoMiktari(d.Key, depolar.GetValueOrDefault(d.Key, "?"), d.Value))
                .OrderBy(d => d.DepoAdi).ToList() ?? [],
            hesap?.Miktar ?? 0m,
            Math.Round(hesap?.BirimMaliyet ?? 0m, 4),
            Math.Round(hesap?.Deger ?? 0m, 2),
            ayar.MaliyetYontemi,
            hesap is not null,
            kart.Surum,
            kart.Izleme,
            kart.SktTakibi);
    }

    // Grup var mı, barkodlar (kart + ek birimler) firma içinde benzersiz mi.
    private static async Task<Hata?> OnKontrolAsync(StokDbContext db, StokKartiKaydetIstegi istek, Guid? haricId, CancellationToken cancellationToken)
    {
        if (istek.GrupId is not null && !await db.StokGruplari.AnyAsync(g => g.Id == istek.GrupId, cancellationToken))
        {
            return StokHatalari.GrupBulunamadi;
        }

        var barkodlar = (istek.EkBirimler ?? []).Select(b => b.Barkod).Append(istek.Barkod)
            .Where(b => !string.IsNullOrWhiteSpace(b)).Select(b => b!.Trim()).ToList();
        if (barkodlar.Count == 0)
        {
            return null;
        }

        if (barkodlar.Distinct().Count() != barkodlar.Count
            || await db.StokKartlari.AnyAsync(k => k.Id != haricId
                    && (barkodlar.Contains(k.Barkod!) || k.Birimler.Any(b => barkodlar.Contains(b.Barkod!))), cancellationToken))
        {
            return StokHatalari.BarkodKullaniliyor;
        }

        return null;
    }

    private static Task<bool> KodKullanimdaMiAsync(StokDbContext db, string kod, Guid? haricId, CancellationToken cancellationToken) =>
        db.StokKartlari.IgnoreQueryFilters([ModulDbContext.SilinmeFiltresi])
            .AnyAsync(k => k.Kod == kod && k.Id != haricId, cancellationToken);
}
