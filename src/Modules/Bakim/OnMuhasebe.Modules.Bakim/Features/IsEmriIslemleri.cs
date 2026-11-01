using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Bakim.Contracts;
using OnMuhasebe.Modules.Bakim.Domain;
using OnMuhasebe.Modules.Bakim.Infrastructure;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Contracts.Sayfalama;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Bakim.Features;

// Bakım ve arıza iş emirleri. Stok kartlı parça iş emri kapanınca sarf olarak düşer, kapanmış iş emri iptal edilince geri alınır.
// İşçilik ve dış servis takip kaydıdır, muhasebe fişi üretmez.
internal static class IsEmriIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var g = grup.MapGroup("/is-emirleri").Yetki(BakimYetkileri.IsEmri);
        g.MapGet("", ListeleAsync);
        g.MapGet("/{id:guid}", DetayAsync);
        g.MapPost("", EkleAsync).Dogrula<IsEmriKaydetIstegi>();
        g.MapPut("/{id:guid}", GuncelleAsync).Dogrula<IsEmriKaydetIstegi>();
        g.MapPost("/{id:guid}/baslat", BaslatAsync).Islem(YetkiIslemleri.Duzenle);
        g.MapPost("/{id:guid}/kapat", KapatAsync).Islem(YetkiIslemleri.Duzenle).Dogrula<IsEmriKapatmaIstegi>();
        g.MapPost("/{id:guid}/iptal", IptalAsync).Islem(YetkiIslemleri.Sil);
    }

    internal sealed class Dogrulayici : AbstractValidator<IsEmriKaydetIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.Tur).IsInEnum();
            RuleFor(x => x.Baslik).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Aciklama).MaximumLength(2000);
            RuleFor(x => x.IscilikTutari).GreaterThanOrEqualTo(0);
            RuleFor(x => x.DisServisTutari).GreaterThanOrEqualTo(0);
            RuleFor(x => x.DisServisFirma).MaximumLength(200);
            RuleFor(x => x.KontrolKurulusu).MaximumLength(200);
            RuleFor(x => x.RaporNo).MaximumLength(100);
            RuleFor(x => x.Parcalar).Must(p => p is null || p.Count <= 100).WithMessage("Bir iş emrinde en çok 100 parça olabilir.");
            RuleForEach(x => x.Parcalar).ChildRules(p =>
            {
                p.RuleFor(x => x.Miktar).GreaterThan(0).WithMessage("Parça miktarı sıfırdan büyük olmalıdır.");
                p.RuleFor(x => x.BirimFiyat).GreaterThanOrEqualTo(0);
                p.RuleFor(x => x.Ad).MaximumLength(250);
                p.RuleFor(x => x.Birim).MaximumLength(20);
            });
        }
    }

    internal sealed class KapatmaDogrulayici : AbstractValidator<IsEmriKapatmaIstegi>
    {
        public KapatmaDogrulayici()
        {
            RuleFor(x => x.BitisZamani).NotEmpty();
            RuleFor(x => x.SayacDegeri).GreaterThanOrEqualTo(0);
            RuleFor(x => x.RaporNo).MaximumLength(100);
        }
    }

    private static DateTime Simdi(TimeProvider zaman) => zaman.GetUtcNow().UtcDateTime;

    private static async Task<IResult> ListeleAsync(
        BakimDbContext db, Guid? ekipmanId, IsEmriDurumu? durum, bool? aciklar, IsEmriTuru? tur, int? sayfa, int? boyut, CancellationToken ct)
    {
        var sorgu = from i in db.IsEmirleri.AsNoTracking() join e in db.Ekipmanlar on i.EkipmanId equals e.Id select new { i, EkipmanAdi = e.Ad };
        if (ekipmanId is not null)
        {
            sorgu = sorgu.Where(x => x.i.EkipmanId == ekipmanId);
        }

        if (durum is not null)
        {
            sorgu = sorgu.Where(x => x.i.Durum == durum);
        }

        if (aciklar == true)
        {
            sorgu = sorgu.Where(x => x.i.Durum == IsEmriDurumu.Acik || x.i.Durum == IsEmriDurumu.Devam);
        }

        if (tur is not null)
        {
            sorgu = sorgu.Where(x => x.i.Tur == tur);
        }

        var sonuc = await sorgu.OrderByDescending(x => x.i.BildirimZamani)
            .Select(x => new { x.i.Id, x.i.Kod, x.i.EkipmanId, x.EkipmanAdi, x.i.Tur, x.i.Durum, x.i.Baslik, x.i.BildirimZamani, x.i.BaslangicZamani, x.i.BitisZamani, x.i.MakineDurduMu })
            .SayfalaAsync(new SayfalamaIstegi(sayfa ?? 1, boyut ?? SayfalamaIstegi.VarsayilanBoyut), ct);
        return TypedResults.Ok(new SayfaliSonuc<IsEmriOzeti>(sonuc.Kayitlar.Select(x => new IsEmriOzeti(x.Id, x.Kod, x.EkipmanId, x.EkipmanAdi, x.Tur, x.Durum, x.Baslik, x.BildirimZamani, x.BitisZamani,
            BakimHesaplari.DurusSaati(x.MakineDurduMu, x.Tur, x.BildirimZamani, x.BaslangicZamani, x.BitisZamani))).ToList(), sonuc.Sayfa, sonuc.Boyut, sonuc.ToplamKayit));
    }

    private static async Task<IResult> DetayAsync(Guid id, BakimDbContext db, CancellationToken ct)
    {
        var (emir, ekipman) = await BulAsync(id, db, ct);
        return emir is null ? BakimHatalari.IsEmriYok.Problem() : TypedResults.Ok(Detay(emir, ekipman!.Ad));
    }

    private static async Task<IResult> EkleAsync(
        IsEmriKaydetIstegi istek, BakimDbContext db, IMevcutKullanici kullanici, IStokSorgusu stok, IFaturaMuhasebeSorgusu fatura, ILisansSorgusu lisans, TimeProvider zaman, CancellationToken ct)
    {
        var ekipman = await db.Ekipmanlar.SingleOrDefaultAsync(e => e.Id == istek.EkipmanId, ct);
        if (ekipman is null)
        {
            return BakimHatalari.EkipmanYok.Problem();
        }

        var denetim = await DenetleAsync(istek, null, db, kullanici, stok, fatura, lisans, ct);
        if (!denetim.BasariliMi)
        {
            return denetim.Hata.Problem();
        }

        var kod = await KodUretici.SonrakiAsync(db, "bakim.is_emri", "IE", kullanici.FirmaId!.Value, ct);
        var emir = IsEmri.Olustur(kod, istek with { Parcalar = denetim.Deger }, Simdi(zaman));
        db.IsEmirleri.Add(emir);
        if (emir.Tur == IsEmriTuru.Ariza)
        {
            ekipman.DurumAta(EkipmanDurumu.Arizali);
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/bakim/is-emirleri/{emir.Id}", Detay(emir, ekipman.Ad));
    }

    private static async Task<IResult> GuncelleAsync(
        Guid id, IsEmriKaydetIstegi istek, BakimDbContext db, IMevcutKullanici kullanici, IStokSorgusu stok, IFaturaMuhasebeSorgusu fatura, ILisansSorgusu lisans, TimeProvider zaman, CancellationToken ct)
    {
        var (emir, ekipman) = await BulAsync(id, db, ct);
        if (emir is null)
        {
            return BakimHatalari.IsEmriYok.Problem();
        }

        var denetim = await DenetleAsync(istek with { EkipmanId = emir.EkipmanId }, emir, db, kullanici, stok, fatura, lisans, ct);
        if (!denetim.BasariliMi)
        {
            return denetim.Hata.Problem();
        }

        db.RemoveRange(emir.Parcalar);
        var sonuc = emir.Guncelle(istek with { Parcalar = denetim.Deger }, Simdi(zaman));
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(Detay(emir, ekipman!.Ad));
    }

    private static async Task<IResult> BaslatAsync(Guid id, BakimDbContext db, TimeProvider zaman, CancellationToken ct)
    {
        var (emir, ekipman) = await BulAsync(id, db, ct);
        if (emir is null)
        {
            return BakimHatalari.IsEmriYok.Problem();
        }

        var sonuc = emir.Baslat(Simdi(zaman));
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        if (emir.MakineDurduMu)
        {
            ekipman!.DurumAta(EkipmanDurumu.Bakimda);
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(Detay(emir, ekipman!.Ad));
    }

    // Tek işlem: iş emri, plan, ekipman ve stok birlikte kaydedilir. Stok eksi kuralı reddederse hiçbiri kaydedilmez.
    private static Task<IResult> KapatAsync(
        Guid id, IsEmriKapatmaIstegi istek, BakimDbContext db, IStokHareketYazici stokYazici, IIslemKapsami islemKapsami, TimeProvider zaman, CancellationToken ct) =>
        SonucAsync(islemKapsami.CalistirAsync<IsEmriDetayi>(async c =>
        {
            var (emir, ekipman) = await BulAsync(id, db, c);
            if (emir is null)
            {
                return BakimHatalari.IsEmriYok;
            }

            var kapanis = emir.Kapat(istek);
            if (!kapanis.BasariliMi)
            {
                return kapanis.Hata;
            }

            ekipman!.SayacIlerlet(istek.SayacDegeri);
            var tarih = TurkiyeSaati.Bugun(zaman);
            var bitisTarihi = TurkiyeSaati.Gunu(emir.BitisZamani!.Value);
            if (emir.PlanId is { } planId && await db.Planlar.SingleOrDefaultAsync(p => p.Id == planId, c) is { } plan)
            {
                plan.Yapildi(bitisTarihi > tarih ? tarih : bitisTarihi, istek.SayacDegeri ?? ekipman.Sayac);
            }

            await EkipmanDurumunuAyarlaAsync(db, emir, ekipman, c);
            await db.SaveChangesAsync(c);

            var stokParcalari = emir.Parcalar.Where(p => p.StokKartiId is not null).ToList();
            string? uyari = null;
            if (stokParcalari.Count > 0)
            {
                var hareketler = stokParcalari.Select(p => new BelgeStokHareketi(p.StokKartiId!.Value, p.DepoId!.Value, StokHareketTuru.Sarf, bitisTarihi, p.Birim!, p.Miktar, null,
                    emir.Kod, $"İş emri {emir.Kod}: {emir.Baslik}")).ToList();
                var yazim = await stokYazici.YazAsync(emir.Id, hareketler, c);
                if (!yazim.BasariliMi)
                {
                    return yazim.Hata;
                }

                uyari = yazim.Deger;
            }

            return Detay(emir, ekipman.Ad) with { StokUyarisi = uyari };
        }, ct));

    // Kapanmış iş emrinde stok geri alınır. Planın tarihi kendiliğinden geri alınmaz, gerekirse plandan düzeltilir.
    private static Task<IResult> IptalAsync(Guid id, BakimDbContext db, IStokHareketYazici stokYazici, IIslemKapsami islemKapsami, CancellationToken ct) =>
        SonucAsync(islemKapsami.CalistirAsync<IsEmriDetayi>(async c =>
        {
            var (emir, ekipman) = await BulAsync(id, db, c);
            if (emir is null)
            {
                return BakimHatalari.IsEmriYok;
            }

            var kapaliMi = emir.Durum == IsEmriDurumu.Kapandi;
            var iptal = emir.IptalEt();
            if (!iptal.BasariliMi)
            {
                return iptal.Hata;
            }

            await EkipmanDurumunuAyarlaAsync(db, emir, ekipman!, c);
            await db.SaveChangesAsync(c);
            if (kapaliMi && emir.Parcalar.Any(p => p.StokKartiId is not null))
            {
                var geri = await stokYazici.IptalEtAsync(emir.Id, c);
                if (!geri.BasariliMi)
                {
                    return geri.Hata;
                }
            }

            return Detay(emir, ekipman!.Ad);
        }, ct));

    private static async Task<IResult> SonucAsync(Task<Sonuc<IsEmriDetayi>> islem)
    {
        var sonuc = await islem;
        return sonuc.BasariliMi ? TypedResults.Ok(sonuc.Deger) : sonuc.Hata.Problem();
    }

    // Başka açık durduran iş emri kalmadıysa ekipman çalışıyor sayılır.
    private static async Task EkipmanDurumunuAyarlaAsync(BakimDbContext db, IsEmri emir, Ekipman ekipman, CancellationToken ct)
    {
        var acikAriza = await db.IsEmirleri.AnyAsync(i => i.EkipmanId == ekipman.Id && i.Id != emir.Id && i.Tur == IsEmriTuru.Ariza
            && (i.Durum == IsEmriDurumu.Acik || i.Durum == IsEmriDurumu.Devam), ct);
        var acikDuran = await db.IsEmirleri.AnyAsync(i => i.EkipmanId == ekipman.Id && i.Id != emir.Id && i.MakineDurduMu && i.Durum == IsEmriDurumu.Devam, ct);
        ekipman.DurumAta(acikAriza ? EkipmanDurumu.Arizali : acikDuran ? EkipmanDurumu.Bakimda : EkipmanDurumu.Calisiyor);
    }

    // Ekipman sorgu filtresinden (firma, işyeri, silinme) geçmeyen iş emri bulunamadı sayılır.
    private static async Task<(IsEmri? Emir, Ekipman? Ekipman)> BulAsync(Guid id, BakimDbContext db, CancellationToken ct)
    {
        var emir = await db.IsEmirleri.Include(i => i.Parcalar).SingleOrDefaultAsync(i => i.Id == id, ct);
        var ekipman = emir is null ? null : await db.Ekipmanlar.SingleOrDefaultAsync(e => e.Id == emir.EkipmanId, ct);
        return ekipman is null ? (null, null) : (emir, ekipman);
    }

    // Parçaların adı, birimi ve deposu stok kartından tamamlanır; böylece kapanışta düşecek hareket baştan doğrulanır.
    private static async Task<Sonuc<List<IsEmriParcaIstegi>>> DenetleAsync(
        IsEmriKaydetIstegi istek, IsEmri? mevcut, BakimDbContext db, IMevcutKullanici kullanici, IStokSorgusu stok, IFaturaMuhasebeSorgusu fatura, ILisansSorgusu lisans, CancellationToken ct)
    {
        if (istek.PlanId is { } planId && planId != mevcut?.PlanId && !await db.Planlar.AnyAsync(p => p.Id == planId && p.EkipmanId == istek.EkipmanId, ct))
        {
            return BakimHatalari.PlanEkipmanFarkli;
        }

        if (istek.FaturaId is { } faturaId && faturaId != mevcut?.FaturaId && !await fatura.VarMiAsync(faturaId, ct))
        {
            return BakimHatalari.FaturaGecersiz;
        }

        var parcalar = istek.Parcalar ?? [];
        var kartIdleri = parcalar.Where(p => p.StokKartiId is not null).Select(p => p.StokKartiId!.Value).Distinct().ToList();
        var kartlar = new Dictionary<Guid, StokKartiBilgisi>();
        if (kartIdleri.Count > 0)
        {
            var moduller = (await lisans.GetirAsync(kullanici.FirmaId!.Value, ct))?.Moduller ?? [];
            if (!moduller.Contains(ModulKodlari.Stok))
            {
                return BakimHatalari.StokLisansiYok;
            }

            kartlar = (await stok.KartlariGetirAsync(kartIdleri, ct)).ToDictionary(k => k.Id);
        }

        var sonuc = new List<IsEmriParcaIstegi>();
        foreach (var p in parcalar)
        {
            if (p.StokKartiId is not { } kartId)
            {
                if (string.IsNullOrWhiteSpace(p.Ad))
                {
                    return BakimHatalari.ParcaAdiGerekli;
                }

                sonuc.Add(p);
                continue;
            }

            if (!kartlar.TryGetValue(kartId, out var kart) || !kart.AktifMi || kart.Tur == StokTuru.Hizmet)
            {
                return BakimHatalari.StokKartiGecersiz;
            }

            if (kart.Izleme != IzlemeTuru.Yok || kart.SktTakibi)
            {
                return BakimHatalari.StokIzlemeli;
            }

            var birim = string.IsNullOrWhiteSpace(p.Birim) ? kart.TemelBirim : p.Birim.Trim();
            if (!kart.BirimTanimliMi(birim))
            {
                return BakimHatalari.StokBirimGecersiz;
            }

            if (await stok.DepoCozAsync(p.DepoId, ct) is not { } depoId)
            {
                return BakimHatalari.DepoGecersiz;
            }

            sonuc.Add(p with { Ad = kart.Ad, Birim = birim, DepoId = depoId });
        }

        return sonuc;
    }

    private static IsEmriDetayi Detay(IsEmri i, string ekipmanAdi) => new(
        i.Id, i.Kod, i.EkipmanId, ekipmanAdi, i.Tur, i.PlanId, i.Durum, i.Baslik, i.Aciklama, i.BildirimZamani, i.BaslangicZamani, i.BitisZamani, i.MakineDurduMu, i.DurusSaati,
        i.IscilikTutari, i.DisServisTutari, i.DisServisFirma, i.FaturaId, i.KontrolKurulusu, i.RaporNo, i.SayacDegeri,
        i.Parcalar.Select(p => new IsEmriParcasiCevabi(p.StokKartiId, p.DepoId, p.Ad, p.Birim, p.Miktar, p.BirimFiyat)).ToList());
}
