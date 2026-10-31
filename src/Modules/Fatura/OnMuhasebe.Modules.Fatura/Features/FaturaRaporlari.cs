using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Modules.Fatura.Domain;
using OnMuhasebe.Modules.Fatura.Infrastructure;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Results;

namespace OnMuhasebe.Modules.Fatura.Features;

internal static class FaturaRaporlari
{
    private const int OzetListeBoyutu = 5;
    private const int GrafikAySayisi = 6;

    public static void Map(RouteGroupBuilder modul)
    {
        var grup = modul.MapGroup("").Yetki(FaturaYetkileri.Rapor);
        grup.MapGet("/raporlar/ozet", OzetAsync);
        grup.MapGet("/raporlar/karlilik", KarlilikAsync).Yetki(StokYetkileri.Maliyet);
        grup.MapGet("/raporlar/kdv", KdvAsync);
        grup.MapGet("/raporlar/masraf-merkezi", MasrafMerkeziAsync);
        grup.MapGet("/raporlar/ba-bs", BaBsAsync);
        grup.MapGet("/raporlar/ba-bs/cari/{cariId:guid}", CariBaBsAsync);
    }

    // Raporlar istenirse tek işyerine daraltılır (şube kârlılığı, şube KDV dökümü). Ba/Bs vergi numarası düzeyinde beyan olduğundan firma genelidir.
    private static IQueryable<FaturaBelgesi> Faturalar(FaturaDbContext db, Guid? isyeriId) =>
        isyeriId is { } i ? db.Faturalar.Where(f => f.IsyeriId == i) : db.Faturalar;

    private static async Task<IResult> BaBsAsync(int yil, int ay, decimal? sinir, FaturaDbContext db, CancellationToken cancellationToken)
    {
        if (ay is < 1 or > 12)
        {
            return Hata.Dogrulama("ay_gecersiz", "Ay 1-12 arasında olmalıdır.").Problem();
        }

        var belgeler = await BaBsBelgeleriAsync(db, yil, ay, null, cancellationToken);
        var s = sinir ?? BaBsHesaplayici.VarsayilanSinir;
        return TypedResults.Ok(new BaBsRaporu(yil, ay, s, BaBsHesaplayici.Ozet(belgeler, true, s), BaBsHesaplayici.Ozet(belgeler, false, s)));
    }

    // Karşı tarafa gönderilecek BA/BS mutabakat mektubu için cariyle o aydaki belgeler (sınır uygulanmaz).
    private static async Task<IResult> CariBaBsAsync(Guid cariId, int yil, int ay, FaturaDbContext db, CancellationToken cancellationToken)
    {
        if (ay is < 1 or > 12)
        {
            return Hata.Dogrulama("ay_gecersiz", "Ay 1-12 arasında olmalıdır.").Problem();
        }

        var belgeler = await BaBsBelgeleriAsync(db, yil, ay, cariId, cancellationToken);
        var ba = belgeler.Where(b => b.BaMi).ToList();
        var bs = belgeler.Where(b => !b.BaMi).ToList();
        return TypedResults.Ok(new CariBaBsOzeti(yil, ay, ba.Count, Math.Truncate(ba.Sum(b => b.MatrahTl)), bs.Count, Math.Truncate(bs.Sum(b => b.MatrahTl))));
    }

    // Onaylı faturalar, belge tarihine göre ay; tutar KDV hariç matrahın fatura kuruyla TL karşılığı.
    private static async Task<List<BaBsBelgesi>> BaBsBelgeleriAsync(FaturaDbContext db, int yil, int ay, Guid? cariId, CancellationToken cancellationToken)
    {
        var bas = new DateOnly(yil, ay, 1);
        var son = bas.AddMonths(1).AddDays(-1);
        var faturalar = await db.Faturalar.AsNoTracking()
            .Where(f => f.Durum == FaturaDurumu.Onayli && f.Tarih >= bas && f.Tarih <= son && (cariId == null || f.CariHesapId == cariId))
            .Select(f => new { f.Tur, f.CariHesapId, f.VergiNo, f.CariUnvan, f.Matrah, f.Kur })
            .ToListAsync(cancellationToken);
        return faturalar
            .Select(f => (Fatura: f, BaMi: BaBsHesaplayici.BaMi(f.Tur, f.CariHesapId is null || f.VergiNo == "11111111111")))
            .Where(x => x.BaMi is not null)
            .Select(x => new BaBsBelgesi(x.BaMi!.Value, x.Fatura.VergiNo, x.Fatura.CariHesapId, x.Fatura.CariUnvan,
                Math.Round(x.Fatura.Matrah * x.Fatura.Kur, 2, MidpointRounding.AwayFromZero)))
            .ToList();
    }

    // Onaylı faturaların merkez bazında KDV hariç TL tutarı: satış gelir, alış/gider pusulası/SMM gider; iadeler düşülür.
    // KDV ve stopaj gelir/gider değildir (vergi alacağı/borcu), bu yüzden matrah esas alınır.
    private static async Task<IResult> MasrafMerkeziAsync(
        FaturaDbContext db, TimeProvider zaman, DateOnly? baslangic, DateOnly? bitis, Guid? isyeriId, CancellationToken cancellationToken)
    {
        var bugun = TurkiyeSaati.Bugun(zaman);
        var (bas, son) = (baslangic ?? new DateOnly(bugun.Year, 1, 1), bitis ?? bugun);
        if (son < bas)
        {
            return Hata.Dogrulama("tarih_araligi", "Bitiş tarihi başlangıçtan önce olamaz.").Problem();
        }

        var faturalar = await Faturalar(db, isyeriId).AsNoTracking()
            .Where(f => f.Durum == FaturaDurumu.Onayli && f.Tarih >= bas && f.Tarih <= son)
            .Select(f => new { f.MasrafMerkeziId, f.Tur, f.Matrah, f.Kur })
            .ToListAsync(cancellationToken);

        var kalemler = faturalar
            .GroupBy(f => f.MasrafMerkeziId)
            .Select(g => new MerkezTutari(
                g.Key,
                g.Sum(f => f.Tur switch
                {
                    FaturaTuru.Satis => Tl(f.Matrah, f.Kur),
                    FaturaTuru.SatistanIade => -Tl(f.Matrah, f.Kur),
                    _ => 0m
                }),
                g.Sum(f => f.Tur switch
                {
                    FaturaTuru.Alis or FaturaTuru.GiderPusulasi or FaturaTuru.SerbestMeslekMakbuzu => Tl(f.Matrah, f.Kur),
                    FaturaTuru.AlistanIade => -Tl(f.Matrah, f.Kur),
                    _ => 0m
                }),
                g.Count()))
            .ToList();
        return TypedResults.Ok(new MerkezTutarlari(bas, son, kalemler));

        static decimal Tl(decimal tutar, decimal kur) => Math.Round(tutar * kur, 2, MidpointRounding.AwayFromZero);
    }

    // Brüt kârlılık: onaylı satış ve satıştan iade faturaları. Net satış = satır matrahı (iskontolar düşülmüş, KDV hariç) × fatura kuru.
    // Maliyet stoktan (firmanın maliyet yöntemiyle): satır irsaliyeden aktarıldıysa irsaliyenin çıkışından, değilse faturanın kendi çıkışından;
    // satıştan iadede stoğa giriş maliyeti düşülür. Hizmet ve serbest satırın maliyeti izlenmez.
    private static async Task<IResult> KarlilikAsync(
        FaturaDbContext db, IStokSorgusu stokSorgusu, TimeProvider zaman,
        DateOnly? baslangic, DateOnly? bitis, KarlilikKirilimi? kirilim, Guid? isyeriId, CancellationToken cancellationToken)
    {
        var bugun = TurkiyeSaati.Bugun(zaman);
        var (bas, son) = (baslangic ?? new DateOnly(bugun.Year, bugun.Month, 1), bitis ?? bugun);
        if (son < bas)
        {
            return Hata.Dogrulama("tarih_araligi", "Bitiş tarihi başlangıçtan önce olamaz.").Problem();
        }

        var faturalar = await Faturalar(db, isyeriId).AsNoTracking().Include(f => f.Satirlar)
            .Where(f => f.Durum == FaturaDurumu.Onayli && (f.Tur == FaturaTuru.Satis || f.Tur == FaturaTuru.SatistanIade)
                        && f.Tarih >= bas && f.Tarih <= son)
            .ToListAsync(cancellationToken);

        var irsaliyeSatirIdleri = faturalar.SelectMany(f => f.Satirlar).Where(s => s.IrsaliyeSatiriId is not null).Select(s => s.IrsaliyeSatiriId!.Value).ToList();
        var irsaliyeleri = irsaliyeSatirIdleri.Count == 0 ? new Dictionary<Guid, Guid>() : await db.IrsaliyeSatirlari.AsNoTracking()
            .Where(s => irsaliyeSatirIdleri.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.IrsaliyeId, cancellationToken);

        var kaynaklar = faturalar.Select(f => f.Id).Concat(irsaliyeleri.Values).Distinct().ToList();
        var maliyetler = (await stokSorgusu.BelgeMaliyetleriAsync(kaynaklar, cancellationToken)).ToDictionary(m => (m.KaynakBelgeId, m.StokKartiId));
        var kartIdleri = faturalar.SelectMany(f => f.Satirlar).Where(s => s.StokKartiId is not null).Select(s => s.StokKartiId!.Value).Distinct().ToList();
        var kartlar = kartIdleri.Count == 0 ? new Dictionary<Guid, StokKartiBilgisi>()
            : (await stokSorgusu.KartlariGetirAsync(kartIdleri, cancellationToken)).ToDictionary(k => k.Id);

        var kalemler = new List<KarlilikKalemi>();
        foreach (var f in faturalar)
        {
            var isaret = f.Tur == FaturaTuru.SatistanIade ? -1m : 1m;
            foreach (var s in f.Satirlar)
            {
                var katsayi = s.StokKartiId is { } kartId && kartlar.TryGetValue(kartId, out var kart)
                    ? (s.Birim == kart.TemelBirim ? 1m : kart.EkBirimler.FirstOrDefault(b => b.BirimKodu == s.Birim)?.Katsayi ?? 1m)
                    : 1m;
                var temelMiktar = s.Miktar * katsayi;
                decimal? maliyet = null;
                if (s.StokluMu)
                {
                    var kaynak = s.IrsaliyeSatiriId is { } irsSatir && irsaliyeleri.TryGetValue(irsSatir, out var irsaliyeId) ? irsaliyeId : f.Id;
                    if (maliyetler.TryGetValue((kaynak, s.StokKartiId!.Value), out var m))
                    {
                        var birim = f.Tur == FaturaTuru.SatistanIade
                            ? (m.GirisMiktari == 0 ? 0m : m.GirisMaliyetiTl / m.GirisMiktari)
                            : m.CikisBirimMaliyeti;
                        maliyet = Para.Yuvarla(temelMiktar * birim) * isaret;
                    }
                }

                kalemler.Add(new KarlilikKalemi(f.Id, f.Numara, f.Tarih, f.CariHesapId, f.CariUnvan, s.StokKartiId, s.StokKodu, s.Ad,
                    temelMiktar * isaret, Para.Yuvarla(s.Matrah * f.Kur) * isaret, maliyet));
            }
        }

        var k = kirilim ?? KarlilikKirilimi.Urun;
        var netSatis = kalemler.Sum(x => x.NetSatisTl);
        var toplamMaliyet = kalemler.Sum(x => x.MaliyetTl ?? 0m);
        return TypedResults.Ok(new KarlilikRaporu(bas, son, k, netSatis, toplamMaliyet, netSatis - toplamMaliyet,
            FaturaRaporHesaplayici.Marj(netSatis - toplamMaliyet, netSatis), kalemler.Where(x => x.MaliyetTl is null).Sum(x => x.NetSatisTl),
            FaturaRaporHesaplayici.Karlilik(kalemler, k)));
    }

    // KDV beyan hazırlık özeti (aylık, fatura tarihine göre). Döviz faturaların TL karşılığı fatura kuruyladır.
    // Muhtasar için stopaj belge tarihine göre toplanır (hesaben ödeme); fiili ödeme tarihi farklıysa beyan dönemi değişebilir.
    private static async Task<IResult> KdvAsync(FaturaDbContext db, TimeProvider zaman, int? yil, int? ay, Guid? isyeriId, CancellationToken cancellationToken)
    {
        var bugun = TurkiyeSaati.Bugun(zaman);
        var (y, a) = (yil ?? bugun.Year, ay ?? bugun.Month);
        if (a is < 1 or > 12 || y is < 2000 or > 2100)
        {
            return Hata.Dogrulama("donem", "Geçersiz dönem.").Problem();
        }

        var bas = new DateOnly(y, a, 1);
        var son = bas.AddMonths(1).AddDays(-1);
        var faturalar = await Faturalar(db, isyeriId).AsNoTracking().Include(f => f.Satirlar)
            .Where(f => f.Durum == FaturaDurumu.Onayli && f.Tarih >= bas && f.Tarih <= son)
            .ToListAsync(cancellationToken);

        var kalemler = faturalar.SelectMany(f => f.Satirlar.Select(s => new KdvKalemi(f.Tur, s.KdvOrani,
                Para.Yuvarla(s.Matrah * f.Kur), Para.Yuvarla(s.KdvTutari * f.Kur), s.TevkifatKodu, s.TevkifatPay, s.TevkifatPayda,
                Para.Yuvarla(s.TevkifEdilenKdv * f.Kur))))
            .ToList();
        var stopajlar = faturalar.Where(f => f.StopajTutari > 0)
            .Select(f => new StopajKalemi(f.Tur, f.StopajOrani, Para.Yuvarla(f.Matrah * f.Kur), Para.Yuvarla(f.StopajTutari * f.Kur)))
            .ToList();

        return TypedResults.Ok(FaturaRaporHesaplayici.KdvOzeti(y, a, kalemler, stopajlar, faturalar.Count));
    }

    // Alış tarafı giderleri: alış faturası, gider pusulası ve serbest meslek makbuzu.
    private static readonly FaturaTuru[] _alisTurleri = [FaturaTuru.Alis, FaturaTuru.GiderPusulasi, FaturaTuru.SerbestMeslekMakbuzu];

    // Ana sayfa özeti: yalnızca onaylı faturalar; tutarlar TL ve KDV hariç, iadeler düşülmüş.
    // KDV1 mantığı: satıcı tevkif edilen KDV'yi hesaplanan KDV'ye katmaz (alıcı KDV2 ile öder); alıcı KDV'nin tamamını indirir.
    private static async Task<IResult> OzetAsync(FaturaDbContext db, TimeProvider zaman, Guid? isyeriId, CancellationToken cancellationToken)
    {
        var bugun = TurkiyeSaati.Bugun(zaman);
        var ayBasi = new DateOnly(bugun.Year, bugun.Month, 1);
        var ilkAy = ayBasi.AddMonths(-(GrafikAySayisi - 1));

        var aylik = await Faturalar(db, isyeriId).AsNoTracking()
            .Where(f => f.Durum == FaturaDurumu.Onayli && f.Tarih >= ilkAy)
            .GroupBy(f => new { f.Tarih.Year, f.Tarih.Month, f.Tur })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.Tur,
                Matrah = g.Sum(f => f.Matrah * f.Kur),
                Kdv = g.Sum(f => f.KdvToplami * f.Kur),
                Tevkif = g.Sum(f => f.TevkifEdilenKdvToplami * f.Kur),
                Sayi = g.Count()
            })
            .ToListAsync(cancellationToken);

        decimal Matrah(int yil, int ay, params FaturaTuru[] turler) =>
            aylik.Where(a => a.Year == yil && a.Month == ay && turler.Contains(a.Tur)).Sum(a => a.Matrah);

        decimal Kdv(int yil, int ay, params FaturaTuru[] turler) =>
            aylik.Where(a => a.Year == yil && a.Month == ay && turler.Contains(a.Tur)).Sum(a => a.Kdv);

        decimal SaticiKdvsi(int yil, int ay, FaturaTuru tur) =>
            aylik.Where(a => a.Year == yil && a.Month == ay && a.Tur == tur).Sum(a => a.Kdv - a.Tevkif);

        var sonAylar = Enumerable.Range(0, GrafikAySayisi)
            .Select(i => ilkAy.AddMonths(i))
            .Select(a => new AylikFaturaTutari(a.Year, a.Month,
                Para.Yuvarla(Matrah(a.Year, a.Month, FaturaTuru.Satis) - Matrah(a.Year, a.Month, FaturaTuru.SatistanIade)),
                Para.Yuvarla(Matrah(a.Year, a.Month, _alisTurleri) - Matrah(a.Year, a.Month, FaturaTuru.AlistanIade))))
            .ToList();

        var (y, m) = (ayBasi.Year, ayBasi.Month);
        var taslakSayisi = await Faturalar(db, isyeriId).CountAsync(f => f.Durum == FaturaDurumu.Taslak, cancellationToken);
        var sonFaturalar = await Faturalar(db, isyeriId).AsNoTracking()
            .Where(f => f.Durum != FaturaDurumu.Taslak)
            .OrderByDescending(f => f.Tarih).ThenByDescending(f => f.OlusturmaTarihi)
            .Take(OzetListeBoyutu)
            .Select(f => new FaturaOzeti(f.Id, f.Tur, f.SatisSekli, f.Durum, f.Numara, f.Tarih, f.VadeTarihi, f.CariHesapId, f.CariUnvan,
                f.ParaBirimi, f.GenelToplam, f.GenelToplamTl, null, f.IsyeriId))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new FaturaOzetRaporu(
            sonAylar[^1].Satis,
            sonAylar[^1].Alis,
            Para.Yuvarla(SaticiKdvsi(y, m, FaturaTuru.Satis) - SaticiKdvsi(y, m, FaturaTuru.SatistanIade)),
            Para.Yuvarla(Kdv(y, m, _alisTurleri) - Kdv(y, m, FaturaTuru.AlistanIade)),
            aylik.Where(a => a.Year == y && a.Month == m).Sum(a => a.Sayi),
            taslakSayisi,
            sonAylar,
            sonFaturalar));
    }
}
