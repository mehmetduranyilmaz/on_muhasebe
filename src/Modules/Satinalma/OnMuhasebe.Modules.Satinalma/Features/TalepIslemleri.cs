using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.Satinalma.Contracts;
using OnMuhasebe.Modules.Satinalma.Domain;
using OnMuhasebe.Modules.Satinalma.Infrastructure;
using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Modules.Yonetim.Contracts;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Contracts.Sayfalama;
using OnMuhasebe.Shared.Contracts.Vergi;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Bildirimler;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Satinalma.Features;

// Talep yaşam döngüsü: taslak, onaya gönder, basamak basamak karar, siparişe dönüşüm. Muhasebe ya da stok etkisi yoktur.
internal static class TalepIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var g = grup.MapGroup("/talepler").Yetki(SatinalmaYetkileri.Talep);
        g.MapGet("", ListeleAsync);
        g.MapGet("/bekleyenler", BekleyenlerAsync).Yetki(SatinalmaYetkileri.Onay);
        g.MapGet("/{id:guid}", DetayAsync);
        g.MapPost("", EkleAsync).Dogrula<TalepKaydetIstegi>();
        g.MapPut("/{id:guid}", GuncelleAsync).Dogrula<TalepKaydetIstegi>();
        g.MapDelete("/{id:guid}", SilAsync);
        g.MapPost("/{id:guid}/gonder", GonderAsync).Islem(YetkiIslemleri.Ekle);
        g.MapPost("/{id:guid}/iptal", IptalAsync).Islem(YetkiIslemleri.Iptal);
        g.MapPost("/{id:guid}/onayla", (Guid id, TalepKararIstegi istek, [AsParameters] KararBagimliliklari b, CancellationToken ct) => KararAsync(id, true, istek, b, ct))
            .Yetki(SatinalmaYetkileri.Onay, YetkiIslemleri.Onayla);
        g.MapPost("/{id:guid}/reddet", (Guid id, TalepKararIstegi istek, [AsParameters] KararBagimliliklari b, CancellationToken ct) => KararAsync(id, false, istek, b, ct))
            .Yetki(SatinalmaYetkileri.Onay, YetkiIslemleri.Onayla);
        g.MapPost("/{id:guid}/siparise-donustur", DonusturAsync).Islem(YetkiIslemleri.Duzenle);
    }

    internal sealed class Dogrulayici : AbstractValidator<TalepKaydetIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.Gerekce).NotEmpty().WithMessage("Talebin gerekçesini yazın.").MaximumLength(1000);
            RuleFor(x => x.Kalemler).NotNull().Must(k => k is null || k.Count <= 100).WithMessage("Bir talepte en çok 100 kalem olabilir.");
            RuleForEach(x => x.Kalemler).ChildRules(k =>
            {
                k.RuleFor(x => x.Miktar).GreaterThan(0).WithMessage("Miktar sıfırdan büyük olmalıdır.");
                k.RuleFor(x => x.TahminiBirimFiyat).GreaterThanOrEqualTo(0);
                k.RuleFor(x => x.KdvOrani).Must(KdvOranlari.GecerliMi).WithMessage("KDV oranı %0, %1, %10 veya %20 olmalıdır.");
                k.RuleFor(x => x.Ad).MaximumLength(250);
                k.RuleFor(x => x.Birim).MaximumLength(20);
            });
        }
    }

    internal sealed class SiparisDogrulayici : AbstractValidator<SipariseDonusturIstegi>
    {
        public SiparisDogrulayici() => RuleFor(x => x.TedarikciId).NotEmpty().WithMessage("Tedarikçi seçin.");
    }

    // Karar uçlarının ortak bağımlılıkları.
    internal sealed record KararBagimliliklari(
        SatinalmaDbContext Db, IFirmaRehberi Rehber, IYetkiDenetimi Yetki, IMevcutKullanici Kullanici, TimeProvider Zaman,
        IEnumerable<IBildirimTetikleyici> Tetikleyiciler, ISatinalmaSiparisiServisi Siparis, ILisansSorgusu Lisans);

    private static async Task<IResult> ListeleAsync(
        SatinalmaDbContext db, TimeProvider zaman, string? arama, TalepDurumu? durum, Guid? isyeriId, int? sayfa, int? boyut, CancellationToken ct)
    {
        var sorgu = db.Talepler.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(arama))
        {
            var desen = AramaDeseni.Icerir(arama);
            sorgu = sorgu.Where(t => EF.Functions.ILike(t.Kod, desen) || EF.Functions.ILike(t.Gerekce, desen) || EF.Functions.ILike(t.TalepEden, desen));
        }

        if (durum is not null)
        {
            sorgu = sorgu.Where(t => t.Durum == durum);
        }

        if (isyeriId is not null)
        {
            sorgu = sorgu.Where(t => t.IsyeriId == isyeriId);
        }

        return TypedResults.Ok(await sorgu.OrderByDescending(t => t.Tarih).ThenByDescending(t => t.Kod)
            .Select(t => new TalepOzeti(t.Id, t.Kod, t.TalepEden, t.Tarih, t.GerekliTarih, t.Gerekce, t.Durum, t.Toplam,
                t.Durum == TalepDurumu.Onayda
                    ? t.Onaylar.Where(o => o.GonderimNo == t.GonderimNo && o.Durum == OnayDurumu.Bekliyor).OrderBy(o => o.Sira).Select(o => o.Onaylayan).FirstOrDefault()
                    : null,
                t.IsyeriId))
            .SayfalaAsync(new SayfalamaIstegi(sayfa ?? 1, boyut ?? SayfalamaIstegi.VarsayilanBoyut), ct));
    }

    // Sırası bana gelen talepler; kendi talebim listelenmez.
    private static async Task<IResult> BekleyenlerAsync(SatinalmaDbContext db, IFirmaRehberi rehber, IYetkiDenetimi yetki, IMevcutKullanici kullanici, CancellationToken ct)
    {
        var (benimId, rolId, tamYetkili) = await KimlikAsync(rehber, yetki, kullanici, ct);
        var talepler = await db.Talepler.AsNoTracking().Include(t => t.Onaylar).Where(t => t.Durum == TalepDurumu.Onayda && t.TalepEdenId != benimId)
            .OrderBy(t => t.Tarih).ToListAsync(ct);
        return TypedResults.Ok(talepler
            .Where(t => t.BekleyenOnay is { } s && OnayKurallari.YetkiliMi(s.RolId, s.KullaniciId, benimId, rolId, tamYetkili))
            .Select(t => new TalepOzeti(t.Id, t.Kod, t.TalepEden, t.Tarih, t.GerekliTarih, t.Gerekce, t.Durum, t.Toplam, t.BekleyenOnay!.Onaylayan, t.IsyeriId))
            .ToList());
    }

    private static async Task<IResult> DetayAsync(
        Guid id, SatinalmaDbContext db, IFirmaRehberi rehber, IYetkiDenetimi yetki, IMevcutKullanici kullanici, ISatinalmaSiparisiServisi siparis, ILisansSorgusu lisans, CancellationToken ct)
    {
        var talep = await db.Talepler.AsNoTracking().Include(t => t.Kalemler).Include(t => t.Onaylar).SingleOrDefaultAsync(t => t.Id == id, ct);
        if (talep is null)
        {
            return TalepHatalari.Bulunamadi.Problem();
        }

        return TypedResults.Ok(await DetayOlusturAsync(talep, rehber, yetki, kullanici, siparis, lisans, ct));
    }

    private static async Task<IResult> EkleAsync(
        TalepKaydetIstegi istek, SatinalmaDbContext db, IFirmaRehberi rehber, IYetkiDenetimi yetki, IMevcutKullanici kullanici, IStokSorgusu stok, IKasaBankaSorgusu kasa, IsyeriSecici isyeriSecici,
        ISatinalmaSiparisiServisi siparis, ILisansSorgusu lisans, CancellationToken ct)
    {
        var denetim = await DenetleAsync(istek, null, kullanici, stok, kasa, isyeriSecici, lisans, ct);
        if (!denetim.BasariliMi)
        {
            return denetim.Hata.Problem();
        }

        var kod = await KodUretici.SonrakiAsync(db, "satinalma.satinalma_talebi", "TL", kullanici.FirmaId!.Value, ct);
        var sonuc = SatinalmaTalebi.Olustur(kod, kullanici.KullaniciId!.Value, kullanici.KullaniciAdi ?? "", istek with { Kalemler = denetim.Deger });
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        db.Talepler.Add(sonuc.Deger);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/satinalma/talepler/{sonuc.Deger.Id}", await DetayOlusturAsync(sonuc.Deger, rehber, yetki, kullanici, siparis, lisans, ct));
    }

    private static async Task<IResult> GuncelleAsync(
        Guid id, TalepKaydetIstegi istek, SatinalmaDbContext db, IFirmaRehberi rehber, IYetkiDenetimi yetki, IMevcutKullanici kullanici, IStokSorgusu stok, IKasaBankaSorgusu kasa, IsyeriSecici isyeriSecici,
        ISatinalmaSiparisiServisi siparis, ILisansSorgusu lisans, CancellationToken ct)
    {
        var talep = await db.Talepler.Include(t => t.Kalemler).Include(t => t.Onaylar).SingleOrDefaultAsync(t => t.Id == id, ct);
        if (talep is null)
        {
            return TalepHatalari.Bulunamadi.Problem();
        }

        var denetim = await DenetleAsync(istek, talep, kullanici, stok, kasa, isyeriSecici, lisans, ct);
        if (!denetim.BasariliMi)
        {
            return denetim.Hata.Problem();
        }

        db.Entry(talep).Property(t => t.Surum).OriginalValue = istek.Surum;
        db.RemoveRange(talep.Kalemler);
        var sonuc = talep.Guncelle(istek with { Kalemler = denetim.Deger });
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await DetayOlusturAsync(talep, rehber, yetki, kullanici, siparis, lisans, ct));
    }

    private static async Task<IResult> SilAsync(Guid id, SatinalmaDbContext db, CancellationToken ct)
    {
        var talep = await db.Talepler.SingleOrDefaultAsync(t => t.Id == id, ct);
        if (talep is null)
        {
            return TalepHatalari.Bulunamadi.Problem();
        }

        if (talep.Durum is not (TalepDurumu.Taslak or TalepDurumu.Reddedildi or TalepDurumu.Iptal))
        {
            return TalepHatalari.Degistirilemez.Problem();
        }

        db.Talepler.Remove(talep);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    // Kural yoksa firma yöneticisinin onayı aranır. Bir basamakta talep edenden başka karar verecek kimse yoksa talep yola çıkmaz: yolda kilitlenmesin.
    private static async Task<IResult> GonderAsync(
        Guid id, SatinalmaDbContext db, IFirmaRehberi rehber, IYetkiDenetimi yetki, IMevcutKullanici kullanici, OnaycilarServisi onaycilar, ISatinalmaSiparisiServisi siparis, ILisansSorgusu lisans,
        IEnumerable<IBildirimTetikleyici> tetikleyiciler, CancellationToken ct)
    {
        var talep = await db.Talepler.Include(t => t.Kalemler).Include(t => t.Onaylar).SingleOrDefaultAsync(t => t.Id == id, ct);
        if (talep is null)
        {
            return TalepHatalari.Bulunamadi.Problem();
        }

        var firmaId = kullanici.FirmaId!.Value;
        var kurallar = await db.OnayBasamaklari.AsNoTracking().OrderBy(b => b.Sira).ToListAsync(ct);
        var uygulanan = OnayKurallari.Uygulanan(kurallar, talep.Toplam);
        if (uygulanan.Count == 0)
        {
            var yonetici = (await rehber.RollerAsync(firmaId, ct)).First(r => r.TamYetkiliMi);
            uygulanan = [OnayBasamagi.Olustur(1, 0, yonetici.Id, null, yonetici.Ad)];
        }

        foreach (var b in uygulanan)
        {
            if ((await onaycilar.AdaylarAsync(firmaId, b.RolId, b.KullaniciId, talep.TalepEdenId, ct)).Count == 0)
            {
                return TalepHatalari.OnaylayanYok(b.Ad).Problem();
            }
        }

        var sonuc = talep.OnayaGonder(uygulanan);
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        await db.SaveChangesAsync(ct);
        await TetikleAsync(tetikleyiciler, firmaId, ct);
        return TypedResults.Ok(await DetayOlusturAsync(talep, rehber, yetki, kullanici, siparis, lisans, ct));
    }

    private static async Task<IResult> KararAsync(Guid id, bool onayMi, TalepKararIstegi istek, KararBagimliliklari b, CancellationToken ct)
    {
        var talep = await b.Db.Talepler.Include(t => t.Kalemler).Include(t => t.Onaylar).SingleOrDefaultAsync(t => t.Id == id, ct);
        if (talep is null)
        {
            return TalepHatalari.Bulunamadi.Problem();
        }

        var (benimId, rolId, tamYetkili) = await KimlikAsync(b.Rehber, b.Yetki, b.Kullanici, ct);
        var yetkiliMi = talep.BekleyenOnay is { } s && OnayKurallari.YetkiliMi(s.RolId, s.KullaniciId, benimId, rolId, tamYetkili);
        var sonuc = talep.Karar(benimId, b.Kullanici.KullaniciAdi ?? "", yetkiliMi, onayMi, istek.Not, b.Zaman.GetUtcNow().UtcDateTime);
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        await b.Db.SaveChangesAsync(ct);
        await TetikleAsync(b.Tetikleyiciler, b.Kullanici.FirmaId!.Value, ct);
        return TypedResults.Ok(await DetayOlusturAsync(talep, b.Rehber, b.Yetki, b.Kullanici, b.Siparis, b.Lisans, ct));
    }

    private static async Task<IResult> IptalAsync(
        Guid id, SatinalmaDbContext db, IFirmaRehberi rehber, IYetkiDenetimi yetki, IMevcutKullanici kullanici, ISatinalmaSiparisiServisi siparis, ILisansSorgusu lisans, CancellationToken ct)
    {
        var talep = await db.Talepler.Include(t => t.Kalemler).Include(t => t.Onaylar).SingleOrDefaultAsync(t => t.Id == id, ct);
        if (talep is null)
        {
            return TalepHatalari.Bulunamadi.Problem();
        }

        var sonuc = talep.IptalEt();
        if (!sonuc.BasariliMi)
        {
            return sonuc.HttpSonucu();
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await DetayOlusturAsync(talep, rehber, yetki, kullanici, siparis, lisans, ct));
    }

    // Taslak satınalma siparişi açılır, tek işlemde talebe bağlanır. Sipariş düzenlenip onaylanması Fatura modülünde sürer.
    private static async Task<IResult> DonusturAsync(
        Guid id, SipariseDonusturIstegi istek, SatinalmaDbContext db, IFirmaRehberi rehber, IYetkiDenetimi yetki, IMevcutKullanici kullanici, ISatinalmaSiparisiServisi siparis, ILisansSorgusu lisans,
        IIslemKapsami islemKapsami, TimeProvider zaman, CancellationToken ct)
    {
        var moduller = (await lisans.GetirAsync(kullanici.FirmaId!.Value, ct))?.Moduller ?? [];
        if (!moduller.Contains(ModulKodlari.Fatura))
        {
            return TalepHatalari.FaturaLisansiYok.Problem();
        }

        if (!await yetki.VarMiAsync("fatura.teklif.ekle", ct))
        {
            return TalepHatalari.SiparisYetkisiYok.Problem();
        }

        var sonuc = await islemKapsami.CalistirAsync<SatinalmaTalebi>(async c =>
        {
            var talep = await db.Talepler.Include(t => t.Kalemler).Include(t => t.Onaylar).SingleOrDefaultAsync(t => t.Id == id, c);
            if (talep is null)
            {
                return TalepHatalari.Bulunamadi;
            }

            var mevcut = talep.SiparisId is { } siparisId ? await siparis.GetirAsync(siparisId, c) : null;
            var uygunluk = talep.SipariseDonusebilirMi(mevcut is not null && mevcut.Durum != TeklifSiparisDurumu.Iptal);
            if (!uygunluk.BasariliMi)
            {
                return uygunluk.Hata;
            }

            var satirlar = talep.Kalemler.Select(k => new FaturaSatiriIstegi(k.StokKartiId, k.Ad, null, k.Birim, k.Miktar, k.TahminiBirimFiyat, false, k.KdvOrani, 0, 0, 0)).ToList();
            var acilan = await siparis.TaslakAcAsync(new SatinalmaSiparisiTaslagi(istek.TedarikciId, DateOnly.FromDateTime(zaman.GetLocalNow().DateTime), istek.TerminTarihi ?? talep.GerekliTarih,
                talep.IsyeriId, $"Satın alma talebi {talep.Kod}: {talep.Gerekce}", satirlar), c);
            if (!acilan.BasariliMi)
            {
                return acilan.Hata;
            }

            talep.SipariseDonustu(acilan.Deger);
            await db.SaveChangesAsync(c);
            return talep;
        }, ct);

        return sonuc.BasariliMi
            ? TypedResults.Ok(await DetayOlusturAsync(sonuc.Deger, rehber, yetki, kullanici, siparis, lisans, ct))
            : sonuc.Hata.Problem();
    }

    private static async Task TetikleAsync(IEnumerable<IBildirimTetikleyici> tetikleyiciler, Guid firmaId, CancellationToken ct)
    {
        foreach (var t in tetikleyiciler)
        {
            await t.FirmaIcinAsync(firmaId, ct);
        }
    }

    private static async Task<(Guid Id, Guid? RolId, bool TamYetkili)> KimlikAsync(IFirmaRehberi rehber, IYetkiDenetimi yetki, IMevcutKullanici kullanici, CancellationToken ct)
    {
        var id = kullanici.KullaniciId!.Value;
        var benim = (await rehber.KullaniciRolleriAsync(kullanici.FirmaId!.Value, ct)).FirstOrDefault(k => k.KullaniciId == id);
        return (id, benim?.RolId, benim?.TamYetkiliMi ?? (await yetki.GetirAsync(ct)).TamYetkiliMi);
    }

    private static async Task<Sonuc<List<TalepKalemiIstegi>>> DenetleAsync(
        TalepKaydetIstegi istek, SatinalmaTalebi? mevcut, IMevcutKullanici kullanici, IStokSorgusu stok, IKasaBankaSorgusu kasa, IsyeriSecici isyeriSecici, ILisansSorgusu lisans, CancellationToken ct)
    {
        if (istek.IsyeriId is { } isyeri && isyeri != mevcut?.IsyeriId)
        {
            var secim = await isyeriSecici.BelirleAsync(isyeri, ct);
            if (!secim.BasariliMi)
            {
                return secim.Hata;
            }
        }

        if (istek.MasrafMerkeziId is { } merkez && merkez != mevcut?.MasrafMerkeziId && !await kasa.MasrafMerkeziAktifMiAsync(merkez, ct))
        {
            return TalepHatalari.MasrafMerkeziGecersiz;
        }

        var kartIdleri = istek.Kalemler.Where(k => k.StokKartiId is not null).Select(k => k.StokKartiId!.Value).Distinct().ToList();
        var kartlar = new Dictionary<Guid, StokKartiBilgisi>();
        if (kartIdleri.Count > 0)
        {
            if (!((await lisans.GetirAsync(kullanici.FirmaId!.Value, ct))?.Moduller ?? []).Contains(ModulKodlari.Stok))
            {
                return TalepHatalari.StokLisansiYok;
            }

            kartlar = (await stok.KartlariGetirAsync(kartIdleri, ct)).ToDictionary(k => k.Id);
        }

        var sonuc = new List<TalepKalemiIstegi>();
        foreach (var k in istek.Kalemler)
        {
            if (k.StokKartiId is not { } kartId)
            {
                if (string.IsNullOrWhiteSpace(k.Ad) || string.IsNullOrWhiteSpace(k.Birim))
                {
                    return TalepHatalari.KalemAdiGerekli;
                }

                sonuc.Add(k);
                continue;
            }

            if (!kartlar.TryGetValue(kartId, out var kart) || !kart.AktifMi)
            {
                return TalepHatalari.StokKartiGecersiz;
            }

            var birim = string.IsNullOrWhiteSpace(k.Birim) ? kart.TemelBirim : k.Birim.Trim();
            if (!kart.BirimTanimliMi(birim))
            {
                return TalepHatalari.StokBirimGecersiz;
            }

            sonuc.Add(k with { Ad = kart.Ad, Birim = birim });
        }

        return sonuc;
    }

    private static async Task<TalepDetayi> DetayOlusturAsync(
        SatinalmaTalebi t, IFirmaRehberi rehber, IYetkiDenetimi yetki, IMevcutKullanici kullanici, ISatinalmaSiparisiServisi siparis, ILisansSorgusu lisans, CancellationToken ct)
    {
        var (benimId, rolId, tamYetkili) = await KimlikAsync(rehber, yetki, kullanici, ct);
        var onaylayabilir = benimId != t.TalepEdenId && t.BekleyenOnay is { } s && OnayKurallari.YetkiliMi(s.RolId, s.KullaniciId, benimId, rolId, tamYetkili)
                            && await yetki.VarMiAsync(OnaycilarServisi.OnayYetkisi, ct);

        SatinalmaSiparisiOzeti? sip = null;
        if (t.SiparisId is { } siparisId && ((await lisans.GetirAsync(kullanici.FirmaId!.Value, ct))?.Moduller ?? []).Contains(ModulKodlari.Fatura))
        {
            sip = await siparis.GetirAsync(siparisId, ct);
        }

        var uyari = sip is { } o && o.TutarTl > t.Toplam ? "Sipariş tutarı onaylanan talep tutarını aşıyor." : null;
        return new TalepDetayi(t.Id, t.Kod, t.TalepEdenId, t.TalepEden, t.Tarih, t.GerekliTarih, t.Gerekce, t.IsyeriId, t.MasrafMerkeziId, t.Durum, t.Toplam,
            t.Kalemler.Select(k => new TalepKalemiCevabi(k.StokKartiId, k.Ad, k.Birim, k.Miktar, k.TahminiBirimFiyat, k.KdvOrani, k.Tutar)).ToList(),
            t.Onaylar.OrderBy(o => o.GonderimNo).ThenBy(o => o.Sira).Select(o => new TalepOnayCevabi(o.GonderimNo, o.Sira, o.Onaylayan, o.Durum, o.KararVeren, o.KararTarihi, o.Not)).ToList(),
            onaylayabilir, t.SiparisId, sip?.Numara, sip?.TutarTl, uyari, t.Surum);
    }
}
