using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
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
        public Dogrulayici()
        {
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
        FaturaDbContext db, FaturaTuru? tur, FaturaDurumu? durum, Guid? cariId, DateOnly? baslangic, DateOnly? bitis, string? arama,
        int? sayfa, int? boyut, CancellationToken cancellationToken)
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
                f.ParaBirimi, f.GenelToplam, f.GenelToplamTl))
            .SayfalaAsync(new SayfalamaIstegi(sayfa ?? 1, boyut ?? SayfalamaIstegi.VarsayilanBoyut), cancellationToken);

        return TypedResults.Ok(sonuc);
    }

    private static async Task<IResult> GetirAsync(Guid id, FaturaDbContext db, CancellationToken cancellationToken)
    {
        var fatura = await db.Faturalar.AsNoTracking().Include(f => f.Satirlar).SingleOrDefaultAsync(f => f.Id == id, cancellationToken);
        return fatura is null ? FaturaHatalari.FaturaBulunamadi.Problem() : TypedResults.Ok(Detay(fatura));
    }

    private static async Task<IResult> OlusturAsync(FaturaKaydetIstegi istek, FaturaKayitServisi servis, CancellationToken cancellationToken)
    {
        var sonuc = await servis.OlusturAsync(istek, cancellationToken);
        return sonuc.BasariliMi
            ? TypedResults.Created($"/api/fatura/faturalar/{sonuc.Deger.Fatura.Id}", Cevap(sonuc.Deger))
            : sonuc.HttpSonucu();
    }

    private static async Task<IResult> GuncelleAsync(
        Guid id, FaturaKaydetIstegi istek, FaturaKayitServisi servis, CancellationToken cancellationToken) =>
        IslemSonucu(await servis.GuncelleAsync(id, istek, cancellationToken));

    private static async Task<IResult> OnaylaAsync(Guid id, FaturaKayitServisi servis, CancellationToken cancellationToken) =>
        IslemSonucu(await servis.OnaylaAsync(id, cancellationToken));

    private static async Task<IResult> IptalEtAsync(Guid id, FaturaKayitServisi servis, CancellationToken cancellationToken) =>
        IslemSonucu(await servis.IptalEtAsync(id, cancellationToken));

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

    private static IResult IslemSonucu(Sonuc<FaturaIslemSonucu> sonuc) =>
        sonuc.BasariliMi ? TypedResults.Ok(Cevap(sonuc.Deger)) : sonuc.HttpSonucu();

    private static FaturaCevabi Cevap(FaturaIslemSonucu sonuc) => new(Detay(sonuc.Fatura), sonuc.Uyari);

    internal static FaturaDetayi Detay(FaturaBelgesi f)
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
            f.Ettn, f.OnayTarihi, f.IptalTarihi, f.Surum);
    }
}
