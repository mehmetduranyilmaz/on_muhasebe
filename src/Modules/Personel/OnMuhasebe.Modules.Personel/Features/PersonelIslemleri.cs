using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.Personel.Contracts;
using OnMuhasebe.Modules.Personel.Domain;
using OnMuhasebe.Modules.Personel.Infrastructure;
using OnMuhasebe.Modules.Yonetim.Contracts;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Sayfalama;
using OnMuhasebe.Shared.Domain.Dogrulama;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Personel.Features;

// Personel kartı, işten çıkış ve kişi hesabı. Maaş, TC kimlik ve IBAN yalnız maaş yetkisiyle görünür ve değişir.
internal static class PersonelIslemleri
{
    private static readonly string MaasGorme = PersonelYetkileri.Maas.Yetki(YetkiIslemleri.Goruntule);
    private static readonly string MaasDuzenleme = PersonelYetkileri.Maas.Yetki(YetkiIslemleri.Duzenle);

    public static void Map(RouteGroupBuilder grup)
    {
        var g = grup.MapGroup("/kartlar").Yetki(PersonelYetkileri.Kart);
        g.MapGet("", ListeleAsync);
        g.MapGet("/kullanicilar", KullanicilarAsync);
        g.MapGet("/{id:guid}", GetirAsync);
        g.MapPost("", EkleAsync).Dogrula<PersonelKaydetIstegi>();
        g.MapPut("/{id:guid}", GuncelleAsync).Dogrula<PersonelKaydetIstegi>();
        g.MapDelete("/{id:guid}", SilAsync);
        g.MapPost("/{id:guid}/isten-cikis", CikisAsync).Islem(YetkiIslemleri.Duzenle);
        g.MapGet("/{id:guid}/hesap", HesapAsync).Yetki(PersonelYetkileri.Maas);
        g.MapGet("/{id:guid}/odemeler", OdemelerAsync).Yetki(PersonelYetkileri.Maas);
    }

    internal sealed class Dogrulayici : AbstractValidator<PersonelKaydetIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.Ad).NotEmpty().WithMessage("Adı yazın.").MaximumLength(100);
            RuleFor(x => x.Soyad).NotEmpty().WithMessage("Soyadı yazın.").MaximumLength(100);
            RuleFor(x => x.NetMaas).GreaterThanOrEqualTo(0).When(x => x.NetMaas is not null);
            RuleFor(x => x.Gorev).MaximumLength(100);
            RuleFor(x => x.Departman).MaximumLength(100);
            RuleFor(x => x.Telefon).MaximumLength(30);
            RuleFor(x => x.Eposta).MaximumLength(256);
        }
    }

    internal sealed record Bagimliliklar(PersonelDbContext Db, IYetkiDenetimi Yetki, IMevcutKullanici Kullanici, IFirmaRehberi Rehber, IKasaBankaSorgusu Kasa, IsyeriSecici Isyeri);

    private static async Task<IResult> ListeleAsync(PersonelDbContext db, string? arama, bool? aktif, Guid? isyeriId, int? sayfa, int? boyut, CancellationToken ct)
    {
        var sorgu = db.Personeller.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(arama))
        {
            var desen = AramaDeseni.Icerir(arama);
            sorgu = sorgu.Where(p => EF.Functions.ILike(p.Ad + " " + p.Soyad, desen) || EF.Functions.ILike(p.Kod, desen) || EF.Functions.ILike(p.Gorev ?? "", desen) || EF.Functions.ILike(p.Departman ?? "", desen));
        }

        if (aktif is { } a)
        {
            sorgu = a ? sorgu.Where(p => p.IstenCikis == null) : sorgu.Where(p => p.IstenCikis != null);
        }

        if (isyeriId is not null)
        {
            sorgu = sorgu.Where(p => p.IsyeriId == isyeriId);
        }

        return TypedResults.Ok(await sorgu.OrderBy(p => p.Ad).ThenBy(p => p.Soyad)
            .Select(p => new PersonelOzeti(p.Id, p.Kod, p.Ad + " " + p.Soyad, p.Gorev, p.Departman, p.IseGiris, p.IstenCikis, p.IstenCikis == null,
                db.Zimmetler.Count(z => z.PersonelId == p.Id && z.Durum == ZimmetDurumu.Zimmette), p.IsyeriId))
            .SayfalaAsync(new SayfalamaIstegi(sayfa ?? 1, boyut ?? SayfalamaIstegi.VarsayilanBoyut), ct));
    }

    private static async Task<IResult> KullanicilarAsync(IFirmaRehberi rehber, IMevcutKullanici kullanici, CancellationToken ct) =>
        TypedResults.Ok((await rehber.KullanicilarAsync(kullanici.FirmaId!.Value, ct)).OrderBy(k => k.AdSoyad).Select(k => new KullaniciSecenegi(k.KullaniciId, k.AdSoyad)).ToList());

    private static async Task<IResult> GetirAsync(Guid id, PersonelDbContext db, IYetkiDenetimi yetki, CancellationToken ct)
    {
        var p = await db.Personeller.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return p is null ? PersonelHatalari.Bulunamadi.Problem() : TypedResults.Ok(await DetayAsync(p, db, yetki, ct));
    }

    private static async Task<IResult> EkleAsync(PersonelKaydetIstegi istek, [AsParameters] Bagimliliklar b, CancellationToken ct)
    {
        // Maaş yetkisi olmayan kişi kart açarsa maaş, kimlik ve IBAN yazılmaz; yetkilisi tamamlar.
        var maasYetkisi = await b.Yetki.VarMiAsync(MaasDuzenleme, ct);
        var temiz = maasYetkisi ? istek : istek with { TcKimlik = null, Iban = null, NetMaas = null };
        var isyeri = await b.Isyeri.BelirleAsync(istek.IsyeriId, ct);
        if (!isyeri.BasariliMi)
        {
            return isyeri.Hata.Problem();
        }

        if (await BenzersizlikHatasiAsync(temiz, null, b, ct) is { } hata)
        {
            return hata.Problem();
        }

        var kod = await KodUretici.SonrakiAsync(b.Db, "personel.personel_karti", "PR", b.Kullanici.FirmaId!.Value, ct);
        var p = PersonelKarti.Olustur(kod, temiz, isyeri.Deger);
        if (!p.BasariliMi)
        {
            return p.HttpSonucu();
        }

        b.Db.Personeller.Add(p.Deger);
        await b.Db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/personel/kartlar/{p.Deger.Id}", await DetayAsync(p.Deger, b.Db, b.Yetki, ct));
    }

    private static async Task<IResult> GuncelleAsync(Guid id, PersonelKaydetIstegi istek, [AsParameters] Bagimliliklar b, CancellationToken ct)
    {
        var p = await b.Db.Personeller.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (p is null)
        {
            return PersonelHatalari.Bulunamadi.Problem();
        }

        if (await BenzersizlikHatasiAsync(istek, p, b, ct) is { } hata)
        {
            return hata.Problem();
        }

        var isyeri = await b.Isyeri.DegisiklikAsync(p.IsyeriId ?? Guid.Empty, istek.IsyeriId, ct);
        if (!isyeri.BasariliMi)
        {
            return isyeri.Hata.Problem();
        }

        b.Db.Entry(p).Property(x => x.Surum).OriginalValue = istek.Surum;
        var sonuc = p.Guncelle(istek, isyeri.Deger, await b.Yetki.VarMiAsync(MaasDuzenleme, ct));
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        await b.Db.SaveChangesAsync(ct);
        return TypedResults.Ok(await DetayAsync(p, b.Db, b.Yetki, ct));
    }

    private static async Task<IResult> SilAsync(Guid id, PersonelDbContext db, CancellationToken ct)
    {
        var p = await db.Personeller.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (p is null)
        {
            return PersonelHatalari.Bulunamadi.Problem();
        }

        if (await db.MaasSatirlari.IgnoreQueryFilters().AnyAsync(s => s.PersonelId == id, ct) || await db.Avanslar.AnyAsync(a => a.PersonelId == id, ct)
            || await db.Masraflar.AnyAsync(m => m.PersonelId == id, ct) || await db.Zimmetler.AnyAsync(z => z.PersonelId == id, ct))
        {
            return PersonelHatalari.HareketliSilinemez.Problem();
        }

        db.Personeller.Remove(p);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    // Üzerinde zimmet ya da kalan bakiye varsa çıkış engellenmez, çıkış ekranında uyarı olarak gösterilir.
    private static async Task<IResult> CikisAsync(Guid id, IstenCikisIstegi istek, PersonelDbContext db, IYetkiDenetimi yetki, CancellationToken ct)
    {
        var p = await db.Personeller.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (p is null)
        {
            return PersonelHatalari.Bulunamadi.Problem();
        }

        var sonuc = p.Cikis(istek.Tarih);
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        await db.SaveChangesAsync(ct);
        var zimmet = await db.Zimmetler.CountAsync(z => z.PersonelId == id && z.Durum == ZimmetDurumu.Zimmette, ct);
        var bakiye = await yetki.VarMiAsync(MaasGorme, ct) ? (await HesapHesaplaAsync(p, db, ct)).Bakiye : 0;
        return TypedResults.Ok(new IstenCikisSonucu(await DetayAsync(p, db, yetki, ct), zimmet, bakiye));
    }

    private static async Task<IResult> HesapAsync(Guid id, PersonelDbContext db, CancellationToken ct)
    {
        var p = await db.Personeller.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return p is null ? PersonelHatalari.Bulunamadi.Problem() : TypedResults.Ok(await HesapHesaplaAsync(p, db, ct));
    }

    private static async Task<IResult> OdemelerAsync(Guid id, PersonelDbContext db, CancellationToken ct)
    {
        if (!await db.Personeller.AnyAsync(p => p.Id == id, ct))
        {
            return PersonelHatalari.Bulunamadi.Problem();
        }

        return TypedResults.Ok(await db.Odemeler.AsNoTracking().Where(o => o.PersonelId == id && !o.IptalMi).OrderByDescending(o => o.Tarih).Take(200)
            .Select(o => new PersonelOdemeCevabi(o.Id, o.Tur, o.Tarih, o.Tutar, o.HesapId != null)).ToListAsync(ct));
    }

    private static async Task<PersonelHesabi> HesapHesaplaAsync(PersonelKarti p, PersonelDbContext db, CancellationToken ct)
    {
        var satirlar = new List<(DateOnly Tarih, string Islem, string? Aciklama, decimal Alacak, decimal Borc)>();
        var tahakkuklar = await (from s in db.MaasSatirlari.AsNoTracking()
                                 join d in db.Donemler.AsNoTracking() on s.DonemId equals d.Id
                                 where s.PersonelId == p.Id && d.Durum == MaasDonemiDurumu.Onayli
                                 select new { d.Yil, d.Ay, s.NetMaas, s.Prim, s.Kesinti, s.AvansKesintisi }).ToListAsync(ct);
        foreach (var t in tahakkuklar)
        {
            var tutar = t.NetMaas + t.Prim - t.Kesinti;
            satirlar.Add((MaasHesabi.DonemSonu(t.Yil, t.Ay), "Maaş tahakkuku", $"{t.Ay:00}/{t.Yil}", tutar, 0));
            if (t.AvansKesintisi > 0)
            {
                satirlar.Add((MaasHesabi.DonemSonu(t.Yil, t.Ay), "Avans mahsubu", $"{t.Ay:00}/{t.Yil}", 0, t.AvansKesintisi));
            }
        }

        var masraflar = await db.Masraflar.AsNoTracking().Where(m => m.PersonelId == p.Id && (m.Durum == MasrafDurumu.Onaylandi || m.Durum == MasrafDurumu.Odendi)).ToListAsync(ct);
        satirlar.AddRange(masraflar.Select(m => (m.Tarih, "Masraf", (string?)m.Aciklama, m.Tutar, 0m)));
        var odemeler = await db.Odemeler.AsNoTracking().Where(o => o.PersonelId == p.Id && !o.IptalMi && o.Tur != PersonelOdemeTuru.Avans).ToListAsync(ct);
        satirlar.AddRange(odemeler.Select(o => (o.Tarih, PersonelMetinleri.Odeme(o.Tur) + " ödemesi", (string?)null, 0m, o.Tutar)));
        var avanslar = await db.Avanslar.AsNoTracking().Where(a => a.PersonelId == p.Id && !a.IptalMi).ToListAsync(ct);
        satirlar.AddRange(avanslar.Select(a => (a.Tarih, "Avans", a.Aciklama, a.Tutar, 0m)));

        // Avans personelin borcudur: verilince bakiye düşer, mahsup edilince maaş borcuyla kapanır.
        var bakiye = 0m;
        var liste = new List<PersonelHesapSatiri>();
        foreach (var s in satirlar.OrderBy(x => x.Tarih).ToList())
        {
            var avansMi = s.Islem == "Avans";
            var mahsupMu = s.Islem == "Avans mahsubu";
            var etki = avansMi ? -s.Alacak : mahsupMu ? 0 : s.Alacak - s.Borc;
            bakiye += etki;
            liste.Add(new PersonelHesapSatiri(s.Tarih, s.Islem, s.Aciklama, avansMi ? 0 : s.Alacak, avansMi ? s.Alacak : mahsupMu ? 0 : s.Borc, bakiye));
        }

        return new PersonelHesabi(p.Id, p.AdSoyad, liste, bakiye);
    }

    private static async Task<Hata?> BenzersizlikHatasiAsync(PersonelKaydetIstegi i, PersonelKarti? mevcut, Bagimliliklar b, CancellationToken ct)
    {
        var maasYetkisi = await b.Yetki.VarMiAsync(MaasDuzenleme, ct);
        var tc = i.TcKimlik?.Trim();
        if (maasYetkisi && !string.IsNullOrEmpty(tc) && await b.Db.TumPersoneller.AnyAsync(p => p.TcKimlik == tc && p.Id != (mevcut == null ? Guid.Empty : mevcut.Id), ct))
        {
            return PersonelHatalari.TcKullaniliyor;
        }

        if (i.MasrafMerkeziId is { } merkez && merkez != mevcut?.MasrafMerkeziId && !await b.Kasa.MasrafMerkeziAktifMiAsync(merkez, ct))
        {
            return PersonelHatalari.MasrafMerkezi;
        }

        if (i.KullaniciId is { } kullaniciId && kullaniciId != mevcut?.KullaniciId)
        {
            if (!(await b.Rehber.KullanicilarAsync(b.Kullanici.FirmaId!.Value, ct)).Any(k => k.KullaniciId == kullaniciId))
            {
                return PersonelHatalari.KullaniciGecersiz;
            }

            if (await b.Db.TumPersoneller.AnyAsync(p => p.KullaniciId == kullaniciId, ct))
            {
                return PersonelHatalari.KullaniciKullaniliyor;
            }
        }

        return null;
    }

    private static async Task<PersonelDetayi> DetayAsync(PersonelKarti p, PersonelDbContext db, IYetkiDenetimi yetki, CancellationToken ct)
    {
        var maas = await yetki.VarMiAsync(MaasGorme, ct);
        var zimmet = await db.Zimmetler.CountAsync(z => z.PersonelId == p.Id && z.Durum == ZimmetDurumu.Zimmette, ct);
        return new PersonelDetayi(p.Id, p.Kod, p.Ad, p.Soyad, maas ? p.TcKimlik : MaasHesabi.Maskele(p.TcKimlik), p.IseGiris, p.IstenCikis, p.Gorev, p.Departman, p.IsyeriId, p.MasrafMerkeziId,
            maas ? p.Iban : MaasHesabi.Maskele(p.Iban, 4, 2), maas ? p.NetMaas : null, p.Telefon, p.Eposta, p.KullaniciId, zimmet, p.Surum);
    }
}

// Maaş dönemi: toplu tahakkuk, düzeltme, onay ve ödeme. Bordroyu mali müşavir hazırlar, buraya sonuç girilir.
internal static class MaasIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var g = grup.MapGroup("/donemler").Yetki(PersonelYetkileri.Maas);
        g.MapGet("", ListeleAsync);
        g.MapGet("/{id:guid}", GetirAsync);
        g.MapPost("", AcAsync).Dogrula<DonemAcIstegi>();
        g.MapDelete("/{id:guid}", SilAsync);
        g.MapPut("/{id:guid}/satirlar/{satirId:guid}", SatirAsync).Dogrula<MaasSatirIstegi>();
        g.MapPost("/{id:guid}/onayla", OnaylaAsync).Islem(YetkiIslemleri.Onayla);
        g.MapPost("/{id:guid}/geri-al", GeriAlAsync).Islem(YetkiIslemleri.Onayla);
        g.MapPost("/{id:guid}/ode", OdeAsync).Islem(YetkiIslemleri.Ekle);
        g.MapGet("/{id:guid}/banka-dosyasi", BankaDosyasiAsync);
        grup.MapPost("/odemeler/{id:guid}/iptal", OdemeIptalAsync).Yetki(PersonelYetkileri.Maas, YetkiIslemleri.Iptal);
    }

    internal sealed class AcDogrulayici : AbstractValidator<DonemAcIstegi>
    {
        public AcDogrulayici()
        {
            RuleFor(x => x.Yil).InclusiveBetween(2000, 2100);
            RuleFor(x => x.Ay).InclusiveBetween(1, 12);
        }
    }

    internal sealed class SatirDogrulayici : AbstractValidator<MaasSatirIstegi>
    {
        public SatirDogrulayici()
        {
            RuleFor(x => x.NetMaas).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Prim).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Kesinti).GreaterThanOrEqualTo(0);
            RuleFor(x => x.AvansKesintisi).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Not).MaximumLength(250);
        }
    }

    private static async Task<IResult> ListeleAsync(PersonelDbContext db, CancellationToken ct)
    {
        var donemler = await db.Donemler.AsNoTracking().Include(d => d.Satirlar).OrderByDescending(d => d.Yil).ThenByDescending(d => d.Ay).Take(60).ToListAsync(ct);
        return TypedResults.Ok(donemler.Select(d => new MaasDonemiOzeti(d.Id, d.Yil, d.Ay, d.Durum, d.Satirlar.Count, d.Satirlar.Sum(s => s.Odenecek), d.Satirlar.Sum(s => s.Odenen))).ToList());
    }

    private static async Task<IResult> GetirAsync(Guid id, PersonelDbContext db, CancellationToken ct)
    {
        var d = await db.Donemler.AsNoTracking().Include(x => x.Satirlar).SingleOrDefaultAsync(x => x.Id == id, ct);
        return d is null ? PersonelHatalari.DonemBulunamadi.Problem() : TypedResults.Ok(await DetayAsync(d, db, ct));
    }

    private static async Task<IResult> AcAsync(DonemAcIstegi istek, PersonelDbContext db, CancellationToken ct)
    {
        if (await db.Donemler.AnyAsync(d => d.Yil == istek.Yil && d.Ay == istek.Ay, ct))
        {
            return PersonelHatalari.DonemVar.Problem();
        }

        var calisanlar = (await db.TumPersoneller.AsNoTracking().ToListAsync(ct)).Where(p => MaasHesabi.DonemdeCalisiyorMu(p.IseGiris, p.IstenCikis, istek.Yil, istek.Ay)).ToList();
        if (calisanlar.Count == 0)
        {
            return PersonelHatalari.PersonelYok.Problem();
        }

        // Önceki taslak dönemlerin kesintisi hesaba katılmaz: aynı anda tek taslak olması beklenir, onayda yeniden denetlenir.
        var avanslar = (await db.Avanslar.AsNoTracking().Where(a => !a.IptalMi && a.Kalan > 0).ToListAsync(ct)).ToLookup(a => a.PersonelId);
        var donem = MaasDonemi.Olustur(istek.Yil, istek.Ay);
        foreach (var p in calisanlar)
        {
            var taksit = avanslar[p.Id].Sum(a => MaasHesabi.Taksit(a.Tutar, a.TaksitSayisi, a.Kalan));
            donem.SatirEkle(MaasSatiri.Olustur(donem.Id, p.Id, p.IsyeriId, p.NetMaas, taksit));
        }

        db.Donemler.Add(donem);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/personel/donemler/{donem.Id}", await DetayAsync(donem, db, ct));
    }

    private static async Task<IResult> SilAsync(Guid id, PersonelDbContext db, CancellationToken ct)
    {
        var d = await db.Donemler.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (d is null)
        {
            return PersonelHatalari.DonemBulunamadi.Problem();
        }

        if (d.Durum != MaasDonemiDurumu.Taslak)
        {
            return PersonelHatalari.DonemTaslakDegil.Problem();
        }

        db.Donemler.Remove(d);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> SatirAsync(Guid id, Guid satirId, MaasSatirIstegi istek, PersonelDbContext db, CancellationToken ct)
    {
        var d = await db.Donemler.Include(x => x.Satirlar).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (d is null)
        {
            return PersonelHatalari.DonemBulunamadi.Problem();
        }

        if (d.Durum != MaasDonemiDurumu.Taslak)
        {
            return PersonelHatalari.DonemTaslakDegil.Problem();
        }

        var satir = d.Satirlar.FirstOrDefault(s => s.Id == satirId);
        if (satir is null)
        {
            return PersonelHatalari.SatirBulunamadi.Problem();
        }

        var acik = await db.Avanslar.Where(a => a.PersonelId == satir.PersonelId && !a.IptalMi).SumAsync(a => a.Kalan, ct);
        if (istek.AvansKesintisi > acik)
        {
            return PersonelHatalari.AvansFazla.Problem();
        }

        var sonuc = satir.Guncelle(istek);
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await DetayAsync(d, db, ct));
    }

    // Onayda avans kesintileri en eski avanstan düşülür; tümü tek işlemdir.
    private static async Task<IResult> OnaylaAsync(Guid id, PersonelDbContext db, IIslemKapsami islemKapsami, CancellationToken ct)
    {
        var sonuc = await islemKapsami.CalistirAsync<MaasDonemi>(async c =>
        {
            var d = await db.Donemler.IgnoreQueryFilters([ModulDbContext.IsyeriFiltresi]).Include(x => x.Satirlar).SingleOrDefaultAsync(x => x.Id == id, c);
            if (d is null)
            {
                return PersonelHatalari.DonemBulunamadi;
            }

            var onay = d.Onayla();
            if (!onay.BasariliMi)
            {
                return onay.Hata;
            }

            var tarih = MaasHesabi.DonemSonu(d.Yil, d.Ay);
            foreach (var s in d.Satirlar.Where(s => s.AvansKesintisi > 0))
            {
                var avanslar = await db.Avanslar.Where(a => a.PersonelId == s.PersonelId && !a.IptalMi && a.Kalan > 0).OrderBy(a => a.Tarih).ToListAsync(c);
                if (avanslar.Sum(a => a.Kalan) < s.AvansKesintisi)
                {
                    return PersonelHatalari.AvansFazla;
                }

                foreach (var (avansId, tutar) in MaasHesabi.Dagit(s.AvansKesintisi, avanslar.Select(a => (a.Id, a.Kalan))))
                {
                    avanslar.First(a => a.Id == avansId).Dus(tutar);
                    db.AvansMahsuplari.Add(AvansMahsubu.Olustur(avansId, s, tarih, tutar));
                }
            }

            await db.SaveChangesAsync(c);
            return d;
        }, ct);
        return sonuc.BasariliMi ? TypedResults.Ok(await DetayAsync(sonuc.Deger, db, ct)) : sonuc.Hata.Problem();
    }

    private static async Task<IResult> GeriAlAsync(Guid id, PersonelDbContext db, IIslemKapsami islemKapsami, CancellationToken ct)
    {
        var sonuc = await islemKapsami.CalistirAsync<MaasDonemi>(async c =>
        {
            var d = await db.Donemler.IgnoreQueryFilters([ModulDbContext.IsyeriFiltresi]).Include(x => x.Satirlar).SingleOrDefaultAsync(x => x.Id == id, c);
            if (d is null)
            {
                return PersonelHatalari.DonemBulunamadi;
            }

            var geri = d.GeriAl();
            if (!geri.BasariliMi)
            {
                return geri.Hata;
            }

            var satirIdleri = d.Satirlar.Select(s => s.Id).ToList();
            var mahsuplar = await db.AvansMahsuplari.Where(m => satirIdleri.Contains(m.SatirId)).ToListAsync(c);
            foreach (var m in mahsuplar)
            {
                (await db.Avanslar.SingleAsync(a => a.Id == m.AvansId, c)).GeriEkle(m.Tutar);
            }

            db.AvansMahsuplari.RemoveRange(mahsuplar);
            await db.SaveChangesAsync(c);
            return d;
        }, ct);
        return sonuc.BasariliMi ? TypedResults.Ok(await DetayAsync(sonuc.Deger, db, ct)) : sonuc.Hata.Problem();
    }

    private static async Task<IResult> OdeAsync(Guid id, MaasOdemeIstegi istek, PersonelDbContext db, PersonelOdemeServisi odeme, IIslemKapsami islemKapsami, CancellationToken ct)
    {
        var sonuc = await islemKapsami.CalistirAsync<MaasDonemi>(async c =>
        {
            var d = await db.Donemler.Include(x => x.Satirlar).SingleOrDefaultAsync(x => x.Id == id, c);
            if (d is null)
            {
                return PersonelHatalari.DonemBulunamadi;
            }

            if (d.Durum != MaasDonemiDurumu.Onayli)
            {
                return PersonelHatalari.DonemOnayliDegil;
            }

            var satirlar = d.Satirlar.Where(s => s.Kalan > 0 && (istek.SatirIdleri is not { Count: > 0 } || istek.SatirIdleri.Contains(s.Id))).ToList();
            if (satirlar.Count == 0)
            {
                return PersonelHatalari.OdenecekYok;
            }

            if (istek.Tutar is not null && satirlar.Count != 1)
            {
                return PersonelHatalari.TekSatir;
            }

            foreach (var s in satirlar)
            {
                var tutar = istek.Tutar ?? s.Kalan;
                var ekleme = s.OdemeEkle(tutar);
                if (!ekleme.BasariliMi)
                {
                    return ekleme.Hata;
                }

                var yazim = await odeme.YazAsync(s.PersonelId, PersonelOdemeTuru.Maas, s.Id, istek.Tarih, tutar, istek.HesapId, $"{d.Ay:00}/{d.Yil} maaş ödemesi", c);
                if (!yazim.BasariliMi)
                {
                    return yazim.Hata;
                }
            }

            await db.SaveChangesAsync(c);
            return d;
        }, ct);
        return sonuc.BasariliMi ? TypedResults.Ok(await DetayAsync(sonuc.Deger, db, ct)) : sonuc.Hata.Problem();
    }

    private static async Task<IResult> OdemeIptalAsync(Guid id, PersonelDbContext db, PersonelOdemeServisi servis, IIslemKapsami islemKapsami, CancellationToken ct)
    {
        var sonuc = await islemKapsami.CalistirAsync<Guid>(async c =>
        {
            var o = await db.Odemeler.SingleOrDefaultAsync(x => x.Id == id, c);
            if (o is null)
            {
                return PersonelHatalari.OdemeBulunamadi;
            }

            var iptal = await servis.IptalAsync(o, c);
            if (!iptal.BasariliMi)
            {
                return iptal.Hata;
            }

            if (o.Tur == PersonelOdemeTuru.Maas)
            {
                (await db.MaasSatirlari.IgnoreQueryFilters([ModulDbContext.IsyeriFiltresi]).SingleAsync(s => s.Id == o.KaynakId, c)).OdemeGeriAl(o.Tutar);
            }
            else if (o.Tur == PersonelOdemeTuru.Masraf)
            {
                (await db.Masraflar.IgnoreQueryFilters([ModulDbContext.IsyeriFiltresi]).SingleAsync(m => m.Id == o.KaynakId, c)).OdemeGeriAl();
            }

            await db.SaveChangesAsync(c);
            return o.Id;
        }, ct);
        return sonuc.BasariliMi ? TypedResults.NoContent() : sonuc.Hata.Problem();
    }

    // Bankaya yüklenecek toplu ödeme listesi: yalnız kalanı olan ve IBAN'ı bulunan satırlar.
    private static async Task<IResult> BankaDosyasiAsync(Guid id, PersonelDbContext db, CancellationToken ct)
    {
        var d = await db.Donemler.AsNoTracking().Include(x => x.Satirlar).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (d is null)
        {
            return PersonelHatalari.DonemBulunamadi.Problem();
        }

        var idler = d.Satirlar.Select(s => s.PersonelId).ToList();
        var kisiler = await db.TumPersoneller.AsNoTracking().Where(p => idler.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var satirlar = d.Satirlar.Where(s => s.Kalan > 0).Select(s => (Kisi: kisiler[s.PersonelId], s.Kalan)).OrderBy(x => x.Kisi.Ad)
            .Select(x => new object?[] { x.Kisi.AdSoyad, x.Kisi.Iban ?? "IBAN YOK", x.Kalan, $"{d.Ay:00}/{d.Yil} maaş" }).ToList();
        var icerik = OnMuhasebe.Shared.Infrastructure.Excel.ExcelYazici.Yaz("Maas odemesi",
            [new("Ad soyad", 30), new("IBAN", 34), new("Tutar", 15), new("Açıklama", 25)], satirlar);
        return TypedResults.File(icerik, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"maas-{d.Yil}-{d.Ay:00}.xlsx");
    }

    private static async Task<MaasDonemiDetayi> DetayAsync(MaasDonemi d, PersonelDbContext db, CancellationToken ct)
    {
        var idler = d.Satirlar.Select(s => s.PersonelId).ToList();
        var kisiler = await db.TumPersoneller.AsNoTracking().Where(p => idler.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var acik = await db.Avanslar.AsNoTracking().Where(a => idler.Contains(a.PersonelId) && !a.IptalMi).GroupBy(a => a.PersonelId).Select(g => new { g.Key, Kalan = g.Sum(a => a.Kalan) }).ToDictionaryAsync(x => x.Key, x => x.Kalan, ct);
        return new MaasDonemiDetayi(d.Id, d.Yil, d.Ay, d.Durum,
            d.Satirlar.OrderBy(s => kisiler.GetValueOrDefault(s.PersonelId)?.Ad).Select(s =>
            {
                var k = kisiler.GetValueOrDefault(s.PersonelId);
                return new MaasSatirCevabi(s.Id, s.PersonelId, k?.AdSoyad ?? "", k?.Iban, s.NetMaas, s.Prim, s.Kesinti, s.AvansKesintisi, acik.GetValueOrDefault(s.PersonelId), s.Odenecek, s.Odenen, s.Kalan, s.Not);
            }).ToList(), d.Surum);
    }
}

internal static class AvansIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var g = grup.MapGroup("/avanslar").Yetki(PersonelYetkileri.Avans);
        g.MapGet("", ListeleAsync);
        g.MapPost("", EkleAsync).Dogrula<AvansKaydetIstegi>();
        g.MapPost("/{id:guid}/iptal", IptalAsync).Islem(YetkiIslemleri.Iptal);
    }

    internal sealed class Dogrulayici : AbstractValidator<AvansKaydetIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.PersonelId).NotEmpty();
            RuleFor(x => x.Tutar).GreaterThan(0).WithMessage("Avans tutarı sıfırdan büyük olmalıdır.");
            RuleFor(x => x.TaksitSayisi).InclusiveBetween(1, 36);
            RuleFor(x => x.Aciklama).MaximumLength(250);
        }
    }

    private static async Task<IResult> ListeleAsync(PersonelDbContext db, Guid? personelId, CancellationToken ct)
    {
        var sorgu = from a in db.Avanslar.AsNoTracking() join p in db.Personeller.AsNoTracking() on a.PersonelId equals p.Id select new { a, p.Ad, p.Soyad };
        if (personelId is { } id)
        {
            sorgu = sorgu.Where(x => x.a.PersonelId == id);
        }

        return TypedResults.Ok(await sorgu.OrderByDescending(x => x.a.Tarih).Take(200)
            .Select(x => new AvansCevabi(x.a.Id, x.a.PersonelId, x.Ad + " " + x.Soyad, x.a.Tarih, x.a.Tutar, x.a.TaksitSayisi, x.a.Kalan, x.a.IptalMi, x.a.Aciklama)).ToListAsync(ct));
    }

    private static async Task<IResult> EkleAsync(AvansKaydetIstegi istek, PersonelDbContext db, PersonelOdemeServisi odeme, IIslemKapsami islemKapsami, CancellationToken ct)
    {
        var p = await db.Personeller.AsNoTracking().SingleOrDefaultAsync(x => x.Id == istek.PersonelId, ct);
        if (p is null)
        {
            return PersonelHatalari.Bulunamadi.Problem();
        }

        if (p.IstenCikis is not null)
        {
            return PersonelHatalari.Cikti.Problem();
        }

        var sonuc = await islemKapsami.CalistirAsync<Avans>(async c =>
        {
            var avans = Avans.Olustur(istek);
            db.Avanslar.Add(avans);
            var yazim = await odeme.YazAsync(p.Id, PersonelOdemeTuru.Avans, avans.Id, istek.Tarih, istek.Tutar, istek.HesapId, $"{p.AdSoyad} avans", c);
            if (!yazim.BasariliMi)
            {
                return yazim.Hata;
            }

            await db.SaveChangesAsync(c);
            return avans;
        }, ct);
        return sonuc.BasariliMi
            ? TypedResults.Created($"/api/personel/avanslar/{sonuc.Deger.Id}", new AvansCevabi(sonuc.Deger.Id, p.Id, p.AdSoyad, sonuc.Deger.Tarih, sonuc.Deger.Tutar, sonuc.Deger.TaksitSayisi, sonuc.Deger.Kalan, false, sonuc.Deger.Aciklama))
            : sonuc.Hata.Problem();
    }

    private static async Task<IResult> IptalAsync(Guid id, PersonelDbContext db, PersonelOdemeServisi servis, IIslemKapsami islemKapsami, CancellationToken ct)
    {
        var sonuc = await islemKapsami.CalistirAsync<Guid>(async c =>
        {
            var a = await db.Avanslar.SingleOrDefaultAsync(x => x.Id == id, c);
            if (a is null)
            {
                return PersonelHatalari.AvansBulunamadi;
            }

            var iptal = a.IptalEt();
            if (!iptal.BasariliMi)
            {
                return iptal.Hata;
            }

            var o = await db.Odemeler.SingleOrDefaultAsync(x => x.KaynakId == a.Id && x.Tur == PersonelOdemeTuru.Avans, c);
            if (o is not null)
            {
                var kasa = await servis.IptalAsync(o, c);
                if (!kasa.BasariliMi)
                {
                    return kasa.Hata;
                }
            }

            await db.SaveChangesAsync(c);
            return a.Id;
        }, ct);
        return sonuc.BasariliMi ? TypedResults.NoContent() : sonuc.Hata.Problem();
    }
}

internal static class MasrafIslemleri
{
    private static readonly string OnayYetkisi = PersonelYetkileri.Masraf.Yetki(YetkiIslemleri.Onayla);

    public static void Map(RouteGroupBuilder grup)
    {
        var g = grup.MapGroup("/masraflar").Yetki(PersonelYetkileri.Masraf);
        g.MapGet("", ListeleAsync);
        g.MapGet("/{id:guid}", GetirAsync);
        g.MapPost("", EkleAsync).Dogrula<MasrafKaydetIstegi>();
        g.MapPut("/{id:guid}", GuncelleAsync).Dogrula<MasrafKaydetIstegi>();
        g.MapDelete("/{id:guid}", SilAsync);
        g.MapPost("/{id:guid}/onayla", (Guid id, MasrafKararIstegi istek, PersonelDbContext db, IMevcutKullanici k, IYetkiDenetimi y, TimeProvider z, CancellationToken ct) => KararAsync(id, true, istek, db, k, y, z, ct)).Islem(YetkiIslemleri.Onayla);
        g.MapPost("/{id:guid}/reddet", (Guid id, MasrafKararIstegi istek, PersonelDbContext db, IMevcutKullanici k, IYetkiDenetimi y, TimeProvider z, CancellationToken ct) => KararAsync(id, false, istek, db, k, y, z, ct)).Islem(YetkiIslemleri.Onayla);
        g.MapPost("/{id:guid}/ode", OdeAsync).Islem(YetkiIslemleri.Onayla);

        var b = grup.MapGroup("/benim").Yetki(PersonelYetkileri.Kendi);
        b.MapGet("", BenimAsync);
        b.MapPost("/masraflar", BenimEkleAsync).Dogrula<KendiMasrafIstegi>();
        b.MapPut("/masraflar/{id:guid}", BenimGuncelleAsync).Dogrula<KendiMasrafIstegi>().Islem(YetkiIslemleri.Ekle);
    }

    internal sealed class Dogrulayici : AbstractValidator<MasrafKaydetIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.PersonelId).NotEmpty();
            RuleFor(x => x.Kategori).IsInEnum();
            RuleFor(x => x.Tutar).GreaterThan(0).WithMessage("Tutar sıfırdan büyük olmalıdır.");
            RuleFor(x => x.Aciklama).NotEmpty().WithMessage("Açıklama yazın.").MaximumLength(500);
        }
    }

    internal sealed class KendiDogrulayici : AbstractValidator<KendiMasrafIstegi>
    {
        public KendiDogrulayici()
        {
            RuleFor(x => x.Kategori).IsInEnum();
            RuleFor(x => x.Tutar).GreaterThan(0).WithMessage("Tutar sıfırdan büyük olmalıdır.");
            RuleFor(x => x.Aciklama).NotEmpty().WithMessage("Açıklama yazın.").MaximumLength(500);
        }
    }

    private static async Task<IResult> ListeleAsync(PersonelDbContext db, IMevcutKullanici k, IYetkiDenetimi y, MasrafDurumu? durum, Guid? personelId, CancellationToken ct)
    {
        var sorgu = db.Masraflar.AsNoTracking();
        if (durum is not null)
        {
            sorgu = sorgu.Where(m => m.Durum == durum);
        }

        if (personelId is not null)
        {
            sorgu = sorgu.Where(m => m.PersonelId == personelId);
        }

        return TypedResults.Ok(await CevaplarAsync(await sorgu.OrderByDescending(m => m.Tarih).Take(300).ToListAsync(ct), db, k, y, ct));
    }

    private static async Task<IResult> GetirAsync(Guid id, PersonelDbContext db, IMevcutKullanici k, IYetkiDenetimi y, CancellationToken ct)
    {
        var m = await db.Masraflar.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return m is null ? PersonelHatalari.MasrafBulunamadi.Problem() : TypedResults.Ok((await CevaplarAsync([m], db, k, y, ct))[0]);
    }

    private static async Task<IResult> EkleAsync(MasrafKaydetIstegi istek, PersonelDbContext db, IMevcutKullanici k, IYetkiDenetimi y, CancellationToken ct)
    {
        var p = await db.Personeller.AsNoTracking().SingleOrDefaultAsync(x => x.Id == istek.PersonelId, ct);
        if (p is null)
        {
            return PersonelHatalari.Bulunamadi.Problem();
        }

        var m = MasrafBeyani.Olustur(p.Id, k.KullaniciId, p.IsyeriId, istek with { MasrafMerkeziId = istek.MasrafMerkeziId ?? p.MasrafMerkeziId });
        db.Masraflar.Add(m);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/personel/masraflar/{m.Id}", (await CevaplarAsync([m], db, k, y, ct))[0]);
    }

    private static async Task<IResult> GuncelleAsync(Guid id, MasrafKaydetIstegi istek, PersonelDbContext db, IMevcutKullanici k, IYetkiDenetimi y, CancellationToken ct)
    {
        var m = await db.Masraflar.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (m is null)
        {
            return PersonelHatalari.MasrafBulunamadi.Problem();
        }

        db.Entry(m).Property(x => x.Surum).OriginalValue = istek.Surum;
        var sonuc = m.Guncelle(istek with { PersonelId = m.PersonelId });
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await CevaplarAsync([m], db, k, y, ct))[0]);
    }

    private static async Task<IResult> SilAsync(Guid id, PersonelDbContext db, CancellationToken ct)
    {
        var m = await db.Masraflar.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (m is null)
        {
            return PersonelHatalari.MasrafBulunamadi.Problem();
        }

        if (m.Durum is not (MasrafDurumu.Beyan or MasrafDurumu.Reddedildi))
        {
            return PersonelHatalari.MasrafDegismez.Problem();
        }

        db.Masraflar.Remove(m);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> KararAsync(Guid id, bool onayMi, MasrafKararIstegi istek, PersonelDbContext db, IMevcutKullanici k, IYetkiDenetimi y, TimeProvider zaman, CancellationToken ct)
    {
        var m = await db.Masraflar.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (m is null)
        {
            return PersonelHatalari.MasrafBulunamadi.Problem();
        }

        var personelKullanicisi = await db.TumPersoneller.Where(p => p.Id == m.PersonelId).Select(p => p.KullaniciId).FirstOrDefaultAsync(ct);
        var sonuc = m.Karar(k.KullaniciId!.Value, k.KullaniciAdi ?? "", personelKullanicisi, onayMi, istek.Not, zaman.GetUtcNow().UtcDateTime);
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await CevaplarAsync([m], db, k, y, ct))[0]);
    }

    private static async Task<IResult> OdeAsync(Guid id, MasrafOdemeIstegi istek, PersonelDbContext db, PersonelOdemeServisi odeme, IIslemKapsami islemKapsami, IMevcutKullanici k, IYetkiDenetimi y, CancellationToken ct)
    {
        var sonuc = await islemKapsami.CalistirAsync<MasrafBeyani>(async c =>
        {
            var m = await db.Masraflar.SingleOrDefaultAsync(x => x.Id == id, c);
            if (m is null)
            {
                return PersonelHatalari.MasrafBulunamadi;
            }

            var durum = m.Odendi();
            if (!durum.BasariliMi)
            {
                return durum.Hata;
            }

            var yazim = await odeme.YazAsync(m.PersonelId, PersonelOdemeTuru.Masraf, m.Id, istek.Tarih, m.Tutar, istek.HesapId, $"Masraf ödemesi: {m.Aciklama}", c);
            if (!yazim.BasariliMi)
            {
                return yazim.Hata;
            }

            await db.SaveChangesAsync(c);
            return m;
        }, ct);
        return sonuc.BasariliMi ? TypedResults.Ok((await CevaplarAsync([sonuc.Deger], db, k, y, ct))[0]) : sonuc.Hata.Problem();
    }

    // Kendi kaydı dışında hiçbir personel verisi dönmez; işyeri kısıtı da aranmaz, kişi kendi kartına bakıyor.
    private static async Task<IResult> BenimAsync(PersonelDbContext db, IMevcutKullanici k, IYetkiDenetimi y, CancellationToken ct)
    {
        var id = k.KullaniciId!.Value;
        var p = await db.TumPersoneller.AsNoTracking().FirstOrDefaultAsync(x => x.KullaniciId == id, ct);
        if (p is null)
        {
            return TypedResults.Ok(new BenimBilgim(null, null, []));
        }

        var masraflar = await db.Masraflar.IgnoreQueryFilters([ModulDbContext.IsyeriFiltresi]).AsNoTracking().Where(m => m.PersonelId == p.Id).OrderByDescending(m => m.Tarih).Take(200).ToListAsync(ct);
        return TypedResults.Ok(new BenimBilgim(p.Id, p.AdSoyad, await CevaplarAsync(masraflar, db, k, y, ct)));
    }

    private static async Task<IResult> BenimEkleAsync(KendiMasrafIstegi istek, PersonelDbContext db, IMevcutKullanici k, IYetkiDenetimi y, CancellationToken ct)
    {
        var id = k.KullaniciId!.Value;
        var p = await db.TumPersoneller.AsNoTracking().FirstOrDefaultAsync(x => x.KullaniciId == id && x.IstenCikis == null, ct);
        if (p is null)
        {
            return PersonelHatalari.BagliPersonelYok.Problem();
        }

        var m = MasrafBeyani.Olustur(p.Id, id, p.IsyeriId, new MasrafKaydetIstegi(p.Id, istek.Tarih, istek.Kategori, istek.Tutar, istek.Aciklama, p.MasrafMerkeziId));
        db.Masraflar.Add(m);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/personel/benim/masraflar/{m.Id}", (await CevaplarAsync([m], db, k, y, ct))[0]);
    }

    // Kişi yalnız kendi beyanını, onaylanmadan önce düzeltir.
    private static async Task<IResult> BenimGuncelleAsync(Guid id, KendiMasrafIstegi istek, PersonelDbContext db, IMevcutKullanici k, IYetkiDenetimi y, CancellationToken ct)
    {
        var kullanici = k.KullaniciId!.Value;
        var p = await db.TumPersoneller.AsNoTracking().FirstOrDefaultAsync(x => x.KullaniciId == kullanici, ct);
        var m = p is null ? null : await db.Masraflar.IgnoreQueryFilters([ModulDbContext.IsyeriFiltresi]).SingleOrDefaultAsync(x => x.Id == id && x.PersonelId == p.Id, ct);
        if (m is null)
        {
            return PersonelHatalari.MasrafBulunamadi.Problem();
        }

        db.Entry(m).Property(x => x.Surum).OriginalValue = istek.Surum;
        var sonuc = m.Guncelle(new MasrafKaydetIstegi(p!.Id, istek.Tarih, istek.Kategori, istek.Tutar, istek.Aciklama, m.MasrafMerkeziId));
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await CevaplarAsync([m], db, k, y, ct))[0]);
    }

    private static async Task<List<MasrafCevabi>> CevaplarAsync(List<MasrafBeyani> masraflar, PersonelDbContext db, IMevcutKullanici k, IYetkiDenetimi y, CancellationToken ct)
    {
        var idler = masraflar.Select(m => m.PersonelId).Distinct().ToList();
        var kisiler = await db.TumPersoneller.AsNoTracking().Where(p => idler.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var onayYetkisi = await y.VarMiAsync(OnayYetkisi, ct);
        return masraflar.Select(m =>
        {
            var p = kisiler.GetValueOrDefault(m.PersonelId);
            var kendisi = k.KullaniciId == p?.KullaniciId || k.KullaniciId == m.BeyanEdenKullaniciId;
            return new MasrafCevabi(m.Id, m.PersonelId, p?.AdSoyad ?? "", m.Tarih, m.Kategori, m.Tutar, m.Aciklama, m.MasrafMerkeziId, m.Durum, m.KararVeren, m.KararTarihi, m.Not,
                onayYetkisi && !kendisi && m.Durum == MasrafDurumu.Beyan, m.Surum);
        }).ToList();
    }
}

internal static class ZimmetIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var g = grup.MapGroup("/zimmetler").Yetki(PersonelYetkileri.Zimmet);
        g.MapGet("", ListeleAsync);
        g.MapGet("/{id:guid}", GetirAsync);
        g.MapPost("", EkleAsync).Dogrula<ZimmetKaydetIstegi>();
        g.MapPut("/{id:guid}", GuncelleAsync).Dogrula<ZimmetKaydetIstegi>();
        g.MapDelete("/{id:guid}", SilAsync);
        g.MapPost("/{id:guid}/iade", IadeAsync).Islem(YetkiIslemleri.Duzenle);
    }

    internal sealed class Dogrulayici : AbstractValidator<ZimmetKaydetIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.PersonelId).NotEmpty();
            RuleFor(x => x.Ad).NotEmpty().WithMessage("Zimmet adını yazın.").MaximumLength(200);
            RuleFor(x => x.SeriNo).MaximumLength(100);
            RuleFor(x => x.Bedel).GreaterThanOrEqualTo(0).When(x => x.Bedel is not null);
            RuleFor(x => x.Not).MaximumLength(500);
        }
    }

    private static async Task<IResult> ListeleAsync(PersonelDbContext db, Guid? personelId, ZimmetDurumu? durum, CancellationToken ct)
    {
        var sorgu = from z in db.Zimmetler.AsNoTracking() join p in db.Personeller.AsNoTracking() on z.PersonelId equals p.Id select new { z, p.Ad, p.Soyad };
        if (personelId is { } id)
        {
            sorgu = sorgu.Where(x => x.z.PersonelId == id);
        }

        if (durum is { } d)
        {
            sorgu = sorgu.Where(x => x.z.Durum == d);
        }

        return TypedResults.Ok(await sorgu.OrderByDescending(x => x.z.VerilisTarihi).Take(300)
            .Select(x => new ZimmetCevabi(x.z.Id, x.z.PersonelId, x.Ad + " " + x.Soyad, x.z.Ad, x.z.SeriNo, x.z.VerilisTarihi, x.z.IadeTarihi, x.z.Bedel, x.z.Durum, x.z.Not, x.z.Surum)).ToListAsync(ct));
    }

    private static async Task<IResult> GetirAsync(Guid id, PersonelDbContext db, CancellationToken ct)
    {
        var z = await db.Zimmetler.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return z is null ? PersonelHatalari.ZimmetBulunamadi.Problem() : TypedResults.Ok(await CevapAsync(z, db, ct));
    }

    private static async Task<IResult> EkleAsync(ZimmetKaydetIstegi istek, PersonelDbContext db, CancellationToken ct)
    {
        if (!await db.Personeller.AnyAsync(p => p.Id == istek.PersonelId, ct))
        {
            return PersonelHatalari.Bulunamadi.Problem();
        }

        var z = Zimmet.Olustur(istek);
        db.Zimmetler.Add(z);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/personel/zimmetler/{z.Id}", await CevapAsync(z, db, ct));
    }

    private static async Task<IResult> GuncelleAsync(Guid id, ZimmetKaydetIstegi istek, PersonelDbContext db, CancellationToken ct)
    {
        var z = await db.Zimmetler.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (z is null)
        {
            return PersonelHatalari.ZimmetBulunamadi.Problem();
        }

        if (!await db.Personeller.AnyAsync(p => p.Id == istek.PersonelId, ct))
        {
            return PersonelHatalari.Bulunamadi.Problem();
        }

        db.Entry(z).Property(x => x.Surum).OriginalValue = istek.Surum;
        z.Ayarla(istek);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await CevapAsync(z, db, ct));
    }

    private static async Task<IResult> SilAsync(Guid id, PersonelDbContext db, CancellationToken ct)
    {
        var z = await db.Zimmetler.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (z is null)
        {
            return PersonelHatalari.ZimmetBulunamadi.Problem();
        }

        db.Zimmetler.Remove(z);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> IadeAsync(Guid id, ZimmetIadeIstegi istek, PersonelDbContext db, CancellationToken ct)
    {
        var z = await db.Zimmetler.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (z is null)
        {
            return PersonelHatalari.ZimmetBulunamadi.Problem();
        }

        var sonuc = z.Kapat(istek);
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await CevapAsync(z, db, ct));
    }

    private static async Task<ZimmetCevabi> CevapAsync(Zimmet z, PersonelDbContext db, CancellationToken ct)
    {
        var ad = await db.TumPersoneller.Where(p => p.Id == z.PersonelId).Select(p => p.Ad + " " + p.Soyad).FirstOrDefaultAsync(ct);
        return new ZimmetCevabi(z.Id, z.PersonelId, ad ?? "", z.Ad, z.SeriNo, z.VerilisTarihi, z.IadeTarihi, z.Bedel, z.Durum, z.Not, z.Surum);
    }
}
