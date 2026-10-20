using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.KasaBanka.Infrastructure;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Results;

namespace OnMuhasebe.Modules.KasaBanka.Features;

internal static class KasaBankaRaporlari
{
    private const int SonHareketSayisi = 6;

    public static void Map(RouteGroupBuilder grup)
    {
        grup.MapGet("/raporlar/ozet", OzetAsync);
        grup.MapGet("/raporlar/gelir-gider", GelirGiderAsync);
    }

    // TL tutarlar kayıtlı işlem kurlarıyladır (güncel kurla yeniden değerleme yapılmaz).
    private static async Task<IResult> OzetAsync(KasaBankaDbContext db, TimeProvider zaman, CancellationToken cancellationToken)
    {
        var bugun = TurkiyeSaati.Bugun(zaman);
        var ayBasi = new DateOnly(bugun.Year, bugun.Month, 1);
        var hesaplar = await HesapIslemleri.Ozetler(db, db.Hesaplar.AsNoTracking().Where(h => h.AktifMi)).ToListAsync(cancellationToken);
        var kategoriler = await KategoriTutarlariAsync(db, ayBasi, bugun, cancellationToken);

        var sonHareketler = await (
                from h in db.KasaHareketleri.AsNoTracking()
                join hs in db.Hesaplar on h.HesapId equals hs.Id
                orderby h.Tarih descending, h.Id descending
                select new SonKasaHareketi(hs.Id, hs.Ad, hs.Tur, h.Tarih, h.Tur, h.ParaBirimi, h.Giris, h.Cikis, h.Aciklama))
            .Take(SonHareketSayisi)
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new KasaBankaOzetRaporu(
            hesaplar.Where(h => h.Tur != HesapTuru.KrediKarti).Sum(h => h.BakiyeTl),
            -hesaplar.Where(h => h.Tur == HesapTuru.KrediKarti && h.BakiyeTl < 0).Sum(h => h.BakiyeTl),
            kategoriler.Where(k => k.Tur == GelirGiderTuru.Gelir).Sum(k => k.Tutar.TutarTl),
            kategoriler.Where(k => k.Tur == GelirGiderTuru.Gider).Sum(k => k.Tutar.TutarTl),
            hesaplar,
            kategoriler.Where(k => k.Tur == GelirGiderTuru.Gider).Select(k => k.Tutar).OrderByDescending(k => k.TutarTl).ToList(),
            sonHareketler));
    }

    private static async Task<IResult> GelirGiderAsync(
        DateOnly? baslangic, DateOnly? bitis, KasaBankaDbContext db, TimeProvider zaman, CancellationToken cancellationToken)
    {
        var bugun = TurkiyeSaati.Bugun(zaman);
        var (bas, son) = (baslangic ?? new DateOnly(bugun.Year, bugun.Month, 1), bitis ?? bugun);
        if (son < bas)
        {
            return Hata.Dogrulama("tarih_araligi", "Bitiş tarihi başlangıçtan önce olamaz.").Problem();
        }

        var kategoriler = await KategoriTutarlariAsync(db, bas, son, cancellationToken);
        var gelirler = kategoriler.Where(k => k.Tur == GelirGiderTuru.Gelir).Select(k => k.Tutar).OrderByDescending(k => k.TutarTl).ToList();
        var giderler = kategoriler.Where(k => k.Tur == GelirGiderTuru.Gider).Select(k => k.Tutar).OrderByDescending(k => k.TutarTl).ToList();
        return TypedResults.Ok(new GelirGiderRaporu(bas, son, gelirler, giderler, gelirler.Sum(g => g.TutarTl), giderler.Sum(g => g.TutarTl)));
    }

    // Ödendi ve tahakkuk kayıtlarının ikisi de sayılır; iptal edilenler hariç.
    private static async Task<List<(GelirGiderTuru Tur, KategoriTutari Tutar)>> KategoriTutarlariAsync(
        KasaBankaDbContext db, DateOnly bas, DateOnly son, CancellationToken cancellationToken) =>
        (await (
                from g in db.GelirGiderKayitlari.AsNoTracking()
                join k in db.Kategoriler on g.KategoriId equals k.Id
                where g.IptalTarihi == null && g.Tarih >= bas && g.Tarih <= son
                group g by new { k.Id, k.Ad, g.Tur } into grup
                select new { grup.Key.Id, grup.Key.Ad, grup.Key.Tur, Tutar = grup.Sum(x => x.TutarTl), Adet = grup.Count() })
            .ToListAsync(cancellationToken))
        .Select(x => (x.Tur, new KategoriTutari(x.Id, x.Ad, x.Tutar, x.Adet)))
        .ToList();
}
