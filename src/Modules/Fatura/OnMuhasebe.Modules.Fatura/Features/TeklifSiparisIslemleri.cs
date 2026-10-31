using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Modules.Fatura.Domain;
using OnMuhasebe.Modules.Fatura.Infrastructure;
using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Contracts.Birimler;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Shared.Contracts.Sayfalama;
using OnMuhasebe.Shared.Contracts.Vergi;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Fatura.Features;

internal static class TeklifSiparisIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var g = grup.MapGroup("/teklif-siparis").Yetki(FaturaYetkileri.Teklif);
        g.MapGet("", ListeleAsync);
        g.MapGet("/{id:guid}", GetirAsync);
        g.MapGet("/aktarim", AktarimAsync);
        g.MapGet("/bekleyen", BekleyenAsync);
        g.MapPost("", OlusturAsync).Dogrula<TeklifSiparisKaydetIstegi>();
        g.MapPut("/{id:guid}", GuncelleAsync).Dogrula<TeklifSiparisKaydetIstegi>();
        g.MapPost("/{id:guid}/onayla", OnaylaAsync).Islem(YetkiIslemleri.Onayla);
        g.MapPost("/{id:guid}/yanit", YanitAsync).Islem(YetkiIslemleri.Duzenle);
        g.MapPost("/{id:guid}/siparise-donustur", SipariseDonusturAsync);
        g.MapPost("/{id:guid}/kapat", (Guid id, TeklifSiparisServisi s, FaturaDbContext db, TimeProvider z, CancellationToken ct) =>
            IslemAsync(s.DurumAsync(id, b => b.Kapat(), ct), db, z, ct)).Islem(YetkiIslemleri.Duzenle);
        g.MapPost("/{id:guid}/iptal", (Guid id, TeklifSiparisServisi s, FaturaDbContext db, TimeProvider z, CancellationToken ct) =>
            IslemAsync(s.DurumAsync(id, b => b.IptalEt(), ct), db, z, ct)).Islem(YetkiIslemleri.Iptal);
        g.MapDelete("/{id:guid}", SilAsync);
    }

    internal sealed class Dogrulayici : AbstractValidator<TeklifSiparisKaydetIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.Tur).IsInEnum();
            RuleFor(x => x.AdayUnvan).MaximumLength(250);
            RuleFor(x => x.ParaBirimi).Must(p => p is null || ParaBirimleri.GecerliMi(p)).WithMessage("Geçersiz para birimi.");
            RuleFor(x => x.Kur).GreaterThan(0).When(x => x.Kur is not null);
            RuleFor(x => x.GenelIskontoTuru).IsInEnum();
            RuleFor(x => x.GenelIskonto).GreaterThanOrEqualTo(0);
            RuleFor(x => x.GenelIskonto).LessThanOrEqualTo(100).When(x => x.GenelIskontoTuru == GenelIskontoTuru.Yuzde);
            RuleFor(x => x.OdemeKosulu).MaximumLength(500);
            RuleFor(x => x.TeslimKosulu).MaximumLength(500);
            RuleFor(x => x.Aciklama).MaximumLength(1000);
            RuleFor(x => x.Satirlar).NotEmpty().Must(s => s.Count <= FaturaIslemleri.AzamiSatirSayisi);
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
        FaturaDbContext db, TimeProvider zaman, TeklifSiparisTuru? tur, bool? teklif, TeklifSiparisDurumu? durum, Guid? cariId, bool? acik, string? arama,
        int? sayfa, int? boyut, CancellationToken cancellationToken)
    {
        var sorgu = db.TeklifSiparisler.AsNoTracking();
        if (teklif is { } t)
        {
            sorgu = t ? sorgu.Where(b => b.Tur == TeklifSiparisTuru.SatisTeklifi) : sorgu.Where(b => b.Tur != TeklifSiparisTuru.SatisTeklifi);
        }

        if (tur is not null)
        {
            sorgu = sorgu.Where(b => b.Tur == tur);
        }

        if (durum is not null)
        {
            sorgu = sorgu.Where(b => b.Durum == durum);
        }

        if (cariId is not null)
        {
            sorgu = sorgu.Where(b => b.CariHesapId == cariId);
        }

        // Açık sipariş: onaylı, kapatılmamış ve tamamı karşılanmamış (irsaliye/faturaya aktarılabilir).
        if (acik == true)
        {
            sorgu = sorgu.Where(b => b.Durum == TeklifSiparisDurumu.Acik && b.Satirlar.Any(s => s.KarsilananMiktar < s.Miktar));
        }

        if (!string.IsNullOrWhiteSpace(arama))
        {
            var desen = AramaDeseni.Icerir(arama);
            sorgu = sorgu.Where(b => EF.Functions.ILike(b.CariUnvan, desen) || (b.Numara != null && EF.Functions.ILike(b.Numara, desen)));
        }

        var bugun = TurkiyeSaati.Bugun(zaman);
        var sonuc = await sorgu.OrderByDescending(b => b.Tarih).ThenByDescending(b => b.OlusturmaTarihi)
            .Select(b => new
            {
                b.Id, b.Tur, b.Durum, b.Numara, b.Tarih, b.GecerlilikTarihi, b.TerminTarihi, b.CariHesapId, b.CariUnvan, b.ParaBirimi, b.GenelToplam,
                Hic = b.Satirlar.All(s => s.KarsilananMiktar == 0), Tam = b.Satirlar.All(s => s.KarsilananMiktar >= s.Miktar)
            })
            .SayfalaAsync(new SayfalamaIstegi(sayfa ?? 1, boyut ?? SayfalamaIstegi.VarsayilanBoyut), cancellationToken);

        return TypedResults.Ok(new SayfaliSonuc<TeklifSiparisOzeti>(sonuc.Kayitlar.Select(b => new TeklifSiparisOzeti(
                b.Id, b.Tur, b.Durum,
                TeklifSiparisTurleri.TeklifMi(b.Tur) ? null : b.Hic ? KarsilamaDurumu.Bekliyor : b.Tam ? KarsilamaDurumu.Tamamlandi : KarsilamaDurumu.Kismen,
                b.Numara, b.Tarih, b.GecerlilikTarihi, b.TerminTarihi, b.CariHesapId, b.CariUnvan, b.ParaBirimi, b.GenelToplam,
                TeklifSiparisTurleri.SuresiDolduMu(b.Tur, b.Durum, b.GecerlilikTarihi, bugun))).ToList(),
            sonuc.Sayfa, sonuc.Boyut, sonuc.ToplamKayit));
    }

    private static async Task<IResult> GetirAsync(Guid id, FaturaDbContext db, TimeProvider zaman, CancellationToken cancellationToken) =>
        await db.TeklifSiparisler.AsNoTracking().Include(b => b.Satirlar).SingleOrDefaultAsync(b => b.Id == id, cancellationToken) is { } belge
            ? TypedResults.Ok(await DetayAsync(db, belge, zaman, cancellationToken))
            : TeklifSiparisHatalari.Bulunamadi.Problem();

    // Carinin açık siparişlerindeki karşılanmamış satırlar, termin sırasıyla. İrsaliyeye yalnızca ürün satırları aktarılır.
    private static async Task<IResult> AktarimAsync(Guid cariId, TeklifSiparisTuru tur, bool? yalnizcaUrun, FaturaDbContext db, CancellationToken cancellationToken)
    {
        if (TeklifSiparisTurleri.TeklifMi(tur))
        {
            return TypedResults.Ok(Array.Empty<AktarilacakSiparisSatiri>());
        }

        var sorgu = from s in db.TeklifSiparisSatirlari.AsNoTracking()
                    join b in db.TeklifSiparisler on s.TeklifSiparisId equals b.Id
                    where b.CariHesapId == cariId && b.Tur == tur && b.Durum == TeklifSiparisDurumu.Acik && s.KarsilananMiktar < s.Miktar
                    select new { b.Numara, b.Tarih, b.TerminTarihi, b.ParaBirimi, Satir = s };
        if (yalnizcaUrun == true)
        {
            sorgu = sorgu.Where(x => x.Satir.StokluMu);
        }

        var liste = await sorgu.OrderBy(x => x.TerminTarihi ?? x.Tarih).ThenBy(x => x.Numara).ThenBy(x => x.Satir.Sira).ToListAsync(cancellationToken);
        return TypedResults.Ok(liste.Select(x => new AktarilacakSiparisSatiri(
            x.Satir.TeklifSiparisId, x.Numara, x.TerminTarihi, x.Satir.Id, x.Satir.StokKartiId, x.Satir.StokKodu, x.Satir.Ad, x.Satir.StokluMu,
            x.Satir.DepoId, x.Satir.Birim, x.Satir.KalanMiktar, x.ParaBirimi, x.Satir.BirimFiyat, x.Satir.KdvDahilMi, x.Satir.KdvOrani,
            x.Satir.Iskonto1, x.Satir.Iskonto2, x.Satir.Iskonto3)).ToList());
    }

    // Kartların açık siparişlerdeki karşılanmamış miktarı, temel birime çevrilmiş olarak.
    private static async Task<IResult> BekleyenAsync(Guid[] stokKartiId, FaturaDbContext db, IStokSorgusu stokSorgusu, CancellationToken cancellationToken)
    {
        var idler = stokKartiId.Distinct().Take(500).ToList();
        if (idler.Count == 0)
        {
            return TypedResults.Ok(Array.Empty<SiparisBekleyenMiktari>());
        }

        var satirlar = await (
                from s in db.TeklifSiparisSatirlari
                join b in db.TeklifSiparisler on s.TeklifSiparisId equals b.Id
                where b.Durum == TeklifSiparisDurumu.Acik && b.Tur != TeklifSiparisTuru.SatisTeklifi
                      && s.StokKartiId != null && idler.Contains(s.StokKartiId.Value) && s.KarsilananMiktar < s.Miktar
                select new { KartId = s.StokKartiId!.Value, b.Tur, s.Birim, Kalan = s.Miktar - s.KarsilananMiktar })
            .ToListAsync(cancellationToken);
        var kartlar = (await stokSorgusu.KartlariGetirAsync(idler, cancellationToken)).ToDictionary(k => k.Id);

        decimal Toplam(Guid kartId, TeklifSiparisTuru tur) => satirlar
            .Where(s => s.KartId == kartId && s.Tur == tur)
            .Sum(s => s.Kalan * (kartlar.TryGetValue(kartId, out var k) ? k.Katsayi(s.Birim) : 1m));

        return TypedResults.Ok(idler.Select(id => new SiparisBekleyenMiktari(
            id, Toplam(id, TeklifSiparisTuru.SatisSiparisi), Toplam(id, TeklifSiparisTuru.SatinalmaSiparisi))).ToList());
    }

    private static async Task<IResult> OlusturAsync(
        TeklifSiparisKaydetIstegi istek, TeklifSiparisServisi servis, FaturaDbContext db, TimeProvider zaman, CancellationToken cancellationToken)
    {
        var sonuc = await servis.OlusturAsync(istek, cancellationToken);
        return sonuc.BasariliMi
            ? TypedResults.Created($"/api/fatura/teklif-siparis/{sonuc.Deger.Id}", await DetayAsync(db, sonuc.Deger, zaman, cancellationToken))
            : sonuc.HttpSonucu();
    }

    private static Task<IResult> GuncelleAsync(
        Guid id, TeklifSiparisKaydetIstegi istek, TeklifSiparisServisi servis, FaturaDbContext db, TimeProvider zaman, CancellationToken cancellationToken) =>
        IslemAsync(servis.GuncelleAsync(id, istek, cancellationToken), db, zaman, cancellationToken);

    private static Task<IResult> OnaylaAsync(Guid id, TeklifSiparisServisi servis, FaturaDbContext db, TimeProvider zaman, CancellationToken cancellationToken) =>
        IslemAsync(servis.OnaylaAsync(id, cancellationToken), db, zaman, cancellationToken);

    private static Task<IResult> YanitAsync(
        Guid id, TeklifYanitiIstegi istek, TeklifSiparisServisi servis, FaturaDbContext db, TimeProvider zaman, CancellationToken cancellationToken) =>
        IslemAsync(servis.DurumAsync(id, b => b.Yanitla(istek.KabulMu, istek.Neden), cancellationToken), db, zaman, cancellationToken);

    private static Task<IResult> SipariseDonusturAsync(
        Guid id, Guid? cariId, TeklifSiparisServisi servis, FaturaDbContext db, TimeProvider zaman, CancellationToken cancellationToken) =>
        IslemAsync(servis.SipariseDonusturAsync(id, cariId, cancellationToken), db, zaman, cancellationToken);

    private static async Task<IResult> SilAsync(Guid id, FaturaDbContext db, CancellationToken cancellationToken)
    {
        var belge = await db.TeklifSiparisler.SingleOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (belge is null)
        {
            return TeklifSiparisHatalari.Bulunamadi.Problem();
        }

        if (belge.Durum != TeklifSiparisDurumu.Taslak)
        {
            return TeklifSiparisHatalari.YalnizcaTaslakSilinir.Problem();
        }

        db.TeklifSiparisler.Remove(belge);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> IslemAsync(Task<Sonuc<TeklifSiparis>> islem, FaturaDbContext db, TimeProvider zaman, CancellationToken cancellationToken)
    {
        var sonuc = await islem;
        return sonuc.BasariliMi ? TypedResults.Ok(await DetayAsync(db, sonuc.Deger, zaman, cancellationToken)) : sonuc.HttpSonucu();
    }

    internal static async Task<TeklifSiparisDetayi> DetayAsync(FaturaDbContext db, TeklifSiparis b, TimeProvider zaman, CancellationToken cancellationToken)
    {
        var siparisler = b.TeklifMi
            ? await db.TeklifSiparisler.AsNoTracking().Where(s => s.KaynakTeklifId == b.Id && s.Durum != TeklifSiparisDurumu.Iptal)
                .Select(s => new BagliBelge(s.Id, s.Numara, s.Tarih)).ToListAsync(cancellationToken)
            : [];
        var satirlar = b.Satirlar.OrderBy(s => s.Sira).ToList();

        // Siparişi karşılayan belgeler: bağlı irsaliyeler, doğrudan bağlı faturalar ve o irsaliyeleri faturalayan faturalar.
        List<BagliBelge> irsaliyeler = [], faturalar = [];
        if (!b.TeklifMi && b.Durum != TeklifSiparisDurumu.Taslak)
        {
            var idler = satirlar.Select(s => s.Id).ToList();
            var irsaliyeSatirlari = db.IrsaliyeSatirlari.Where(s => s.SiparisSatiriId != null && idler.Contains(s.SiparisSatiriId.Value)).Select(s => s.Id);
            irsaliyeler = await db.Irsaliyeler.AsNoTracking()
                .Where(i => i.Durum != IrsaliyeDurumu.Iptal && i.Satirlar.Any(s => s.SiparisSatiriId != null && idler.Contains(s.SiparisSatiriId.Value)))
                .OrderBy(i => i.SevkTarihi)
                .Select(i => new BagliBelge(i.Id, i.Numara, i.SevkTarihi))
                .ToListAsync(cancellationToken);
            faturalar = await db.Faturalar.AsNoTracking()
                .Where(f => f.Durum != FaturaDurumu.Iptal && f.Satirlar.Any(s =>
                    (s.SiparisSatiriId != null && idler.Contains(s.SiparisSatiriId.Value))
                    || (s.IrsaliyeSatiriId != null && irsaliyeSatirlari.Contains(s.IrsaliyeSatiriId.Value))))
                .OrderBy(f => f.Tarih)
                .Select(f => new BagliBelge(f.Id, f.Numara, f.Tarih))
                .ToListAsync(cancellationToken);
        }
        return new TeklifSiparisDetayi(
            b.Id, b.Tur, b.Durum, b.Karsilama, b.Numara, b.Tarih, b.GecerlilikTarihi, b.TerminTarihi,
            b.CariHesapId, b.CariKodu, b.CariUnvan, b.VergiNo, b.VergiDairesi, b.Il, b.Ilce, b.Adres, b.ParaBirimi, b.Kur,
            b.GenelIskontoTuru, b.GenelIskonto, b.OdemeKosulu, b.TeslimKosulu, b.Aciklama,
            b.BrutToplam, b.SatirIskontoToplami, b.GenelIskontoTutari, b.Matrah, b.KdvToplami, b.GenelToplam,
            satirlar.GroupBy(s => s.KdvOrani).OrderBy(g => g.Key).Select(g => new KdvDagilimi(g.Key, g.Sum(s => s.Matrah), g.Sum(s => s.KdvTutari))).ToList(),
            satirlar.Select(s => new TeklifSiparisSatiriDetayi(s.Id, s.Sira, s.StokKartiId, s.StokKodu, s.Ad, s.StokluMu, s.DepoId, s.Birim, s.Miktar,
                s.BirimFiyat, s.KdvDahilMi, s.KdvOrani, s.Iskonto1, s.Iskonto2, s.Iskonto3, s.Matrah, s.KdvTutari, s.Toplam,
                s.KarsilananMiktar, s.KalanMiktar)).ToList(),
            b.KaynakTeklifId, siparisler, irsaliyeler, faturalar,
            TeklifSiparisTurleri.SuresiDolduMu(b.Tur, b.Durum, b.GecerlilikTarihi, TurkiyeSaati.Bugun(zaman)),
            b.OnayTarihi, b.Surum);
    }
}
