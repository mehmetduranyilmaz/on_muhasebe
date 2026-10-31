using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.KasaBanka.Domain;
using OnMuhasebe.Modules.KasaBanka.Domain.CekSenet;
using OnMuhasebe.Modules.KasaBanka.Infrastructure;
using OnMuhasebe.Modules.KasaBanka.Infrastructure.CekSenet;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.KasaBanka.Features;

// Firmanın çek karneleri ve senet defterleri: verilen evrakta sıradaki yaprak önerisi, yaprak durumu, yırtık/kayıp yaprak iptali.
internal static class KarneIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        grup.MapGet("/karneler", ListeleAsync);
        grup.MapPost("/karneler", EkleAsync).Dogrula<KarneEkleIstegi>();
        grup.MapGet("/karneler/{id:guid}", DetayAsync);
        grup.MapPost("/karneler/{id:guid}/yaprak-iptal", YaprakIptalAsync).Dogrula<YaprakIptalIstegi>().Islem(YetkiIslemleri.Duzenle);
        grup.MapDelete("/karneler/{id:guid}/yaprak-iptal/{seriNo}", IptalGeriAlAsync).Islem(YetkiIslemleri.Duzenle);
        grup.MapPost("/karneler/{id:guid}/kapat", (Guid id, KasaBankaDbContext db, CekSenetServisi servis, CancellationToken ct) =>
            DurumAsync(id, false, db, servis, ct)).Islem(YetkiIslemleri.Duzenle);
        grup.MapPost("/karneler/{id:guid}/ac", (Guid id, KasaBankaDbContext db, CekSenetServisi servis, CancellationToken ct) =>
            DurumAsync(id, true, db, servis, ct)).Islem(YetkiIslemleri.Duzenle);
        grup.MapDelete("/karneler/{id:guid}", SilAsync);
    }

    internal sealed class Dogrulayici : AbstractValidator<KarneEkleIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.Tur).IsInEnum();
            RuleFor(x => x.KendiHesapId).NotNull().When(x => x.Tur == EvrakTuru.Cek).WithMessage("Çek karnesinin banka hesabı seçilmelidir.");
            RuleFor(x => x.BaslangicSeri).NotEmpty().MaximumLength(30)
                .Must(s => EvrakSeriNo.Ayir(s) is not null).WithMessage("Başlangıç seri no sayı ile bitmelidir (ör. ABC 1234501).");
            RuleFor(x => x.YaprakSayisi).InclusiveBetween(1, EvrakKarnesi.AzamiYaprak);
            RuleFor(x => x.Aciklama).MaximumLength(500);
        }
    }

    internal sealed class YaprakDogrulayici : AbstractValidator<YaprakIptalIstegi>
    {
        public YaprakDogrulayici()
        {
            RuleFor(x => x.SeriNo).NotEmpty().MaximumLength(30);
            RuleFor(x => x.Neden).NotEmpty().MaximumLength(EvrakKarnesi.NedenUzunlugu);
        }
    }

    private static async Task<IResult> ListeleAsync(
        EvrakTuru? tur, Guid? hesapId, bool? aktif, KasaBankaDbContext db, CekSenetServisi servis, CancellationToken ct)
    {
        var sorgu = db.Karneler.AsNoTracking().Include(k => k.Iptaller).AsQueryable();
        if (tur is not null)
        {
            sorgu = sorgu.Where(k => k.Tur == tur);
        }

        if (hesapId is not null)
        {
            sorgu = sorgu.Where(k => k.KendiHesapId == hesapId);
        }

        if (aktif is not null)
        {
            sorgu = sorgu.Where(k => k.AktifMi == aktif);
        }

        var karneler = await sorgu.OrderByDescending(k => k.AktifMi).ThenBy(k => k.Tarih).ThenBy(k => k.BaslangicNo).ToListAsync(ct);
        var ozetler = new List<KarneOzeti>(karneler.Count);
        var bilgi = await BilgiAsync(db, karneler, ct);
        foreach (var k in karneler)
        {
            ozetler.Add(Ozet(k, await servis.KullanilanSerilerAsync(k.Id, ct), bilgi));
        }

        return TypedResults.Ok(ozetler);
    }

    private static async Task<IResult> EkleAsync(KarneEkleIstegi istek, KasaBankaDbContext db, CekSenetServisi servis, CancellationToken ct)
    {
        if (istek.Tur == EvrakTuru.Cek)
        {
            var hesap = await db.Hesaplar.SingleOrDefaultAsync(h => h.Id == istek.KendiHesapId, ct);
            if (hesap is not { Tur: HesapTuru.Banka })
            {
                return KasaBankaHatalari.VerilenCekHesabi.Problem();
            }
        }

        var sonuc = EvrakKarnesi.Olustur(istek.Tur, istek.KendiHesapId, istek.BaslangicSeri, istek.YaprakSayisi, istek.Tarih, istek.Aciklama);
        if (!sonuc.BasariliMi)
        {
            return sonuc.Hata.Problem();
        }

        var karne = sonuc.Deger;
        var digerleri = await db.Karneler.Where(k => k.Tur == karne.Tur && k.KendiHesapId == karne.KendiHesapId && k.Onek == karne.Onek).ToListAsync(ct);
        if (digerleri.Any(karne.Cakisir))
        {
            return KasaBankaHatalari.KarneCakisiyor.Problem();
        }

        db.Karneler.Add(karne);
        await db.SaveChangesAsync(ct);
        var ozet = Ozet(karne, await servis.KullanilanSerilerAsync(karne.Id, ct), await BilgiAsync(db, [karne], ct));
        return TypedResults.Created($"/api/kasabanka/cek-senet/karneler/{karne.Id}", ozet);
    }

    private static async Task<IResult> DetayAsync(Guid id, KasaBankaDbContext db, CekSenetServisi servis, ICariSorgusu cariSorgusu, CancellationToken ct)
    {
        var karne = await db.Karneler.AsNoTracking().Include(k => k.Iptaller).SingleOrDefaultAsync(k => k.Id == id, ct);
        if (karne is null)
        {
            return KasaBankaHatalari.KarneBulunamadi.Problem();
        }

        return TypedResults.Ok(await DetayOlusturAsync(karne, db, servis, cariSorgusu, ct));
    }

    private static async Task<IResult> YaprakIptalAsync(
        Guid id, YaprakIptalIstegi istek, KasaBankaDbContext db, CekSenetServisi servis, ICariSorgusu cariSorgusu, TimeProvider zaman,
        CancellationToken ct)
    {
        var karne = await db.Karneler.Include(k => k.Iptaller).SingleOrDefaultAsync(k => k.Id == id, ct);
        if (karne is null)
        {
            return KasaBankaHatalari.KarneBulunamadi.Problem();
        }

        var sonuc = karne.YapragiIptalEt(istek.SeriNo, istek.Neden, DateOnly.FromDateTime(zaman.GetLocalNow().DateTime),
            await servis.KullanilanSerilerAsync(karne.Id, ct));
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await DetayOlusturAsync(karne, db, servis, cariSorgusu, ct));
    }

    private static async Task<IResult> IptalGeriAlAsync(
        Guid id, string seriNo, KasaBankaDbContext db, CekSenetServisi servis, ICariSorgusu cariSorgusu, CancellationToken ct)
    {
        var karne = await db.Karneler.Include(k => k.Iptaller).SingleOrDefaultAsync(k => k.Id == id, ct);
        if (karne is null)
        {
            return KasaBankaHatalari.KarneBulunamadi.Problem();
        }

        var sonuc = karne.IptaliGeriAl(seriNo);
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await DetayOlusturAsync(karne, db, servis, cariSorgusu, ct));
    }

    private static async Task<IResult> DurumAsync(Guid id, bool aktif, KasaBankaDbContext db, CekSenetServisi servis, CancellationToken ct)
    {
        var karne = await db.Karneler.Include(k => k.Iptaller).SingleOrDefaultAsync(k => k.Id == id, ct);
        if (karne is null)
        {
            return KasaBankaHatalari.KarneBulunamadi.Problem();
        }

        if (aktif)
        {
            karne.Ac();
        }
        else
        {
            karne.Kapat();
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(Ozet(karne, await servis.KullanilanSerilerAsync(karne.Id, ct), await BilgiAsync(db, [karne], ct)));
    }

    // Yalnızca yanlış tanımlanan (hiç yaprağı kullanılmamış) karne silinir; kullanılmışsa kapatılır.
    private static async Task<IResult> SilAsync(Guid id, KasaBankaDbContext db, CancellationToken ct)
    {
        var karne = await db.Karneler.Include(k => k.Iptaller).SingleOrDefaultAsync(k => k.Id == id, ct);
        if (karne is null)
        {
            return KasaBankaHatalari.KarneBulunamadi.Problem();
        }

        if (await db.Evraklar.AnyAsync(e => e.KarneId == id, ct))
        {
            return KasaBankaHatalari.KarneKullanildi.Problem();
        }

        db.Karneler.Remove(karne);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<KarneDetayi> DetayOlusturAsync(
        EvrakKarnesi karne, KasaBankaDbContext db, CekSenetServisi servis, ICariSorgusu cariSorgusu, CancellationToken ct)
    {
        var evraklar = await db.Evraklar.AsNoTracking()
            .Where(e => e.KarneId == karne.Id && e.Durum != EvrakDurumu.Iptal)
            .Select(e => new { e.Id, e.Kod, e.SeriNo, e.CariHesapId, e.Tutar, e.VadeTarihi })
            .ToListAsync(ct);
        var unvanlar = await cariSorgusu.UnvanlarAsync(evraklar.Select(e => e.CariHesapId).Distinct().ToList(), ct);
        var seridenEvrak = evraklar.GroupBy(e => e.SeriNo).ToDictionary(g => g.Key, g => g.First());
        var iptaller = karne.Iptaller.ToDictionary(i => i.SeriNo);

        var yapraklar = karne.Seriler().Select(s =>
            seridenEvrak.TryGetValue(s, out var e)
                ? new KarneYapragi(s, YaprakDurumu.Kullanildi, e.Id, e.Kod, unvanlar.GetValueOrDefault(e.CariHesapId), e.Tutar, e.VadeTarihi, null)
                : iptaller.TryGetValue(s, out var i)
                    ? new KarneYapragi(s, YaprakDurumu.Iptal, null, null, null, null, null, i.Neden)
                    : new KarneYapragi(s, YaprakDurumu.Bos, null, null, null, null, null, null)).ToList();

        return new KarneDetayi(Ozet(karne, await servis.KullanilanSerilerAsync(karne.Id, ct), await BilgiAsync(db, [karne], ct)), yapraklar);
    }

    private sealed record KarneBilgisi(IReadOnlyDictionary<Guid, string> HesapAdlari, int UyariEsigi);

    private static async Task<KarneBilgisi> BilgiAsync(KasaBankaDbContext db, IReadOnlyCollection<EvrakKarnesi> karneler, CancellationToken ct)
    {
        var hesapIdleri = karneler.Where(k => k.KendiHesapId is not null).Select(k => k.KendiHesapId!.Value).Distinct().ToList();
        var adlar = await db.Hesaplar.AsNoTracking().Where(h => hesapIdleri.Contains(h.Id)).ToDictionaryAsync(h => h.Id, h => h.Ad, ct);
        var esik = await db.CekSenetAyarlari.AsNoTracking().Select(a => (int?)a.KarneUyariEsigi).SingleOrDefaultAsync(ct) ?? 5;
        return new KarneBilgisi(adlar, esik);
    }

    private static KarneOzeti Ozet(EvrakKarnesi k, IReadOnlySet<string> kullanilanlar, KarneBilgisi bilgi)
    {
        var kalan = k.Kalan(kullanilanlar);
        return new KarneOzeti(
            k.Id, k.Tur, k.KendiHesapId, k.KendiHesapId is { } h ? bilgi.HesapAdlari.GetValueOrDefault(h) : null,
            k.Seri(k.BaslangicNo), k.Seri(k.BitisNo), k.YaprakSayisi, k.Seriler().Count(kullanilanlar.Contains), k.Iptaller.Count, kalan,
            k.AktifMi ? k.Siradaki(kullanilanlar) : null, k.AktifMi && kalan <= bilgi.UyariEsigi, k.Tarih, k.Aciklama, k.AktifMi);
    }
}
