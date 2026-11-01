using OnMuhasebe.Shared.Contracts.Muhasebe;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.KasaBanka.Domain;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.KasaBanka.Infrastructure;

internal sealed class KasaKodUretici(KasaBankaDbContext db, IMevcutKullanici mevcutKullanici)
{
    public Task<string> SonrakiAsync(HesapTuru tur, CancellationToken cancellationToken) =>
        KodUretici.SonrakiAsync(db, "kasabanka.kasa_hesabi", KasaHesabi.KodOneki(tur), mevcutKullanici.FirmaId!.Value, cancellationToken);
}

internal sealed class KategoriServisi(KasaBankaDbContext db)
{
    // Firmanın hiç kategorisi yoksa başlangıç kategorileri açılır; kullanıcı sonra düzenler.
    public async Task VarsayilanlariHazirlaAsync(CancellationToken cancellationToken)
    {
        if (await db.Kategoriler.AnyAsync(cancellationToken))
        {
            return;
        }

        db.Kategoriler.AddRange(GelirGiderKategorisi.Varsayilanlar.Select(k => GelirGiderKategorisi.Olustur(k.Ad, k.Tur)));
        await db.SaveChangesAsync(cancellationToken);
    }
}

internal sealed class KasaBankaSorgusu(KasaBankaDbContext db) : IKasaBankaSorgusu
{
    public async Task<IReadOnlyList<MuhasebeKarti>> HesapKartlariAsync(CancellationToken cancellationToken = default) =>
        await db.Hesaplar.IgnoreQueryFilters([ModulDbContext.SilinmeFiltresi, ModulDbContext.IsyeriFiltresi]).AsNoTracking()
            .OrderBy(h => h.Kod)
            .Select(h => new MuhasebeKarti(h.Id, h.Kod, h.Ad, h.Tur.ToString()))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MuhasebeKarti>> KategoriKartlariAsync(CancellationToken cancellationToken = default) =>
        await db.Kategoriler.AsNoTracking()
            .OrderBy(k => k.Tur).ThenBy(k => k.Ad)
            .Select(k => new MuhasebeKarti(k.Id, "", k.Ad,
                k.Tur == GelirGiderTuru.Gider && k.Ad == KrediServisi.GiderKategorisi ? "Finansman" : k.Tur.ToString()))
            .ToListAsync(cancellationToken);

    // Hesap işyeri kısıtı geçerlidir (kısıtlı kullanıcı yalnız kendi işyerinin hareketlerini görür); silinmiş hesabın hareketi de gelir.
    public async Task<IReadOnlyList<MuhasebeKasaHareketi>> MuhasebeHareketleriAsync(
        DateOnly baslangic, DateOnly bitis, Guid? isyeriId, CancellationToken cancellationToken = default)
    {
        var satirlar = await (
                from h in db.KasaHareketleri.AsNoTracking()
                join hesap in db.Hesaplar.IgnoreQueryFilters([ModulDbContext.SilinmeFiltresi]) on h.HesapId equals hesap.Id
                where h.Tarih >= baslangic && h.Tarih <= bitis && h.Tur != KasaHareketTuru.Acilis
                      && h.IptalHareketId == null && h.IptalEdilenHareketId == null
                      && (isyeriId == null || hesap.IsyeriId == isyeriId)
                orderby h.Tarih, h.IslemId, h.Id
                select new { h, hesap.IsyeriId })
            .ToListAsync(cancellationToken);

        var evrakIdleri = satirlar.Where(s => s.h.Tur is KasaHareketTuru.CekSenetTahsil or KasaHareketTuru.CekSenetOdeme && s.h.KaynakBelgeId != null)
            .Select(s => s.h.KaynakBelgeId!.Value).Distinct().ToList();
        var evrakTurleri = await db.Evraklar.IgnoreQueryFilters([ModulDbContext.SilinmeFiltresi, ModulDbContext.IsyeriFiltresi]).AsNoTracking()
            .Where(e => evrakIdleri.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Tur, cancellationToken);

        // Karşılıksızdan tahsilde evrak portföyde değildir: karşılıksızla cariye geri yazılan alacak kapanır (karşı hesap cari).
        var tahsilIslemleri = satirlar.Where(s => s.h.Tur == KasaHareketTuru.CekSenetTahsil).Select(s => s.h.IslemId).ToList();
        var karsiliksizdanTahsiller = (await db.EvrakHareketleri.AsNoTracking()
                .Where(e => tahsilIslemleri.Contains(e.IslemId) && e.Islem == EvrakIslemi.KarsiliksizdanTahsil)
                .Select(e => e.IslemId).ToListAsync(cancellationToken))
            .ToHashSet();

        // Taksit tek kasa çıkışıdır; faiz ve vergiler aynı işlemin ödenmiş gider kaydındadır.
        var taksitIslemleri = satirlar.Where(s => s.h.Tur == KasaHareketTuru.KrediOdeme).Select(s => s.h.IslemId).ToList();
        var taksitGiderleri = (await db.GelirGiderKayitlari.AsNoTracking()
                .Where(g => taksitIslemleri.Contains(g.IslemId) && g.Bicim == GelirGiderBicimi.Odendi && g.IptalTarihi == null)
                .Select(g => new { g.IslemId, g.TutarTl, g.KategoriId })
                .ToListAsync(cancellationToken))
            .GroupBy(g => g.IslemId)
            .ToDictionary(g => g.Key, g => (Tutar: g.Sum(x => x.TutarTl), KategoriId: (Guid?)g.First().KategoriId));

        return satirlar.Select(s =>
        {
            var h = s.h;
            var karsiRol = h.Tur switch
            {
                KasaHareketTuru.CekSenetTahsil when karsiliksizdanTahsiller.Contains(h.IslemId) => null,
                KasaHareketTuru.CekSenetTahsil => Evrak(h.KaynakBelgeId) == EvrakTuru.Senet ? "AlacakSenetleri" : "AlinanCekler",
                KasaHareketTuru.CekSenetOdeme => Evrak(h.KaynakBelgeId) == EvrakTuru.Senet ? "BorcSenetleri" : "VerilenCekler",
                KasaHareketTuru.KrediKullanim or KasaHareketTuru.KrediOdeme => "BankaKredileri",
                _ => null
            };
            var gider = h.Tur == KasaHareketTuru.KrediOdeme && taksitGiderleri.TryGetValue(h.IslemId, out var g) ? g : default;
            return new MuhasebeKasaHareketi(h.Id, h.IslemId, h.Tarih, h.Tur.ToString(), h.HesapId, h.GirisTl, h.CikisTl, h.CariHesapId,
                h.KategoriId, karsiRol, h.BelgeNo, h.Aciklama, s.IsyeriId, gider.Tutar, gider.KategoriId);
        }).ToList();

        EvrakTuru? Evrak(Guid? id) => id is { } e && evrakTurleri.TryGetValue(e, out var tur) ? tur : null;
    }

    // Para hareketi olan işlemler (tahsil, ödeme) kasa hareketinden gelir; burada yalnız cari ile portföy arasındaki kayıtlar var.
    private static readonly EvrakIslemi[] _muhasebeIslemleri =
    [
        EvrakIslemi.Giris, EvrakIslemi.Cikis, EvrakIslemi.Ciro, EvrakIslemi.CirodanGeriAlma, EvrakIslemi.CirodanKarsiliksiz,
        EvrakIslemi.Karsiliksiz, EvrakIslemi.Iade, EvrakIslemi.VerilenIade, EvrakIslemi.VerilenKarsiliksiz
    ];

    public async Task<IReadOnlyList<MuhasebeEvrakHareketi>> MuhasebeEvrakHareketleriAsync(
        DateOnly baslangic, DateOnly bitis, CancellationToken cancellationToken = default)
    {
        var satirlar = await (
                from h in db.EvrakHareketleri.AsNoTracking()
                join e in db.Evraklar.IgnoreQueryFilters([ModulDbContext.SilinmeFiltresi]) on h.EvrakId equals e.Id
                join b in db.EvrakBordrolari on h.BordroId equals b.Id
                where h.Tarih >= baslangic && h.Tarih <= bitis && !h.GeriAlindi && _muhasebeIslemleri.Contains(h.Islem)
                orderby h.Tarih, b.Kod, h.Id
                select new
                {
                    h.Id, h.BordroId, BordroNo = b.Kod, h.Tarih, h.Islem, e.Tur, e.Kod, e.SeriNo, e.VadeTarihi, e.Tutar, e.Kur, EvrakCariId = e.CariHesapId,
                    IslemCariId = h.CariHesapId, h.OncekiCiroCariId, h.Aciklama
                })
            .ToListAsync(cancellationToken);

        return satirlar.Select(s => new MuhasebeEvrakHareketi(s.Id, s.BordroId, s.BordroNo, s.Tarih, s.Islem.ToString(), s.Tur.ToString(),
            $"{s.Kod}/{s.SeriNo}", s.VadeTarihi, Para.TlKarsiligi(s.Tutar, s.Kur), s.EvrakCariId,
            s.Islem == EvrakIslemi.Ciro ? s.IslemCariId : s.OncekiCiroCariId, s.Aciklama)).ToList();
    }

    public async Task<IReadOnlyList<MuhasebeTahakkuk>> MuhasebeTahakkuklariAsync(
        DateOnly baslangic, DateOnly bitis, CancellationToken cancellationToken = default) =>
        await db.GelirGiderKayitlari.AsNoTracking()
            .Where(g => g.Bicim == GelirGiderBicimi.Tahakkuk && g.IptalTarihi == null && g.Tarih >= baslangic && g.Tarih <= bitis)
            .OrderBy(g => g.Tarih).ThenBy(g => g.Id)
            .Select(g => new MuhasebeTahakkuk(g.IslemId, g.Tarih, g.Tur.ToString(), g.KategoriId, g.TutarTl, g.CariHesapId, g.BelgeNo, g.Aciklama))
            .ToListAsync(cancellationToken);

    public Task<KasaHesapBilgisi?> HesapGetirAsync(Guid hesapId, CancellationToken cancellationToken = default) =>
        db.Hesaplar.AsNoTracking()
            .Where(h => h.Id == hesapId)
            .Select(h => new KasaHesapBilgisi(h.Id, h.Kod, h.Ad, h.Tur, h.ParaBirimi, h.AktifMi))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> DemirbasVarMiAsync(Guid demirbasId, CancellationToken cancellationToken = default) =>
        db.Demirbaslar.AnyAsync(d => d.Id == demirbasId, cancellationToken);

    public Task<bool> MasrafMerkeziAktifMiAsync(Guid masrafMerkeziId, CancellationToken cancellationToken = default) =>
        db.MasrafMerkezleri.AnyAsync(m => m.Id == masrafMerkeziId && m.AktifMi, cancellationToken);
}

// Faturanın peşin tahsilatı/ödemesi. Carili belgede cari hareketi de yazılır ve önce belgenin kendi kalemini kapatır.
internal sealed class KasaBankaYazici(KasaBankaDbContext db, KasaIslemServisi islemServisi) : IKasaBankaYazici
{
    public async Task<Sonuc> BelgeTahsilatiYazAsync(Guid kaynakBelgeId, BelgeTahsilati t, CancellationToken cancellationToken = default)
    {
        if (t.CariHesapId is { } cariId)
        {
            var sonuc = await islemServisi.TahsilatOdemeAsync(
                new TahsilatOdemeIstegi(t.HesapId, cariId, t.Tarih, t.Tutar, t.Kur, null, t.BelgeNo, t.Aciklama), t.GirisMi, cancellationToken,
                kaynakBelgeId);
            return sonuc.BasariliMi ? Sonuc.Basarili() : sonuc.Hata;
        }

        // Carisiz (nihai tüketici) satış: para doğrudan kasaya/bankaya girer.
        var hesap = await db.Hesaplar.SingleOrDefaultAsync(h => h.Id == t.HesapId, cancellationToken);
        if (hesap is null)
        {
            return KasaBankaHatalari.HesapBulunamadi;
        }

        var hareket = KasaHareketi.Olustur(hesap, Guid.CreateVersion7(), t.GirisMi ? KasaHareketTuru.Tahsilat : KasaHareketTuru.Odeme, t.GirisMi,
            t.Tarih, t.Tutar, t.Kur, t.BelgeNo, t.Aciklama, kaynakBelgeId: kaynakBelgeId);
        if (!hareket.BasariliMi)
        {
            return hareket.Hata;
        }

        db.KasaHareketleri.Add(hareket.Deger);
        await db.SaveChangesAsync(cancellationToken);
        return Sonuc.Basarili();
    }

    public async Task<Sonuc> BelgeIslemleriniIptalEtAsync(Guid kaynakBelgeId, CancellationToken cancellationToken = default)
    {
        var islemler = await db.KasaHareketleri
            .Where(h => h.KaynakBelgeId == kaynakBelgeId && h.IptalHareketId == null && h.IptalEdilenHareketId == null)
            .Select(h => h.IslemId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var islemId in islemler)
        {
            var sonuc = await islemServisi.IptalAsync(islemId, belgedenMi: true, cancellationToken);
            if (!sonuc.BasariliMi)
            {
                return sonuc.Hata;
            }
        }

        return Sonuc.Basarili();
    }
}
