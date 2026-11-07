using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Modules.Fatura.Domain;
using OnMuhasebe.Modules.Fatura.Infrastructure;
using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Results;

namespace OnMuhasebe.Modules.Fatura.Features;

// Satış/alış özeti, ürün bazlı alış-satış toplamları ve sipariş durum raporu.
internal static class TicariRaporIslemleri
{
    // Rapor tek istekte döner; çok uzun aralıkta bellek ve süre sınırlı kalsın.
    private const int AzamiGun = 400;

    public static void Map(RouteGroupBuilder modul)
    {
        var grup = modul.MapGroup("/raporlar").Yetki(FaturaYetkileri.Rapor);
        grup.MapGet("/fatura-ozeti", FaturaOzetiAsync);
        grup.MapGet("/urun-toplamlari", UrunToplamlariAsync);
        grup.MapGet("/siparis-durumu", SiparisDurumuAsync).Yetki(FaturaYetkileri.Teklif);
    }

    private static Sonuc<(DateOnly Bas, DateOnly Son)> Donem(TimeProvider zaman, DateOnly? baslangic, DateOnly? bitis, bool yilBasindan = false)
    {
        var bugun = TurkiyeSaati.Bugun(zaman);
        var bas = baslangic ?? (yilBasindan ? new DateOnly(bugun.Year, 1, 1) : new DateOnly(bugun.Year, bugun.Month, 1));
        var son = bitis ?? bugun;
        if (son < bas)
        {
            return Hata.Dogrulama("tarih_araligi", "Bitiş tarihi başlangıçtan önce olamaz.");
        }

        return son.DayNumber - bas.DayNumber > AzamiGun
            ? Hata.Dogrulama("tarih_araligi", $"Tarih aralığı en çok {AzamiGun} gün olabilir.")
            : (bas, son);
    }

    private static FaturaTuru[] Turler(TicariTaraf taraf) => taraf == TicariTaraf.Satis
        ? [FaturaTuru.Satis, FaturaTuru.SatistanIade]
        : [FaturaTuru.Alis, FaturaTuru.SerbestMeslekMakbuzu, FaturaTuru.GiderPusulasi, FaturaTuru.AlistanIade];

    private static bool IadeMi(FaturaTuru tur) => tur is FaturaTuru.SatistanIade or FaturaTuru.AlistanIade;

    private static IQueryable<FaturaBelgesi> OnayliFaturalar(FaturaDbContext db, Guid? isyeriId, DateOnly bas, DateOnly son) =>
        (isyeriId is { } i ? db.Faturalar.Where(f => f.IsyeriId == i) : db.Faturalar)
        .Where(f => f.Durum == FaturaDurumu.Onayli && f.Tarih >= bas && f.Tarih <= son);

    private static async Task<IResult> FaturaOzetiAsync(
        FaturaDbContext db, TimeProvider zaman, DateOnly? baslangic, DateOnly? bitis, TicariTaraf? taraf, FaturaOzetKirilimi? kirilim,
        Guid? cariId, Guid? isyeriId, CancellationToken cancellationToken)
    {
        var donem = Donem(zaman, baslangic, bitis);
        if (!donem.BasariliMi)
        {
            return donem.Hata.Problem();
        }

        var (bas, son) = donem.Deger;
        var t = taraf ?? TicariTaraf.Satis;
        var turler = Turler(t);
        var sorgu = OnayliFaturalar(db, isyeriId, bas, son).Where(f => turler.Contains(f.Tur));
        if (cariId is not null)
        {
            sorgu = sorgu.Where(f => f.CariHesapId == cariId);
        }

        var faturalar = await sorgu.AsNoTracking()
            .Select(f => new
            {
                f.Id, f.Numara, f.Tarih, f.CariHesapId, f.CariUnvan, f.Tur, f.Kur, f.BrutToplam, f.SatirIskontoToplami, f.GenelIskontoTutari,
                f.Matrah, f.KdvToplami, f.TevkifEdilenKdvToplami, f.GenelToplamTl
            })
            .ToListAsync(cancellationToken);

        var kalemler = faturalar.Select(f =>
        {
            var isaret = IadeMi(f.Tur) ? -1m : 1m;
            return new FaturaOzetKalemi(f.Id, f.Numara, f.Tarih, f.CariHesapId, f.CariUnvan, IadeMi(f.Tur),
                Para.Yuvarla(f.BrutToplam * f.Kur) * isaret, Para.Yuvarla((f.SatirIskontoToplami + f.GenelIskontoTutari) * f.Kur) * isaret,
                Para.Yuvarla(f.Matrah * f.Kur) * isaret, Para.Yuvarla(f.KdvToplami * f.Kur) * isaret,
                Para.Yuvarla(f.TevkifEdilenKdvToplami * f.Kur) * isaret, f.GenelToplamTl * isaret);
        }).ToList();

        return TypedResults.Ok(TicariRaporHesaplayici.FaturaOzeti(bas, son, t, kirilim ?? FaturaOzetKirilimi.Cari, kalemler));
    }

    private static async Task<IResult> UrunToplamlariAsync(
        FaturaDbContext db, IStokSorgusu stokSorgusu, TimeProvider zaman, DateOnly? baslangic, DateOnly? bitis, Guid? isyeriId,
        CancellationToken cancellationToken)
    {
        var donem = Donem(zaman, baslangic, bitis);
        if (!donem.BasariliMi)
        {
            return donem.Hata.Problem();
        }

        var (bas, son) = donem.Deger;
        var turler = Turler(TicariTaraf.Satis).Concat(Turler(TicariTaraf.Alis)).ToArray();
        var faturalar = await OnayliFaturalar(db, isyeriId, bas, son).AsNoTracking().Include(f => f.Satirlar)
            .Where(f => turler.Contains(f.Tur))
            .ToListAsync(cancellationToken);

        var kartIdleri = faturalar.SelectMany(f => f.Satirlar).Where(s => s.StokKartiId is not null).Select(s => s.StokKartiId!.Value).Distinct().ToList();
        var kartlar = kartIdleri.Count == 0 ? new Dictionary<Guid, StokKartiBilgisi>()
            : (await stokSorgusu.KartlariGetirAsync(kartIdleri, cancellationToken)).ToDictionary(k => k.Id);

        var kalemler = new List<UrunToplamKalemi>();
        foreach (var f in faturalar)
        {
            var taraf = Turler(TicariTaraf.Satis).Contains(f.Tur) ? TicariTaraf.Satis : TicariTaraf.Alis;
            var isaret = IadeMi(f.Tur) ? -1m : 1m;
            foreach (var s in f.Satirlar)
            {
                // Fatura ek birimle (koli) kesilmiş olabilir; toplam kartın temel biriminde (adet) verilir.
                var kart = s.StokKartiId is { } kartId ? kartlar.GetValueOrDefault(kartId) : null;
                var katsayi = kart is null || s.Birim == kart.TemelBirim ? 1m : kart.EkBirimler.FirstOrDefault(b => b.BirimKodu == s.Birim)?.Katsayi ?? 1m;
                kalemler.Add(new UrunToplamKalemi(s.StokKartiId, s.StokKodu, kart?.Ad ?? s.Ad, kart?.TemelBirim ?? s.Birim, taraf,
                    s.Miktar * katsayi * isaret,
                    Para.Yuvarla((s.IskontoTutari + s.GenelIskontoPayi) * f.Kur) * isaret,
                    Para.Yuvarla(s.Matrah * f.Kur) * isaret,
                    Para.Yuvarla(s.KdvTutari * f.Kur) * isaret));
            }
        }

        return TypedResults.Ok(TicariRaporHesaplayici.UrunToplamlari(bas, son, kalemler));
    }

    private static async Task<IResult> SiparisDurumuAsync(
        FaturaDbContext db, ICariSorgusu cariSorgusu, TimeProvider zaman, DateOnly? baslangic, DateOnly? bitis, TeklifSiparisTuru? tur,
        TeklifSiparisDurumu? durum, SiparisOdemeDurumu? odeme, Guid? cariId, Guid? isyeriId, CancellationToken cancellationToken)
    {
        var donem = Donem(zaman, baslangic, bitis, yilBasindan: true);
        if (!donem.BasariliMi)
        {
            return donem.Hata.Problem();
        }

        var (bas, son) = donem.Deger;
        var sorgu = db.TeklifSiparisler.AsNoTracking().Include(s => s.Satirlar)
            .Where(s => s.Tur != TeklifSiparisTuru.SatisTeklifi && s.Tarih >= bas && s.Tarih <= son);
        sorgu = tur is { } t && t != TeklifSiparisTuru.SatisTeklifi ? sorgu.Where(s => s.Tur == t) : sorgu;
        // Taslak henüz verilmemiş, iptal geçersiz siparişlerdir; açıkça istenmedikçe rapora girmez.
        sorgu = durum is { } d ? sorgu.Where(s => s.Durum == d) : sorgu.Where(s => s.Durum != TeklifSiparisDurumu.Taslak && s.Durum != TeklifSiparisDurumu.Iptal);
        sorgu = cariId is not null ? sorgu.Where(s => s.CariHesapId == cariId) : sorgu;
        sorgu = isyeriId is { } i ? sorgu.Where(s => s.IsyeriId == i) : sorgu;
        var siparisler = await sorgu.OrderByDescending(s => s.Tarih).ThenByDescending(s => s.Id).ToListAsync(cancellationToken);

        var siparisSatiri = siparisler.SelectMany(s => s.Satirlar.Select(x => (SatirId: x.Id, SiparisId: s.Id))).ToDictionary(x => x.SatirId, x => x.SiparisId);
        var satirIdleri = siparisSatiri.Keys.ToList();
        var irsaliyeSatiri = satirIdleri.Count == 0 ? [] : await db.IrsaliyeSatirlari.AsNoTracking()
            .Where(s => s.SiparisSatiriId != null && satirIdleri.Contains(s.SiparisSatiriId.Value))
            .ToDictionaryAsync(s => s.Id, s => s.SiparisSatiriId!.Value, cancellationToken);
        var irsaliyeIdleri = irsaliyeSatiri.Keys.ToList();

        var faturaSatirlari = satirIdleri.Count == 0 ? [] : await (
                from s in db.FaturaSatirlari.AsNoTracking()
                join f in db.Faturalar on s.FaturaId equals f.Id
                where f.Durum == FaturaDurumu.Onayli
                      && ((s.SiparisSatiriId != null && satirIdleri.Contains(s.SiparisSatiriId.Value))
                          || (s.IrsaliyeSatiriId != null && irsaliyeIdleri.Contains(s.IrsaliyeSatiriId.Value)))
                select new { s.SiparisSatiriId, s.IrsaliyeSatiriId, s.Toplam, FaturaId = f.Id, f.Kur, f.GenelToplam })
            .ToListAsync(cancellationToken);

        var kalanlar = faturaSatirlari.Count == 0 ? new Dictionary<Guid, decimal>()
            : await cariSorgusu.BelgeKalanlariAsync(faturaSatirlari.Select(s => s.FaturaId).Distinct().ToList(), cancellationToken);

        var siparisFaturalari = faturaSatirlari
            .Select(s => (SiparisId: siparisSatiri[s.SiparisSatiriId ?? irsaliyeSatiri[s.IrsaliyeSatiriId!.Value]], Satir: s))
            .ToLookup(x => x.SiparisId, x => x.Satir);

        var bugun = TurkiyeSaati.Bugun(zaman);
        var satirlar = new List<SiparisDurumSatiri>();
        foreach (var s in siparisler)
        {
            var bagli = siparisFaturalari[s.Id].ToList();
            var faturalanan = bagli.Sum(x => Para.Yuvarla(x.Toplam * x.Kur));
            var kalan = bagli.Sum(x => x.GenelToplam == 0m ? 0m
                : Para.Yuvarla(kalanlar.GetValueOrDefault(x.FaturaId) * (x.Toplam / x.GenelToplam) * x.Kur));
            var karsilama = s.Karsilama;
            var gecikme = s.Durum == TeklifSiparisDurumu.Acik && karsilama != KarsilamaDurumu.Tamamlandi && s.TerminTarihi is { } termin && termin < bugun
                ? bugun.DayNumber - termin.DayNumber
                : (int?)null;
            var satir = new SiparisDurumSatiri(s.Id, s.Tur, s.Numara, s.Tarih, s.TerminTarihi, s.CariHesapId, s.CariUnvan, s.Durum, karsilama,
                TicariRaporHesaplayici.KarsilamaYuzdesi(s.Satirlar.Select(x => (x.Miktar, x.KarsilananMiktar, x.Matrah))),
                s.ParaBirimi, s.GenelToplam, faturalanan, kalan, TicariRaporHesaplayici.OdemeDurumu(faturalanan, kalan), gecikme);
            if (odeme is null || satir.OdemeDurumu == odeme)
            {
                satirlar.Add(satir);
            }
        }

        return TypedResults.Ok(new SiparisDurumRaporu(bas, son, satirlar.Count, satirlar.Count(x => x.GecikmeGun is not null),
            satirlar.Sum(x => x.FaturalananTl), satirlar.Sum(x => x.KalanTl), satirlar));
    }
}
