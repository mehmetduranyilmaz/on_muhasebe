using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Modules.Fatura.Domain;
using OnMuhasebe.Modules.Fatura.Infrastructure;
using OnMuhasebe.Shared.Contracts.Birimler;
using OnMuhasebe.Shared.Contracts.Sayfalama;
using OnMuhasebe.Shared.Contracts.Vergi;
using OnMuhasebe.Shared.Domain.Dogrulama;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Fatura.Features;

internal static class FaturaIslemleri
{
    public const int AzamiSatirSayisi = 500;

    public static void Map(RouteGroupBuilder grup)
    {
        grup.MapGet("/faturalar", ListeleAsync);
        grup.MapGet("/faturalar/{id:guid}", GetirAsync);
        grup.MapPost("/faturalar", OlusturAsync).Dogrula<FaturaKaydetIstegi>();
        grup.MapPut("/faturalar/{id:guid}", GuncelleAsync).Dogrula<FaturaKaydetIstegi>();
        grup.MapPost("/faturalar/{id:guid}/onayla", OnaylaAsync);
        grup.MapPost("/faturalar/{id:guid}/iptal", IptalEtAsync);
        grup.MapDelete("/faturalar/{id:guid}", SilAsync);
    }

    internal sealed class Dogrulayici : AbstractValidator<FaturaKaydetIstegi>
    {
        private static readonly AlanKontrolu<FaturaKaydetIstegi>[] _zorunluKontroller =
        [
            new(FaturaZorunluAlanlari.VadeTarihi, nameof(FaturaKaydetIstegi.VadeTarihi), x => x.VadeTarihi is not null),
            new(FaturaZorunluAlanlari.Aciklama, nameof(FaturaKaydetIstegi.Aciklama), x => ZorunluAlanKurali.Dolu(x.Aciklama))
        ];

        public Dogrulayici(IZorunluAlanSorgusu zorunlu)
        {
            this.ZorunluAlanlar(zorunlu, FaturaZorunluAlanlari.Tumu, _zorunluKontroller);
            RuleFor(x => x.Tur).IsInEnum();
            RuleFor(x => x.SatisSekli).IsInEnum().When(x => x.SatisSekli is not null);
            RuleFor(x => x.GenelIskontoTuru).IsInEnum();
            RuleFor(x => x.GenelIskonto).GreaterThanOrEqualTo(0).LessThan(1_000_000_000_000m);
            RuleFor(x => x.GenelIskonto).LessThanOrEqualTo(100).When(x => x.GenelIskontoTuru == GenelIskontoTuru.Yuzde)
                .WithMessage("Genel iskonto yüzdesi 100'ü aşamaz.");
            RuleFor(x => x.Kur).GreaterThan(0).When(x => x.Kur is not null);
            RuleFor(x => x.VadeTarihi).GreaterThanOrEqualTo(x => x.Tarih).When(x => x.VadeTarihi is not null)
                .WithMessage("Vade tarihi fatura tarihinden önce olamaz.");
            RuleFor(x => x.Numara).MaximumLength(30);
            RuleFor(x => x.Aciklama).MaximumLength(1000);
            RuleFor(x => x.NihaiTuketici!.AdSoyad).MaximumLength(250).When(x => x.NihaiTuketici is not null);
            RuleFor(x => x.NihaiTuketici!.Tckn)
                .Must(t => t!.Trim() == FaturaBelgesi.NihaiTuketiciTckn || VergiKimlik.TcknGecerliMi(t.Trim()))
                .When(x => !string.IsNullOrWhiteSpace(x.NihaiTuketici?.Tckn))
                .WithMessage("Geçersiz T.C. kimlik numarası.");
            RuleFor(x => x.Satirlar).NotEmpty().WithMessage("Faturada en az bir satır olmalıdır.")
                .Must(s => s.Count <= AzamiSatirSayisi).WithMessage($"Bir faturada en fazla {AzamiSatirSayisi} satır olabilir.");
            RuleForEach(x => x.Satirlar).ChildRules(s =>
            {
                s.RuleFor(x => x.Ad).MaximumLength(250);
                s.RuleFor(x => x.Birim).Must(Birimler.GecerliMi).WithMessage("Geçersiz birim.");
                s.RuleFor(x => x.Miktar).GreaterThan(0).LessThan(1_000_000_000m);
                s.RuleFor(x => x.BirimFiyat).GreaterThanOrEqualTo(0).LessThan(1_000_000_000_000m);
                s.RuleFor(x => x.KdvOrani).Must(KdvOranlari.GecerliMi).WithMessage("Geçersiz KDV oranı.");
                s.RuleFor(x => x.Iskonto1).InclusiveBetween(0, 100);
                s.RuleFor(x => x.Iskonto2).InclusiveBetween(0, 100);
                s.RuleFor(x => x.Iskonto3).InclusiveBetween(0, 100);
            });
        }
    }

    private static async Task<IResult> ListeleAsync(
        FaturaDbContext db, ICariSorgusu cariSorgusu, FaturaTuru? tur, FaturaDurumu? durum, Guid? cariId, DateOnly? baslangic, DateOnly? bitis,
        string? arama, int? sayfa, int? boyut, CancellationToken cancellationToken)
    {
        var sorgu = db.Faturalar.AsNoTracking();
        if (tur is not null)
        {
            sorgu = sorgu.Where(f => f.Tur == tur);
        }

        if (durum is not null)
        {
            sorgu = sorgu.Where(f => f.Durum == durum);
        }

        if (cariId is not null)
        {
            sorgu = sorgu.Where(f => f.CariHesapId == cariId);
        }

        if (baslangic is not null)
        {
            sorgu = sorgu.Where(f => f.Tarih >= baslangic);
        }

        if (bitis is not null)
        {
            sorgu = sorgu.Where(f => f.Tarih <= bitis);
        }

        if (!string.IsNullOrWhiteSpace(arama))
        {
            var desen = $"%{arama.Trim()}%";
            sorgu = sorgu.Where(f => EF.Functions.ILike(f.CariUnvan, desen) || (f.Numara != null && EF.Functions.ILike(f.Numara, desen)));
        }

        var sonuc = await sorgu
            .OrderByDescending(f => f.Tarih).ThenByDescending(f => f.OlusturmaTarihi)
            .Select(f => new FaturaOzeti(f.Id, f.Tur, f.SatisSekli, f.Durum, f.Numara, f.Tarih, f.VadeTarihi, f.CariHesapId, f.CariUnvan,
                f.ParaBirimi, f.GenelToplam, f.GenelToplamTl, null))
            .SayfalaAsync(new SayfalamaIstegi(sayfa ?? 1, boyut ?? SayfalamaIstegi.VarsayilanBoyut), cancellationToken);

        return TypedResults.Ok(sonuc with { Kayitlar = await KalanlariEkleAsync(sonuc.Kayitlar, cariSorgusu, cancellationToken) });
    }

    private static async Task<IResult> GetirAsync(Guid id, FaturaDbContext db, ICariSorgusu cariSorgusu, CancellationToken cancellationToken)
    {
        var fatura = await db.Faturalar.AsNoTracking().Include(f => f.Satirlar).SingleOrDefaultAsync(f => f.Id == id, cancellationToken);
        return fatura is null
            ? FaturaHatalari.FaturaBulunamadi.Problem()
            : TypedResults.Ok(Detay(fatura, await KalanAsync(fatura, cariSorgusu, cancellationToken)));
    }

    private static async Task<IResult> OlusturAsync(
        FaturaKaydetIstegi istek, FaturaKayitServisi servis, ICariSorgusu cariSorgusu, CancellationToken cancellationToken)
    {
        var sonuc = await servis.OlusturAsync(istek, cancellationToken);
        return sonuc.BasariliMi
            ? TypedResults.Created($"/api/fatura/faturalar/{sonuc.Deger.Fatura.Id}", await CevapAsync(sonuc.Deger, cariSorgusu, cancellationToken))
            : sonuc.HttpSonucu();
    }

    private static async Task<IResult> GuncelleAsync(
        Guid id, FaturaKaydetIstegi istek, FaturaKayitServisi servis, ICariSorgusu cariSorgusu, CancellationToken cancellationToken) =>
        await IslemSonucuAsync(await servis.GuncelleAsync(id, istek, cancellationToken), cariSorgusu, cancellationToken);

    private static async Task<IResult> OnaylaAsync(Guid id, FaturaKayitServisi servis, ICariSorgusu cariSorgusu, CancellationToken cancellationToken) =>
        await IslemSonucuAsync(await servis.OnaylaAsync(id, cancellationToken), cariSorgusu, cancellationToken);

    private static async Task<IResult> IptalEtAsync(Guid id, FaturaKayitServisi servis, ICariSorgusu cariSorgusu, CancellationToken cancellationToken) =>
        await IslemSonucuAsync(await servis.IptalEtAsync(id, cancellationToken), cariSorgusu, cancellationToken);

    private static async Task<IResult> SilAsync(Guid id, FaturaDbContext db, CancellationToken cancellationToken)
    {
        var fatura = await db.Faturalar.SingleOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (fatura is null)
        {
            return FaturaHatalari.FaturaBulunamadi.Problem();
        }

        if (fatura.Durum != FaturaDurumu.Taslak)
        {
            return FaturaHatalari.YalnizcaTaslakSilinir.Problem();
        }

        db.Faturalar.Remove(fatura);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> IslemSonucuAsync(Sonuc<FaturaIslemSonucu> sonuc, ICariSorgusu cariSorgusu, CancellationToken cancellationToken) =>
        sonuc.BasariliMi ? TypedResults.Ok(await CevapAsync(sonuc.Deger, cariSorgusu, cancellationToken)) : sonuc.HttpSonucu();

    private static async Task<FaturaCevabi> CevapAsync(FaturaIslemSonucu sonuc, ICariSorgusu cariSorgusu, CancellationToken cancellationToken) =>
        new(Detay(sonuc.Fatura, await KalanAsync(sonuc.Fatura, cariSorgusu, cancellationToken)), sonuc.Uyari);

    // Kalan: faturanın kapanmamış tutarı (cari dövizinde = fatura dövizinde). Taslak, iptal ve carisiz faturada yoktur.
    private static async Task<decimal?> KalanAsync(FaturaBelgesi f, ICariSorgusu cariSorgusu, CancellationToken cancellationToken) =>
        f.Durum == FaturaDurumu.Onayli && f.CariHesapId is not null
            ? (await cariSorgusu.BelgeKalanlariAsync([f.Id], cancellationToken)).GetValueOrDefault(f.Id)
            : null;

    internal static async Task<List<FaturaOzeti>> KalanlariEkleAsync(
        IReadOnlyList<FaturaOzeti> faturalar, ICariSorgusu cariSorgusu, CancellationToken cancellationToken)
    {
        var idler = faturalar.Where(f => f.Durum == FaturaDurumu.Onayli && f.CariHesapId is not null).Select(f => f.Id).ToList();
        IReadOnlyDictionary<Guid, decimal> kalanlar = idler.Count == 0 ? new Dictionary<Guid, decimal>() : await cariSorgusu.BelgeKalanlariAsync(idler, cancellationToken);
        return faturalar.Select(f => idler.Contains(f.Id) ? f with { Kalan = kalanlar.GetValueOrDefault(f.Id) } : f).ToList();
    }

    internal static FaturaDetayi Detay(FaturaBelgesi f, decimal? kalan)
    {
        var satirlar = f.Satirlar.OrderBy(s => s.Sira).ToList();
        var dagilim = satirlar
            .GroupBy(s => s.KdvOrani)
            .OrderBy(g => g.Key)
            .Select(g => new KdvDagilimi(g.Key, g.Sum(s => s.Matrah), g.Sum(s => s.KdvTutari)))
            .ToList();

        return new FaturaDetayi(
            f.Id, f.Tur, f.SatisSekli, f.Durum, f.Numara, f.Tarih, f.VadeTarihi,
            f.CariHesapId, f.CariKodu, f.CariUnvan, f.VergiNo, f.VergiDairesi, f.Il, f.Ilce, f.Adres,
            f.ParaBirimi, f.Kur, f.GenelIskontoTuru, f.GenelIskonto, f.Aciklama,
            f.BrutToplam, f.SatirIskontoToplami, f.GenelIskontoTutari, f.Matrah, f.KdvToplami, f.GenelToplam, f.GenelToplamTl,
            dagilim,
            satirlar.Select(s => new FaturaSatiriDetayi(
                s.Id, s.Sira, s.StokKartiId, s.StokKodu, s.Ad, s.StokluMu, s.DepoId, s.Birim, s.Miktar, s.BirimFiyat, s.KdvDahilMi,
                s.Iskonto1, s.Iskonto2, s.Iskonto3, s.KdvOrani, s.BrutTutar, s.IskontoTutari, s.GenelIskontoPayi, s.Matrah,
                s.KdvTutari, s.Toplam)).ToList(),
            f.Ettn, f.OnayTarihi, f.IptalTarihi, f.Surum, f.PesinHesapId, kalan);
    }
}
