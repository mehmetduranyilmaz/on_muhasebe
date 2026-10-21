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
using OnMuhasebe.Shared.Contracts.Sayfalama;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;

namespace OnMuhasebe.Modules.KasaBanka.Features;

// Portföy listesi, evrak/bordro detayı, keşideci/borçlular, risk sorgusu ve raporlar. TL karşılıkları evrakın giriş kuruyla hesaplanır.
internal static class CekSenetSorgulari
{
    private const int YaklasanGun = 7;

    private static readonly EvrakDurumu[] _kapalilar =
        [EvrakDurumu.TahsilEdildi, EvrakDurumu.CirodanKapandi, EvrakDurumu.IadeEdildi, EvrakDurumu.Odendi, EvrakDurumu.IadeAlindi, EvrakDurumu.Iptal];

    private static readonly EvrakDurumu[] _portfoy = [EvrakDurumu.Portfoyde, EvrakDurumu.Tahsilde, EvrakDurumu.Teminatta];
    private static readonly EvrakDurumu[] _karsiliksiz = [EvrakDurumu.Karsiliksiz, EvrakDurumu.HukukiTakipte];

    public static void Map(RouteGroupBuilder grup)
    {
        grup.MapGet("/evraklar", ListeleAsync);
        grup.MapGet("/evraklar/{id:guid}", DetayAsync);
        grup.MapGet("/bordrolar/{id:guid}", BordroAsync);
        grup.MapGet("/borclular", BorclularAsync);
        grup.MapGet("/borclular/{id:guid}", BorcluAsync);
        grup.MapGet("/risk", RiskAsync);
        grup.MapGet("/cari-riski/{cariId:guid}", CariRiskiAsync);
        grup.MapGet("/raporlar/vade-takvimi", VadeTakvimiAsync);
        grup.MapGet("/raporlar/ozet", OzetAsync);
    }

    // acik: kapanmamış (tahsil/iade/ödeme/iptal olmamış) evraklar. cariId hem getiren hem ciro edilen cariyi kapsar.
    private static async Task<IResult> ListeleAsync(
        EvrakYonu? yon, EvrakTuru? tur, EvrakDurumu? durum, bool? acik, Guid? cariId, Guid? borcluId, DateOnly? vadeBaslangic, DateOnly? vadeBitis,
        string? arama, int? sayfa, int? boyut, KasaBankaDbContext db, ICariSorgusu cariSorgusu, CancellationToken ct)
    {
        var sorgu = db.Evraklar.AsNoTracking();
        sorgu = yon is { } y ? sorgu.Where(e => e.Yon == y) : sorgu;
        sorgu = tur is { } t ? sorgu.Where(e => e.Tur == t) : sorgu;
        sorgu = durum is { } d ? sorgu.Where(e => e.Durum == d) : sorgu.Where(e => e.Durum != EvrakDurumu.Iptal);
        sorgu = acik == true ? sorgu.Where(e => !_kapalilar.Contains(e.Durum)) : sorgu;
        sorgu = cariId is { } c ? sorgu.Where(e => e.CariHesapId == c || e.CiroCariId == c) : sorgu;
        sorgu = borcluId is { } b ? sorgu.Where(e => e.BorcluId == b) : sorgu;
        sorgu = vadeBaslangic is { } vb ? sorgu.Where(e => e.VadeTarihi >= vb) : sorgu;
        sorgu = vadeBitis is { } vs ? sorgu.Where(e => e.VadeTarihi <= vs) : sorgu;
        if (!string.IsNullOrWhiteSpace(arama))
        {
            var desen = AramaDeseni.Icerir(arama);
            sorgu = sorgu.Where(e => EF.Functions.ILike(e.Kod, desen) || EF.Functions.ILike(e.SeriNo, desen)
                                     || (e.BorcluAdi != null && EF.Functions.ILike(e.BorcluAdi, desen)));
        }

        var sonuc = await Ozetler(sorgu.OrderBy(e => e.VadeTarihi).ThenBy(e => e.Kod))
            .SayfalaAsync(new SayfalamaIstegi(sayfa ?? 1, boyut ?? SayfalamaIstegi.VarsayilanBoyut), ct);
        return TypedResults.Ok(sonuc with { Kayitlar = await AdlariEkleAsync(sonuc.Kayitlar, db, cariSorgusu, ct) });
    }

    private static async Task<IResult> DetayAsync(Guid id, KasaBankaDbContext db, ICariSorgusu cariSorgusu, CancellationToken ct)
    {
        var e = await db.Evraklar.AsNoTracking().Include(x => x.Hareketler).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (e is null)
        {
            return KasaBankaHatalari.EvrakBulunamadi.Problem();
        }

        var bordroIdleri = e.Hareketler.Select(h => h.BordroId).Distinct().ToList();
        var bordrolar = await db.EvrakBordrolari.AsNoTracking().Where(b => bordroIdleri.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.Kod, ct);
        var hesapIdleri = e.Hareketler.Select(h => h.HesapId).Append(e.KendiHesapId).Append(e.KonumHesapId).OfType<Guid>().Distinct().ToList();
        var hesaplar = await db.Hesaplar.AsNoTracking().Where(h => hesapIdleri.Contains(h.Id)).ToDictionaryAsync(h => h.Id, h => h.Ad, ct);
        var cariIdleri = e.Hareketler.Select(h => h.CariHesapId).Append(e.CariHesapId).Append(e.CiroCariId).OfType<Guid>().Distinct().ToList();
        var unvanlar = await cariSorgusu.UnvanlarAsync(cariIdleri, ct);

        string? Hesap(Guid? h) => h is { } x ? hesaplar.GetValueOrDefault(x) : null;
        string? Unvan(Guid? c) => c is { } x ? unvanlar.GetValueOrDefault(x) : null;

        var hareketler = e.Hareketler.OrderBy(h => h.Sira).Select(h => new EvrakHareketDetayi(
            h.Id, h.BordroId, bordrolar.GetValueOrDefault(h.BordroId) ?? "", h.Islem, h.Tarih, h.OncekiDurum, h.YeniDurum, h.HesapId, Hesap(h.HesapId),
            h.CariHesapId, Unvan(h.CariHesapId), h.Tutar, h.Aciklama, h.GeriAlindi, h.OlusturmaTarihi)).ToList();
        return TypedResults.Ok(new EvrakDetayi(
            e.Id, e.Kod, e.Tur, e.Yon, e.SeriNo, e.GirisTarihi, e.VadeTarihi, e.Tutar, e.ParaBirimi, e.Kur, e.Durum, e.CariHesapId, Unvan(e.CariHesapId),
            e.BorcluId, e.BorcluAdi, e.BorcluKimlikNo, e.BankaAdi, e.Sube, e.HesapNo, e.KesideYeri, e.KefilAdi, e.KefilKimlikNo,
            e.KendiHesapId, Hesap(e.KendiHesapId), e.KonumHesapId, Hesap(e.KonumHesapId), e.CiroCariId, Unvan(e.CiroCariId),
            e.TahsilEdilen, e.KarsiliksizTarihi, e.RiskOnayTarihi, e.Aciklama,
            e.Durum == EvrakDurumu.Iptal ? [] : EvrakGecisleri.Uygunlar(e.Yon, e.Durum),
            e.Durum != EvrakDurumu.Iptal && e.Hareketler.Any(h => !h.GeriAlindi),
            hareketler));
    }

    private static async Task<IResult> BordroAsync(Guid id, KasaBankaDbContext db, ICariSorgusu cariSorgusu, CancellationToken ct)
    {
        var b = await db.EvrakBordrolari.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (b is null)
        {
            return KasaBankaHatalari.BordroBulunamadi.Problem();
        }

        var evrakIdleri = db.EvrakHareketleri.Where(h => h.BordroId == id).Select(h => h.EvrakId);
        var evraklar = await AdlariEkleAsync(
            await Ozetler(db.Evraklar.AsNoTracking().Where(e => evrakIdleri.Contains(e.Id)).OrderBy(e => e.Kod)).ToListAsync(ct), db, cariSorgusu, ct);
        var unvan = b.CariHesapId is { } c ? (await cariSorgusu.UnvanlarAsync([c], ct)).GetValueOrDefault(c) : null;
        var hesap = b.HesapId is { } h ? await db.Hesaplar.Where(x => x.Id == h).Select(x => x.Ad).SingleOrDefaultAsync(ct) : null;
        return TypedResults.Ok(new BordroDetayi(b.Id, b.Kod, b.Islem, b.Tarih, b.CariHesapId, unvan, b.HesapId, hesap, b.Aciklama, evraklar));
    }

    private static async Task<IResult> BorclularAsync(string? arama, bool? riskli, int? sayfa, int? boyut, KasaBankaDbContext db, CancellationToken ct)
    {
        var sorgu = db.EvrakBorclulari.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(arama))
        {
            var desen = AramaDeseni.Icerir(arama);
            sorgu = sorgu.Where(b => EF.Functions.ILike(b.Ad, desen) || (b.KimlikNo != null && EF.Functions.ILike(b.KimlikNo, desen)));
        }

        var evraklar = db.Evraklar.Where(e => e.Durum != EvrakDurumu.Iptal);
        var sonuc = await sorgu.OrderBy(b => b.Ad)
            .Select(b => new
            {
                b.Id, b.Ad, b.KimlikNo, b.Telefon, b.KaraListede, b.UyariKapali,
                EvrakSayisi = evraklar.Count(e => e.BorcluId == b.Id),
                KarsiliksizSayisi = evraklar.Count(e => e.BorcluId == b.Id && e.KarsiliksizTarihi != null)
            })
            .Where(b => riskli != true || b.KaraListede || (b.KarsiliksizSayisi > 0 && !b.UyariKapali))
            .Select(b => new BorcluOzeti(b.Id, b.Ad, b.KimlikNo, b.Telefon, b.EvrakSayisi, b.KarsiliksizSayisi, b.KaraListede,
                b.KaraListede || (b.KarsiliksizSayisi > 0 && !b.UyariKapali)))
            .SayfalaAsync(new SayfalamaIstegi(sayfa ?? 1, boyut ?? SayfalamaIstegi.VarsayilanBoyut), ct);
        return TypedResults.Ok(sonuc);
    }

    private static async Task<IResult> BorcluAsync(Guid id, KasaBankaDbContext db, EvrakRiskServisi risk, CancellationToken ct)
    {
        var b = await db.EvrakBorclulari.AsNoTracking().Include(x => x.Hesaplar).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (b is null)
        {
            return KasaBankaHatalari.BorcluBulunamadi.Problem();
        }

        var karsiliksizlar = (await risk.KarsiliksizlarAsync([id], ct)).GetValueOrDefault(id) ?? [];
        return TypedResults.Ok(new BorcluDetayi(
            b.Id, b.Ad, b.KimlikNo, b.Telefon, b.Adres, b.Not, b.KaraListede, b.UyariKapali, b.RiskNotu,
            RiskMesaji.RiskliMi(b.KaraListede, b.UyariKapali, karsiliksizlar.Count),
            b.Hesaplar.Select(h => new BorcluHesapBilgisi(h.Id, h.BankaAdi, h.Sube, h.HesapNo)).ToList(),
            karsiliksizlar.OrderByDescending(k => k.KarsiliksizTarihi).ToList(), b.Surum));
    }

    // Evrak girişi ekranının anlık uyarısı; kayıtta aynı kontrol API tarafında tekrar yapılır.
    private static async Task<IResult> RiskAsync(
        Guid? borcluId, string? kimlikNo, string? ad, Guid? cariId, KasaBankaDbContext db, EvrakRiskServisi risk, ICariSorgusu cariSorgusu,
        CekSenetServisi servis, CancellationToken ct)
    {
        if ((await servis.AyarAsync(ct)).RiskDavranisi == RiskDavranisi.Kapali)
        {
            return TypedResults.Ok(new RiskBilgisi(false, null, []));
        }

        var kimlikler = new List<string>();
        if (!string.IsNullOrWhiteSpace(kimlikNo))
        {
            kimlikler.Add(kimlikNo.Trim());
        }

        if (cariId is { } c && (await cariSorgusu.GetirAsync(c, ct))?.VergiNo is { } vergiNo)
        {
            kimlikler.Add(vergiNo);
        }

        var riskliler = await risk.RiskliBorclularAsync(
            borcluId is { } id ? [id] : [], kimlikler, string.IsNullOrWhiteSpace(ad) || kimlikler.Count > 0 ? [] : [ad], ct);
        return TypedResults.Ok(new RiskBilgisi(riskliler.Count > 0, riskliler.Count > 0 ? RiskMesaji.Olustur(riskliler) : null, riskliler));
    }

    private static async Task<IResult> CariRiskiAsync(Guid cariId, KasaBankaDbContext db, CancellationToken ct)
    {
        var evraklar = await db.Evraklar.AsNoTracking()
            .Where(e => e.CariHesapId == cariId && !_kapalilar.Contains(e.Durum))
            .Select(e => new { e.Yon, e.Durum, e.ParaBirimi, e.Tutar, e.TahsilEdilen })
            .ToListAsync(ct);
        var riskler = evraklar.GroupBy(e => e.ParaBirimi).OrderBy(g => g.Key).Select(g => new CariEvrakRiski(
            g.Key,
            g.Where(e => e.Yon == EvrakYonu.Alinan && _portfoy.Contains(e.Durum)).Sum(e => e.Tutar),
            g.Where(e => e.Yon == EvrakYonu.Alinan && e.Durum == EvrakDurumu.CiroEdildi).Sum(e => e.Tutar),
            g.Where(e => e.Yon == EvrakYonu.Alinan && _karsiliksiz.Contains(e.Durum)).Sum(e => e.Tutar - e.TahsilEdilen),
            g.Where(e => e.Yon == EvrakYonu.Verilen && e.Durum == EvrakDurumu.Verildi).Sum(e => e.Tutar))).ToList();
        return TypedResults.Ok(riskler);
    }

    private static async Task<IResult> VadeTakvimiAsync(
        DateOnly? baslangic, DateOnly? bitis, VadeGruplama? gruplama, KasaBankaDbContext db, TimeProvider zaman, CancellationToken ct)
    {
        var bugun = TurkiyeSaati.Bugun(zaman);
        var (bas, son) = (baslangic ?? bugun, bitis ?? bugun.AddMonths(3));
        if (son < bas || son > bas.AddYears(3))
        {
            return Hata.Dogrulama("tarih_araligi", "Bitiş tarihi başlangıçtan önce ya da 3 yıldan uzak olamaz.").Problem();
        }

        var kalemler = await AcikVadeler(db, son).ToListAsync(ct);
        return TypedResults.Ok(VadeTakvimiHesaplayici.Olustur(
            kalemler.Select(k => new VadeKalemi(k.VadeTarihi, k.Yon, Para.Yuvarla(k.Tutar * k.Kur))).ToList(), bugun, bas, son,
            gruplama ?? (son.DayNumber - bas.DayNumber > 120 ? VadeGruplama.Ay : VadeGruplama.Hafta)));
    }

    private static async Task<IResult> OzetAsync(KasaBankaDbContext db, ICariSorgusu cariSorgusu, TimeProvider zaman, CancellationToken ct)
    {
        var bugun = TurkiyeSaati.Bugun(zaman);
        var sinir = bugun.AddDays(YaklasanGun);
        var acik = await AcikVadeler(db).ToListAsync(ct);
        var karsiliksizTl = await db.Evraklar.AsNoTracking()
            .Where(e => e.Yon == EvrakYonu.Alinan && _karsiliksiz.Contains(e.Durum))
            .SumAsync(e => (e.Tutar - e.TahsilEdilen) * e.Kur, ct);
        var yaklasanlar = await AdlariEkleAsync(await Ozetler(db.Evraklar.AsNoTracking()
                .Where(e => ((e.Yon == EvrakYonu.Alinan && _portfoy.Contains(e.Durum)) || (e.Yon == EvrakYonu.Verilen && e.Durum == EvrakDurumu.Verildi))
                            && e.VadeTarihi <= sinir)
                .OrderBy(e => e.VadeTarihi).ThenBy(e => e.Kod).Take(8))
            .ToListAsync(ct), db, cariSorgusu, ct);

        decimal Tl(IEnumerable<AcikVade> e) => e.Sum(x => Para.Yuvarla(x.Tutar * x.Kur));
        var alinan = acik.Where(e => e.Yon == EvrakYonu.Alinan).ToList();
        var verilen = acik.Where(e => e.Yon == EvrakYonu.Verilen).ToList();
        return TypedResults.Ok(new CekSenetOzeti(
            Tl(alinan), alinan.Count,
            Tl(alinan.Where(e => e.VadeTarihi >= bugun && e.VadeTarihi <= sinir)),
            Tl(verilen.Where(e => e.VadeTarihi >= bugun && e.VadeTarihi <= sinir)),
            verilen.Count(e => e.VadeTarihi < bugun),
            Para.Yuvarla(karsiliksizTl),
            yaklasanlar));
    }

    private sealed record AcikVade(DateOnly VadeTarihi, EvrakYonu Yon, decimal Tutar, decimal Kur);

    // Vadesinde para hareketi doğuracak evraklar: portföydeki alınanlar ve ödenmemiş verilenler.
    private static IQueryable<AcikVade> AcikVadeler(KasaBankaDbContext db, DateOnly? sonVade = null) => db.Evraklar.AsNoTracking()
        .Where(e => (e.Yon == EvrakYonu.Alinan && _portfoy.Contains(e.Durum)) || (e.Yon == EvrakYonu.Verilen && e.Durum == EvrakDurumu.Verildi))
        .Where(e => sonVade == null || e.VadeTarihi <= sonVade)
        .Select(e => new AcikVade(e.VadeTarihi, e.Yon, e.Tutar, e.Kur));

    private static IQueryable<EvrakOzeti> Ozetler(IQueryable<Evrak> sorgu) => sorgu.Select(e => new EvrakOzeti(
        e.Id, e.Kod, e.Tur, e.Yon, e.SeriNo, e.VadeTarihi, e.Tutar, e.ParaBirimi, e.Durum, e.CariHesapId, null, e.BorcluAdi, e.BankaAdi,
        e.KonumHesapId != null ? e.KonumHesapId.ToString() : e.CiroCariId != null ? e.CiroCariId.ToString() : null,
        e.Tutar - e.TahsilEdilen, e.KarsiliksizTarihi != null, Array.Empty<EvrakIslemi>()));

    // Cari unvanı ve konum (tahsil/teminat bankası ya da ciro edilen cari) adı doldurulur; konum projeksiyonda kimlik olarak gelir.
    private static async Task<List<EvrakOzeti>> AdlariEkleAsync(
        IReadOnlyList<EvrakOzeti> ozetler, KasaBankaDbContext db, ICariSorgusu cariSorgusu, CancellationToken ct)
    {
        var konumlar = ozetler.Select(o => o.KonumAdi).OfType<string>().Select(Guid.Parse).ToHashSet();
        var hesaplar = await db.Hesaplar.AsNoTracking().Where(h => konumlar.Contains(h.Id)).ToDictionaryAsync(h => h.Id, h => h.Ad, ct);
        var cariIdleri = ozetler.Select(o => o.CariHesapId).Concat(konumlar.Where(k => !hesaplar.ContainsKey(k))).Distinct().ToList();
        var unvanlar = cariIdleri.Count == 0 ? new Dictionary<Guid, string>() : await cariSorgusu.UnvanlarAsync(cariIdleri, ct);
        return ozetler.Select(o => o with
        {
            CariUnvan = unvanlar.GetValueOrDefault(o.CariHesapId),
            UygunIslemler = o.Durum == EvrakDurumu.Iptal ? [] : EvrakGecisleri.Uygunlar(o.Yon, o.Durum),
            KonumAdi = o.KonumAdi is { } k && Guid.Parse(k) is var id ? hesaplar.GetValueOrDefault(id) ?? unvanlar.GetValueOrDefault(id) : null
        }).ToList();
    }
}
