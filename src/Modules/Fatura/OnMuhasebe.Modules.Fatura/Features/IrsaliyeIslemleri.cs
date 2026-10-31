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
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Domain.Dogrulama;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Fatura.Features;

internal static class IrsaliyeIslemleri
{
    public const int AzamiSatirSayisi = 500;

    public static void Map(RouteGroupBuilder grup)
    {
        grup.MapGet("/irsaliyeler", ListeleAsync);
        grup.MapGet("/irsaliyeler/aktarim", AktarimAsync);
        grup.MapGet("/irsaliyeler/{id:guid}", GetirAsync);
        grup.MapPost("/irsaliyeler", OlusturAsync).Dogrula<IrsaliyeKaydetIstegi>();
        grup.MapPut("/irsaliyeler/{id:guid}", GuncelleAsync).Dogrula<IrsaliyeKaydetIstegi>();
        grup.MapPost("/irsaliyeler/{id:guid}/onayla", OnaylaAsync);
        grup.MapPost("/irsaliyeler/{id:guid}/iptal", IptalEtAsync);
        grup.MapDelete("/irsaliyeler/{id:guid}", SilAsync);
    }

    internal sealed class Dogrulayici : AbstractValidator<IrsaliyeKaydetIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.Tur).IsInEnum();
            RuleFor(x => x.SevkTarihi).GreaterThanOrEqualTo(x => x.Tarih)
                .WithMessage("Fiili sevk tarihi irsaliye düzenleme tarihinden önce olamaz.");
            RuleFor(x => x.Kur).GreaterThan(0).When(x => x.Kur is not null);
            RuleFor(x => x.Numara).MaximumLength(30);
            RuleFor(x => x.TeslimAdresi).MaximumLength(500);
            RuleFor(x => x.AracPlakasi).MaximumLength(20)
                .Must(p => PlakaGecerliMi(p!)).When(x => !string.IsNullOrWhiteSpace(x.AracPlakasi))
                .WithMessage("Araç plakası geçersiz (ör. 34 ABC 123: il kodu 01-81, 1-3 harf, 2-4 rakam).");
            RuleFor(x => x.SoforAdi).MaximumLength(100);
            RuleFor(x => x.SoforTckn).Must(t => VergiKimlik.TcknGecerliMi(t!.Trim()))
                .When(x => !string.IsNullOrWhiteSpace(x.SoforTckn)).WithMessage("Şoförün T.C. kimlik numarası geçersiz.");
            RuleFor(x => x.TasiyiciUnvani).MaximumLength(250);
            RuleFor(x => x.TasiyiciVkn).Must(v => VergiKimlik.VknGecerliMi(v!.Trim()) || VergiKimlik.TcknGecerliMi(v.Trim()))
                .When(x => !string.IsNullOrWhiteSpace(x.TasiyiciVkn)).WithMessage("Taşıyıcının vergi/T.C. kimlik numarası geçersiz.");
            RuleFor(x => x.Aciklama).MaximumLength(1000);
            RuleFor(x => x.Satirlar).NotEmpty().WithMessage("İrsaliyede en az bir satır olmalıdır.")
                .Must(s => s.Count <= AzamiSatirSayisi).WithMessage($"Bir irsaliyede en fazla {AzamiSatirSayisi} satır olabilir.");
            RuleForEach(x => x.Satirlar).ChildRules(s =>
            {
                s.RuleFor(x => x.Ad).MaximumLength(250);
                s.RuleFor(x => x.Birim).Must(Birimler.GecerliMi).WithMessage("Geçersiz birim.");
                s.RuleFor(x => x.Miktar).GreaterThan(0).LessThan(1_000_000_000m);
                s.RuleFor(x => x.BirimFiyat).GreaterThanOrEqualTo(0).LessThan(1_000_000_000_000m).When(x => x.BirimFiyat is not null);
            });
        }
    }

    // Türk plakası: il kodu (01-81) + 1-3 harf + 2-4 rakam; boşluklar yok sayılır.
    internal static bool PlakaGecerliMi(string plaka) =>
        System.Text.RegularExpressions.Regex.IsMatch(plaka.Replace(" ", "").ToUpperInvariant(), "^(0[1-9]|[1-7][0-9]|8[01])[A-Z]{1,3}[0-9]{2,4}$");

    private static async Task<IResult> ListeleAsync(
        FaturaDbContext db, IrsaliyeTuru? tur, IrsaliyeDurumu? durum, Guid? cariId, FaturalamaDurumu? faturalama, bool? acik,
        DateOnly? baslangic, DateOnly? bitis, string? arama, int? sayfa, int? boyut, CancellationToken cancellationToken)
    {
        var sorgu = db.Irsaliyeler.AsNoTracking();
        if (tur is not null)
        {
            sorgu = sorgu.Where(i => i.Tur == tur);
        }

        if (durum is not null)
        {
            sorgu = sorgu.Where(i => i.Durum == durum);
        }

        if (cariId is not null)
        {
            sorgu = sorgu.Where(i => i.CariHesapId == cariId);
        }

        // Açık irsaliye: onaylı, faturalanabilir ve tamamı faturalanmamış (VUK 231/5 takibi ve faturaya aktarım için).
        if (acik == true)
        {
            sorgu = sorgu.Where(i => i.Durum == IrsaliyeDurumu.Onayli && i.Tur != IrsaliyeTuru.DepoSevk
                                     && i.Satirlar.Any(s => s.FaturalananMiktar < s.Miktar));
        }

        if (faturalama is { } f)
        {
            sorgu = sorgu.Where(i => i.Tur != IrsaliyeTuru.DepoSevk);
            sorgu = f switch
            {
                FaturalamaDurumu.Faturalanmadi => sorgu.Where(i => i.Satirlar.All(s => s.FaturalananMiktar == 0)),
                FaturalamaDurumu.Faturalandi => sorgu.Where(i => i.Satirlar.All(s => s.FaturalananMiktar >= s.Miktar)),
                _ => sorgu.Where(i => i.Satirlar.Any(s => s.FaturalananMiktar > 0) && i.Satirlar.Any(s => s.FaturalananMiktar < s.Miktar))
            };
        }

        if (baslangic is not null)
        {
            sorgu = sorgu.Where(i => i.Tarih >= baslangic);
        }

        if (bitis is not null)
        {
            sorgu = sorgu.Where(i => i.Tarih <= bitis);
        }

        if (!string.IsNullOrWhiteSpace(arama))
        {
            var desen = AramaDeseni.Icerir(arama);
            sorgu = sorgu.Where(i => EF.Functions.ILike(i.CariUnvan, desen) || (i.Numara != null && EF.Functions.ILike(i.Numara, desen)));
        }

        var sonuc = await sorgu
            .OrderByDescending(i => i.Tarih).ThenByDescending(i => i.OlusturmaTarihi)
            .Select(i => new
            {
                i.Id, i.Tur, i.Durum, i.Numara, i.Tarih, i.SevkTarihi, i.CariHesapId, i.CariUnvan,
                SatirSayisi = i.Satirlar.Count,
                HicFaturalanmadi = i.Satirlar.All(s => s.FaturalananMiktar == 0),
                TamFaturalandi = i.Satirlar.All(s => s.FaturalananMiktar >= s.Miktar)
            })
            .SayfalaAsync(new SayfalamaIstegi(sayfa ?? 1, boyut ?? SayfalamaIstegi.VarsayilanBoyut), cancellationToken);

        var kayitlar = sonuc.Kayitlar.Select(i =>
        {
            FaturalamaDurumu? fd = IrsaliyeTurleri.FaturalanirMi(i.Tur)
                ? i.HicFaturalanmadi ? FaturalamaDurumu.Faturalanmadi : i.TamFaturalandi ? FaturalamaDurumu.Faturalandi : FaturalamaDurumu.KismenFaturalandi
                : null;
            return new IrsaliyeOzeti(i.Id, i.Tur, i.Durum, fd, i.Numara, i.Tarih, i.SevkTarihi, i.CariHesapId, i.CariUnvan, i.SatirSayisi,
                FaturaSonGunu(i.Durum, fd, i.SevkTarihi));
        }).ToList();

        return TypedResults.Ok(new SayfaliSonuc<IrsaliyeOzeti>(kayitlar, sonuc.Sayfa, sonuc.Boyut, sonuc.ToplamKayit));
    }

    // Faturaya aktarılabilecek satırlar: carinin, fatura türüne karşılık gelen onaylı irsaliyelerindeki faturalanmamış kalan.
    // Sevk sırasıyla gelir (en eski irsaliye önce faturalanmalı); bugüne göre 7 günü geçenler işaretlenir.
    private static async Task<IResult> AktarimAsync(
        FaturaDbContext db, TimeProvider zaman, Guid cariId, FaturaTuru faturaTuru, CancellationToken cancellationToken)
    {
        if (IrsaliyeTurleri.Tur(faturaTuru) is not { } tur)
        {
            return TypedResults.Ok(Array.Empty<AktarilacakIrsaliyeSatiri>());
        }

        var bugun = TurkiyeSaati.Bugun(zaman);
        var satirlar = await (
                from s in db.IrsaliyeSatirlari
                join i in db.Irsaliyeler on s.IrsaliyeId equals i.Id
                where i.CariHesapId == cariId && i.Tur == tur && i.Durum == IrsaliyeDurumu.Onayli && s.FaturalananMiktar < s.Miktar
                orderby i.SevkTarihi, i.Numara, s.Sira
                select new { i.Id, i.Numara, i.SevkTarihi, SatirId = s.Id, s.StokKartiId, s.StokKodu, s.Ad, s.DepoId, s.Birim,
                    s.Miktar, s.FaturalananMiktar, s.BirimFiyat, s.KdvOrani })
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(satirlar.Select(s => new AktarilacakIrsaliyeSatiri(
            s.Id, s.Numara, s.SevkTarihi, s.SatirId, s.StokKartiId, s.StokKodu, s.Ad, s.DepoId, s.Birim,
            IrsaliyeHesaplari.Kalan(s.Miktar, s.FaturalananMiktar), s.BirimFiyat, s.KdvOrani,
            IrsaliyeHesaplari.FaturaSuresiGectiMi(s.SevkTarihi, bugun))).ToList());
    }

    private static async Task<IResult> GetirAsync(Guid id, FaturaDbContext db, CancellationToken cancellationToken)
    {
        var irsaliye = await db.Irsaliyeler.AsNoTracking().Include(i => i.Satirlar).SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
        return irsaliye is null
            ? IrsaliyeHatalari.IrsaliyeBulunamadi.Problem()
            : TypedResults.Ok(Detay(irsaliye, await BagliFaturalarAsync(db, irsaliye, cancellationToken)));
    }

    private static async Task<IResult> OlusturAsync(IrsaliyeKaydetIstegi istek, IrsaliyeKayitServisi servis, FaturaDbContext db, CancellationToken cancellationToken)
    {
        var sonuc = await servis.OlusturAsync(istek, cancellationToken);
        return sonuc.BasariliMi
            ? TypedResults.Created($"/api/fatura/irsaliyeler/{sonuc.Deger.Irsaliye.Id}", await CevapAsync(sonuc.Deger, db, cancellationToken))
            : sonuc.HttpSonucu();
    }

    private static async Task<IResult> GuncelleAsync(
        Guid id, IrsaliyeKaydetIstegi istek, IrsaliyeKayitServisi servis, FaturaDbContext db, CancellationToken cancellationToken) =>
        await IslemSonucuAsync(await servis.GuncelleAsync(id, istek, cancellationToken), db, cancellationToken);

    private static async Task<IResult> OnaylaAsync(Guid id, IrsaliyeKayitServisi servis, FaturaDbContext db, CancellationToken cancellationToken) =>
        await IslemSonucuAsync(await servis.OnaylaAsync(id, cancellationToken), db, cancellationToken);

    private static async Task<IResult> IptalEtAsync(Guid id, IrsaliyeKayitServisi servis, FaturaDbContext db, CancellationToken cancellationToken) =>
        await IslemSonucuAsync(await servis.IptalEtAsync(id, cancellationToken), db, cancellationToken);

    private static async Task<IResult> SilAsync(Guid id, FaturaDbContext db, CancellationToken cancellationToken)
    {
        var irsaliye = await db.Irsaliyeler.SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (irsaliye is null)
        {
            return IrsaliyeHatalari.IrsaliyeBulunamadi.Problem();
        }

        if (irsaliye.Durum != IrsaliyeDurumu.Taslak)
        {
            return IrsaliyeHatalari.YalnizcaTaslakSilinir.Problem();
        }

        db.Irsaliyeler.Remove(irsaliye);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> IslemSonucuAsync(Sonuc<IrsaliyeIslemSonucu> sonuc, FaturaDbContext db, CancellationToken cancellationToken) =>
        sonuc.BasariliMi ? TypedResults.Ok(await CevapAsync(sonuc.Deger, db, cancellationToken)) : sonuc.HttpSonucu();

    private static async Task<IrsaliyeCevabi> CevapAsync(IrsaliyeIslemSonucu sonuc, FaturaDbContext db, CancellationToken cancellationToken) =>
        new(Detay(sonuc.Irsaliye, await BagliFaturalarAsync(db, sonuc.Irsaliye, cancellationToken)), sonuc.Uyari);

    // İrsaliye satırlarına bağlanan (iptal edilmemiş) faturalar; bağ fatura satırında tutulur.
    internal static async Task<IReadOnlyList<BagliBelge>> BagliFaturalarAsync(FaturaDbContext db, Irsaliye irsaliye, CancellationToken cancellationToken)
    {
        var satirIdleri = irsaliye.Satirlar.Select(s => s.Id).ToList();
        return await db.Faturalar.AsNoTracking()
            .Where(f => f.Durum != FaturaDurumu.Iptal && f.Satirlar.Any(s => s.IrsaliyeSatiriId != null && satirIdleri.Contains(s.IrsaliyeSatiriId.Value)))
            .OrderBy(f => f.Tarih)
            .Select(f => new BagliBelge(f.Id, f.Numara, f.Tarih))
            .ToListAsync(cancellationToken);
    }

    private static DateOnly? FaturaSonGunu(IrsaliyeDurumu durum, FaturalamaDurumu? faturalama, DateOnly sevkTarihi) =>
        durum == IrsaliyeDurumu.Onayli && faturalama is FaturalamaDurumu.Faturalanmadi or FaturalamaDurumu.KismenFaturalandi
            ? IrsaliyeHesaplari.FaturaSonGunu(sevkTarihi)
            : null;

    internal static IrsaliyeDetayi Detay(Irsaliye i, IReadOnlyList<BagliBelge> faturalar) => new(
        i.Id, i.Tur, i.Durum, i.FaturalamaDurumu, i.Numara, i.Tarih, i.SevkTarihi, i.SevkSaati,
        i.CariHesapId, i.CariKodu, i.CariUnvan, i.VergiNo, i.VergiDairesi, i.Il, i.Ilce, i.Adres, i.ParaBirimi, i.Kur,
        i.HedefDepoId, i.TeslimAdresi, i.AracPlakasi, i.SoforAdi, i.SoforTckn, i.TasiyiciUnvani, i.TasiyiciVkn, i.Aciklama,
        i.Satirlar.OrderBy(s => s.Sira).Select(s => new IrsaliyeSatiriDetayi(
            s.Id, s.Sira, s.StokKartiId, s.StokKodu, s.Ad, s.DepoId, s.Birim, s.Miktar, s.BirimFiyat, s.KdvOrani,
            s.FaturalananMiktar, s.KalanMiktar)).ToList(),
        faturalar,
        FaturaSonGunu(i.Durum, i.FaturalamaDurumu, i.SevkTarihi),
        i.OnayTarihi, i.IptalTarihi, i.Surum);
}
