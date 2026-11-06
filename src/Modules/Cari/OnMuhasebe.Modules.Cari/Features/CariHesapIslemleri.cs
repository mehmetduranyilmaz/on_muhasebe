using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.Cari.Domain;
using OnMuhasebe.Modules.Cari.Infrastructure;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Shared.Contracts.Sayfalama;
using OnMuhasebe.Shared.Contracts.Vergi;
using OnMuhasebe.Shared.Domain.Dogrulama;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Cari.Features;

internal static class CariHesapIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var hesaplar = grup.MapGroup("/hesaplar").Yetki(CariYetkileri.Hesap);
        hesaplar.MapGet("", ListeleAsync);
        hesaplar.MapGet("/{id:guid}", DetayAsync);
        hesaplar.MapPost("", EkleAsync).Dogrula<CariKaydetIstegi>();
        hesaplar.MapPut("/{id:guid}", GuncelleAsync).Dogrula<CariKaydetIstegi>();
        hesaplar.MapPost("/{id:guid}/pasiflestir", (Guid id, CariDbContext db, CancellationToken ct) =>
            DurumDegistirAsync(id, db, c => c.Pasiflestir(), ct)).Islem(YetkiIslemleri.Duzenle);
        hesaplar.MapPost("/{id:guid}/aktiflestir", (Guid id, CariDbContext db, CancellationToken ct) =>
            DurumDegistirAsync(id, db, c => c.Aktiflestir(), ct)).Islem(YetkiIslemleri.Duzenle);
        hesaplar.MapDelete("/{id:guid}", SilAsync);
    }

    private const int EnCokAdres = 20;
    private const int EnCokIlgiliKisi = 20;
    private const int EnCokBankaHesabi = 10;

    internal sealed class Dogrulayici : AbstractValidator<CariKaydetIstegi>
    {
        private static readonly AlanKontrolu<CariKaydetIstegi>[] _zorunluKontroller =
        [
            new(CariZorunluAlanlari.VergiNo, nameof(CariKaydetIstegi.VergiNo), x => ZorunluAlanKurali.Dolu(x.VergiNo)),
            new(CariZorunluAlanlari.VergiDairesi, nameof(CariKaydetIstegi.VergiDairesi), x => ZorunluAlanKurali.Dolu(x.VergiDairesi)),
            new(CariZorunluAlanlari.Il, nameof(CariKaydetIstegi.Il), x => ZorunluAlanKurali.Dolu(x.Il)),
            new(CariZorunluAlanlari.Ilce, nameof(CariKaydetIstegi.Ilce), x => ZorunluAlanKurali.Dolu(x.Ilce)),
            new(CariZorunluAlanlari.Adres, nameof(CariKaydetIstegi.Adres), x => ZorunluAlanKurali.Dolu(x.Adres)),
            new(CariZorunluAlanlari.Telefon, nameof(CariKaydetIstegi.Telefon), x => ZorunluAlanKurali.Dolu(x.Telefon)),
            new(CariZorunluAlanlari.Eposta, nameof(CariKaydetIstegi.Eposta), x => ZorunluAlanKurali.Dolu(x.Eposta)),
            new(CariZorunluAlanlari.BankaHesabi, nameof(CariKaydetIstegi.BankaHesaplari), x => x.BankaHesaplari?.Any(h => ZorunluAlanKurali.Dolu(h.Iban)) == true),
            new(CariZorunluAlanlari.Grup, nameof(CariKaydetIstegi.GrupId), x => x.GrupId is not null),
            new(CariZorunluAlanlari.VadeGun, nameof(CariKaydetIstegi.VadeGun), x => x.VadeGun is not null),
            new(CariZorunluAlanlari.RiskLimiti, nameof(CariKaydetIstegi.RiskLimiti), x => x.RiskLimiti is not null),
            new(CariZorunluAlanlari.IlgiliKisi, nameof(CariKaydetIstegi.IlgiliKisiler), x => x.IlgiliKisiler is { Count: > 0 }),
            new(CariZorunluAlanlari.IlgiliKisiTelefonu, nameof(CariKaydetIstegi.IlgiliKisiler),
                x => x.IlgiliKisiler is { Count: > 0 } kisiler && kisiler.All(k => ZorunluAlanKurali.Dolu(k.Telefon)))
        ];

        public Dogrulayici(IZorunluAlanSorgusu zorunlu)
        {
            this.ZorunluAlanlar(zorunlu, CariZorunluAlanlari.Tumu, _zorunluKontroller);
            RuleFor(x => x.Kod).MaximumLength(20).Matches("^[A-Za-z0-9._-]*$")
                .WithMessage("Cari kodu yalnızca harf, rakam, nokta, tire ve alt çizgi içerebilir.");
            RuleFor(x => x.Unvan).NotEmpty().MaximumLength(250);
            RuleFor(x => x.Tur).IsInEnum();
            RuleFor(x => x.KisiTipi).IsInEnum();
            RuleFor(x => x.VergiNo)
                .Must(v => VergiKimlik.GecerliMi(v!.Trim()))
                .When(x => !string.IsNullOrWhiteSpace(x.VergiNo))
                .WithMessage("Geçerli bir vergi kimlik (10 hane) veya T.C. kimlik (11 hane) numarası girin.");
            RuleFor(x => x.VergiNo)
                .Must(v => v!.Trim().Length == 11)
                .When(x => x.KisiTipi == KisiTipi.Sahis && !string.IsNullOrWhiteSpace(x.VergiNo))
                .WithMessage("Şahıs carilerde T.C. kimlik numarası (11 hane) girilmelidir.");
            RuleFor(x => x.VergiDairesi).MaximumLength(100);
            RuleFor(x => x.Ad).MaximumLength(100);
            RuleFor(x => x.Soyad).MaximumLength(100);
            RuleFor(x => x.Ulke).MaximumLength(60);
            RuleFor(x => x.PostaKodu).MaximumLength(10);
            RuleFor(x => x.CepTelefonu).MaximumLength(30);
            RuleFor(x => x.Faks).MaximumLength(30);
            RuleFor(x => x.WebSitesi).MaximumLength(200);
            RuleFor(x => x.VergiNo).NotEmpty().When(x => x.EFaturaMukellefiMi)
                .WithMessage("e-Fatura mükellefi carinin vergi / T.C. kimlik numarası girilmelidir.");
            RuleFor(x => x.EFaturaEtiketi).MaximumLength(100).Matches(@"^urn:mail:[^@\s]+@[^@\s]+$")
                .When(x => x.EFaturaMukellefiMi && !string.IsNullOrWhiteSpace(x.EFaturaEtiketi))
                .WithMessage("e-Fatura etiketi urn:mail:defaultpk@firma.com.tr biçiminde olmalıdır.");
            RuleFor(x => x.VarsayilanKdvIstisnaKodu).Must(k => KdvIstisnaKodlari.GecerliMi(k!.Trim()))
                .When(x => !string.IsNullOrWhiteSpace(x.VarsayilanKdvIstisnaKodu)).WithMessage("Geçersiz ya da kullanımdan kalkmış KDV istisna kodu.");
            RuleFor(x => x.VarsayilanTevkifatKodu).Must(k => TevkifatKodlari.GecerliMi(k!.Trim()))
                .When(x => !string.IsNullOrWhiteSpace(x.VarsayilanTevkifatKodu)).WithMessage("Geçersiz tevkifat kodu.");
            // İstisna KDV'yi sıfırlar; sıfır KDV'nin tevkifatı olmaz.
            RuleFor(x => x.VarsayilanTevkifatKodu).Empty()
                .When(x => !string.IsNullOrWhiteSpace(x.VarsayilanKdvIstisnaKodu))
                .WithMessage("KDV istisnası ile tevkifat birlikte seçilemez.");

            RuleFor(x => x.Adresler!.Count).LessThanOrEqualTo(EnCokAdres).When(x => x.Adresler is not null)
                .WithName("Adresler").WithMessage($"En çok {EnCokAdres} ek adres girilebilir.");
            RuleForEach(x => x.Adresler).ChildRules(a =>
            {
                a.RuleFor(y => y.Baslik).NotEmpty().WithMessage("Adres başlığı gerekli.").MaximumLength(60);
                a.RuleFor(y => y.Adres).NotEmpty().WithMessage("Adres gerekli.").MaximumLength(500);
                a.RuleFor(y => y.AdSoyad).MaximumLength(100);
                a.RuleFor(y => y.Telefon).MaximumLength(30);
                a.RuleFor(y => y.Ulke).MaximumLength(60);
                a.RuleFor(y => y.Il).MaximumLength(50);
                a.RuleFor(y => y.Ilce).MaximumLength(50);
                a.RuleFor(y => y.PostaKodu).MaximumLength(10);
            });
            RuleFor(x => x.IlgiliKisiler!.Count).LessThanOrEqualTo(EnCokIlgiliKisi).When(x => x.IlgiliKisiler is not null)
                .WithName("İlgili kişiler").WithMessage($"En çok {EnCokIlgiliKisi} ilgili kişi girilebilir.");
            RuleForEach(x => x.IlgiliKisiler).ChildRules(k =>
            {
                k.RuleFor(y => y.AdSoyad).NotEmpty().WithMessage("İlgili kişinin adı gerekli.").MaximumLength(100);
                k.RuleFor(y => y.Gorev).MaximumLength(60);
                k.RuleFor(y => y.Telefon).MaximumLength(30);
                k.RuleFor(y => y.Eposta).EmailAddress().MaximumLength(256).When(y => !string.IsNullOrWhiteSpace(y.Eposta));
            });
            RuleFor(x => x.BankaHesaplari!.Count).LessThanOrEqualTo(EnCokBankaHesabi).When(x => x.BankaHesaplari is not null)
                .WithName("Banka hesapları").WithMessage($"En çok {EnCokBankaHesabi} banka hesabı girilebilir.");
            RuleForEach(x => x.BankaHesaplari).ChildRules(h =>
            {
                h.RuleFor(y => y.BankaAdi).NotEmpty().WithMessage("Banka adı gerekli.").MaximumLength(100);
                h.RuleFor(y => y.Sube).MaximumLength(100);
                h.RuleFor(y => y.HesapNo).MaximumLength(30);
                h.RuleFor(y => y.Iban).Must(Iban.GecerliMi).When(y => !string.IsNullOrWhiteSpace(y.Iban))
                    .WithMessage("Geçerli bir IBAN girin (TR ile başlayan 26 karakter).");
                h.RuleFor(y => y).Must(y => !string.IsNullOrWhiteSpace(y.Iban) || !string.IsNullOrWhiteSpace(y.HesapNo))
                    .WithName("Banka hesabı").WithMessage("Banka hesabında IBAN ya da hesap numarası girilmelidir.");
                h.RuleFor(y => y.ParaBirimi).Must(ParaBirimleri.GecerliMi).WithMessage("Geçersiz para birimi.");
            });
            RuleFor(x => x.AcilisBakiyesi!).ChildRules(a =>
            {
                a.RuleFor(y => y.Yon).IsInEnum();
                a.RuleFor(y => y.Tutar).GreaterThan(0).WithMessage("Açılış bakiyesi sıfırdan büyük olmalıdır.");
                a.RuleFor(y => y.Kur).GreaterThan(0).When(y => y.Kur is not null).WithMessage("Kur sıfırdan büyük olmalıdır.");
            }).When(x => x.AcilisBakiyesi is not null);
            RuleFor(x => x.Il).MaximumLength(50);
            RuleFor(x => x.Ilce).MaximumLength(50);
            RuleFor(x => x.Adres).MaximumLength(500);
            RuleFor(x => x.Telefon).MaximumLength(30);
            RuleFor(x => x.Eposta).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.Eposta));
            RuleFor(x => x.ParaBirimi).Must(ParaBirimleri.GecerliMi).WithMessage("Geçersiz para birimi.");
            RuleFor(x => x.VadeGun).InclusiveBetween(0, 365);
            RuleFor(x => x.VadeFarkiOrani).InclusiveBetween(0, 500).When(x => x.VadeFarkiOrani is not null);
            RuleFor(x => x.RiskLimiti).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Not).MaximumLength(1000);
        }
    }

    private static async Task<IResult> ListeleAsync(
        CariDbContext db,
        string? arama,
        CariTuru? tur,
        Guid? grupId,
        bool? aktif,
        int? sayfa,
        int? boyut,
        CancellationToken cancellationToken)
    {
        var sorgu = db.CariHesaplar.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(arama))
        {
            var desen = AramaDeseni.Icerir(arama);
            sorgu = sorgu.Where(c => EF.Functions.ILike(c.Unvan, desen) || EF.Functions.ILike(c.Kod, desen) || c.VergiNo == arama.Trim());
        }

        if (tur is { } t)
        {
            // "Müşteri ve tedarikçi" olan cariler her iki filtrede de görünür.
            sorgu = sorgu.Where(c => c.Tur == t || c.Tur == CariTuru.MusteriVeTedarikci);
        }

        if (grupId is not null)
        {
            sorgu = sorgu.Where(c => c.GrupId == grupId);
        }

        if (aktif is not null)
        {
            sorgu = sorgu.Where(c => c.AktifMi == aktif);
        }

        var sonuc = await sorgu
            .OrderBy(c => c.Unvan)
            .Select(c => new CariOzeti(
                c.Id, c.Kod, c.Unvan, c.Tur, c.Grup != null ? c.Grup.Ad : null, c.ParaBirimi, c.Telefon, c.AktifMi,
                db.CariHareketler.Where(h => h.CariHesapId == c.Id).Sum(h => h.Borc - h.Alacak),
                db.CariHareketler.Where(h => h.CariHesapId == c.Id).Sum(h => h.BorcTl - h.AlacakTl),
                c.RiskLimiti))
            .SayfalaAsync(new SayfalamaIstegi(sayfa ?? 1, boyut ?? SayfalamaIstegi.VarsayilanBoyut), cancellationToken);

        return TypedResults.Ok(sonuc);
    }

    private static async Task<IResult> DetayAsync(Guid id, CariDbContext db, CancellationToken cancellationToken)
    {
        var detay = await DetayGetirAsync(db, id, cancellationToken);
        return detay is null ? CariHatalari.CariBulunamadi.Problem() : TypedResults.Ok(detay);
    }

    private static async Task<IResult> EkleAsync(
        CariKaydetIstegi istek, CariDbContext db, CariKodUretici kodUretici, IYetkiDenetimi yetki, CariKapamaServisi kapama,
        CancellationToken cancellationToken)
    {
        if (await GrupKontrolAsync(db, istek.GrupId, cancellationToken) is { } grupHatasi)
        {
            return grupHatasi.Problem();
        }

        var kod = string.IsNullOrWhiteSpace(istek.Kod) ? await kodUretici.SonrakiAsync(cancellationToken) : istek.Kod.Trim();
        if (await KodKullanimdaMiAsync(db, kod, haricId: null, cancellationToken))
        {
            return CariHatalari.KodKullaniliyor.Problem();
        }

        var cari = CariHesap.Olustur(kod, CariBilgileri.Istekten(istek));
        db.CariHesaplar.Add(cari);

        // Açılış, karttan ayrı girilen açılış dekontuyla aynı kayıttır (aynı yetki, dönem kilidi ve kapama); kartla tek işlemde yazılır.
        CariHareket? acilis = null;
        if (istek.AcilisBakiyesi is { } bakiye)
        {
            if (!await yetki.VarMiAsync(CariYetkileri.Hareket.Yetki(YetkiIslemleri.Ekle), cancellationToken))
            {
                return CariHatalari.AcilisYetkisiYok.Problem();
            }

            var kur = cari.ParaBirimi == ParaBirimleri.Tl ? 1m : bakiye.Kur;
            if (kur is null)
            {
                return CariHatalari.KurGerekli.Problem();
            }

            var hareket = CariHareket.Olustur(cari, CariHareketTuru.Acilis, bakiye.Yon, bakiye.Tarih, null, bakiye.Tutar, kur.Value, null, "Açılış bakiyesi");
            if (!hareket.BasariliMi)
            {
                return hareket.HttpSonucu();
            }

            acilis = hareket.Deger;
            db.CariHareketler.Add(acilis);
        }

        await db.SaveChangesAsync(cancellationToken);
        if (acilis is not null)
        {
            await kapama.KapatAsync(cari.Id, acilis.Id, null, cancellationToken);
        }

        return TypedResults.Created($"/api/cari/hesaplar/{cari.Id}", await DetayGetirAsync(db, cari.Id, cancellationToken));
    }

    private static async Task<IResult> GuncelleAsync(
        Guid id, CariKaydetIstegi istek, CariDbContext db, CariKodUretici kodUretici, CancellationToken cancellationToken)
    {
        var cari = await db.CariHesaplar
            .Include(c => c.Adresler).Include(c => c.IlgiliKisiler).Include(c => c.BankaHesaplari)
            .AsSplitQuery()
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (cari is null)
        {
            return CariHatalari.CariBulunamadi.Problem();
        }

        // İstemcinin gördüğü sürüm; arada başkası değiştirdiyse kayıt 409 ile reddedilir.
        db.Entry(cari).Property(c => c.Surum).OriginalValue = istek.Surum;

        if (await GrupKontrolAsync(db, istek.GrupId, cancellationToken) is { } grupHatasi)
        {
            return grupHatasi.Problem();
        }

        var kod = string.IsNullOrWhiteSpace(istek.Kod) ? cari.Kod : istek.Kod.Trim();
        if (kod != cari.Kod && await KodKullanimdaMiAsync(db, kod, cari.Id, cancellationToken))
        {
            return CariHatalari.KodKullaniliyor.Problem();
        }

        var hareketVarMi = await db.CariHareketler.AnyAsync(h => h.CariHesapId == id, cancellationToken);
        var sonuc = cari.Guncelle(kod, CariBilgileri.Istekten(istek), hareketVarMi);
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        // Yalnızca alt kayıtlar değişse de kart satırı yazılsın: sürüm (xmin) ilerler, eşzamanlı düzenleme denetimi alt kayıtları da kapsar.
        if (istek.Adresler is not null || istek.IlgiliKisiler is not null || istek.BankaHesaplari is not null)
        {
            db.Entry(cari).State = EntityState.Modified;
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(await DetayGetirAsync(db, id, cancellationToken));
    }

    private static async Task<IResult> DurumDegistirAsync(
        Guid id, CariDbContext db, Action<CariHesap> islem, CancellationToken cancellationToken)
    {
        var cari = await db.CariHesaplar.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (cari is null)
        {
            return CariHatalari.CariBulunamadi.Problem();
        }

        islem(cari);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> SilAsync(Guid id, CariDbContext db, CancellationToken cancellationToken)
    {
        var cari = await db.CariHesaplar.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (cari is null)
        {
            return CariHatalari.CariBulunamadi.Problem();
        }

        var sonuc = CariHesap.SilinebilirMi(await db.CariHareketler.AnyAsync(h => h.CariHesapId == id, cancellationToken));
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        db.CariHesaplar.Remove(cari);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    internal static Task<CariDetayi?> DetayGetirAsync(CariDbContext db, Guid id, CancellationToken cancellationToken) =>
        db.CariHesaplar.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CariDetayi(
                c.Id, c.Kod, c.Unvan, c.Tur, c.KisiTipi, c.VergiNo, c.VergiDairesi, c.Il, c.Ilce, c.Adres, c.Telefon,
                c.Eposta, c.ParaBirimi, c.VadeGun, c.RiskLimiti, c.GrupId, c.Grup != null ? c.Grup.Ad : null,
                c.Not, c.AktifMi, c.FarkliDovizdeIslem,
                db.CariHareketler.Where(h => h.CariHesapId == c.Id).Sum(h => h.Borc - h.Alacak),
                db.CariHareketler.Where(h => h.CariHesapId == c.Id).Sum(h => h.BorcTl - h.AlacakTl),
                db.CariHareketler.Any(h => h.CariHesapId == c.Id),
                c.Surum,
                c.VadeFarkiOrani, c.Ad, c.Soyad, c.Ulke, c.PostaKodu, c.CepTelefonu, c.Faks, c.WebSitesi,
                c.EFaturaMukellefiMi, c.EFaturaEtiketi, c.VarsayilanKdvIstisnaKodu, c.VarsayilanTevkifatKodu,
                c.Adresler.OrderBy(a => a.Sira)
                    .Select(a => new CariAdresBilgisi(a.Baslik, a.AdSoyad, a.Telefon, a.Ulke, a.Il, a.Ilce, a.Adres, a.PostaKodu)).ToList(),
                c.IlgiliKisiler.OrderBy(k => k.Sira)
                    .Select(k => new CariIlgiliKisiBilgisi(k.AdSoyad, k.Gorev, k.Telefon, k.Eposta)).ToList(),
                c.BankaHesaplari.OrderBy(h => h.Sira)
                    .Select(h => new CariBankaHesabiBilgisi(h.BankaAdi, h.Sube, h.HesapNo, h.Iban, h.ParaBirimi)).ToList()))
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);

    private static Task<bool> KodKullanimdaMiAsync(CariDbContext db, string kod, Guid? haricId, CancellationToken cancellationToken) =>
        db.CariHesaplar
            .IgnoreQueryFilters([ModulDbContext.SilinmeFiltresi])
            .AnyAsync(c => c.Kod == kod && c.Id != haricId, cancellationToken);

    private static async Task<Hata?> GrupKontrolAsync(CariDbContext db, Guid? grupId, CancellationToken cancellationToken) =>
        grupId is null || await db.CariGruplari.AnyAsync(g => g.Id == grupId, cancellationToken)
            ? null
            : CariHatalari.GrupBulunamadi;
}
