using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.Doviz.Contracts;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.KasaBanka.Infrastructure;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Results;

namespace OnMuhasebe.Modules.KasaBanka.Features;

// Nakit akış takvimi: hazır değerlerden başlayarak beklenen giriş/çıkışları dönemlere dağıtır.
// Tüm dövizler güncel TCMB döviz alış kuruyla TL'ye çevrilir (VUK değerlemesindeki kur).
internal static class NakitAkisIslemleri
{
    public static void Map(RouteGroupBuilder grup) => grup.MapGet("/raporlar/nakit-akis", RaporAsync);

    private static async Task<IResult> RaporAsync(
        KasaBankaDbContext db, ICariSorgusu cariSorgusu, IKurSorgusu kurSorgusu, TimeProvider zaman,
        NakitPeriyodu? periyot, int? adet, bool? gecikmisAlacak, CancellationToken cancellationToken)
    {
        var p = periyot ?? NakitPeriyodu.Hafta;
        var n = adet ?? (p switch { NakitPeriyodu.Gun => 30, NakitPeriyodu.Hafta => 13, _ => 6 });
        var azami = p switch { NakitPeriyodu.Gun => 92, NakitPeriyodu.Hafta => 52, _ => 24 };
        if (n < 1 || n > azami)
        {
            return Hata.Dogrulama("donem_sayisi", $"Dönem sayısı 1 ile {azami} arasında olmalıdır.").Problem();
        }

        var bugun = TurkiyeSaati.Bugun(zaman);
        var donemler = NakitAkisHesaplayici.Donemler(bugun, p, n);
        var ufuk = donemler[^1].Bitis;

        var hesaplar = await db.Hesaplar.AsNoTracking()
            .Select(h => new
            {
                h.Id, h.Ad, h.Tur, h.ParaBirimi, h.SonOdemeGunu, h.PosGunSayisi, h.PosGunTipi,
                Bakiye = db.KasaHareketleri.Where(x => x.HesapId == h.Id).Sum(x => x.Giris - x.Cikis)
            })
            .Where(h => h.Bakiye != 0)
            .ToListAsync(cancellationToken);

        var evraklar = await db.Evraklar.AsNoTracking()
            .Where(e => (e.Yon == EvrakYonu.Alinan && (e.Durum == EvrakDurumu.Portfoyde || e.Durum == EvrakDurumu.Tahsilde))
                        || (e.Yon == EvrakYonu.Verilen && e.Durum == EvrakDurumu.Verildi))
            .Where(e => e.VadeTarihi <= ufuk)
            .Select(e => new { e.Id, e.Tur, e.Yon, e.Kod, e.SeriNo, e.VadeTarihi, e.ParaBirimi, Kalan = e.Tutar - e.TahsilEdilen, e.CariHesapId, e.BorcluAdi })
            .ToListAsync(cancellationToken);

        var cariKalemler = (await cariSorgusu.AcikKalemlerAsync(cancellationToken)).Where(k => k.Vade <= ufuk).ToList();
        var unvanlar = await cariSorgusu.UnvanlarAsync(evraklar.Select(e => e.CariHesapId).Distinct().ToList(), cancellationToken);

        var kurlar = new Dictionary<string, decimal?> { [ParaBirimleri.Tl] = 1m };
        foreach (var pb in hesaplar.Select(h => h.ParaBirimi).Concat(evraklar.Select(e => e.ParaBirimi)).Concat(cariKalemler.Select(k => k.ParaBirimi))
                     .Distinct().Where(pb => !kurlar.ContainsKey(pb)))
        {
            kurlar[pb] = (await kurSorgusu.IslemKuruAsync(pb, bugun, cancellationToken))?.DovizAlis;
        }

        decimal? Tl(decimal tutar, string pb) => kurlar.GetValueOrDefault(pb) is { } kur ? Para.TlKarsiligi(tutar, kur) : null;

        var kalemler = new List<NakitKalemi>();
        void Ekle(DateOnly tarih, NakitKaynagi kaynak, string aciklama, string? belgeNo, string pb, decimal tutar, NakitBaglantisi baglanti, Guid id)
        {
            if (tutar > 0 && Tl(tutar, pb) is { } tl)
            {
                kalemler.Add(new NakitKalemi(tarih, kaynak, aciklama, belgeNo, pb, tutar, tl, tarih < bugun, baglanti, id));
            }
        }

        // Kasa ve banka (vadesiz, vadeli, KMH) bakiyeleri açılıştır; POS'ta bekleyen valörde bankaya geçer, kredi kartı borcu son ödeme gününde ödenir.
        var hazirDegerler = hesaplar.Where(h => h.Tur is HesapTuru.Kasa or HesapTuru.Banka && Tl(h.Bakiye, h.ParaBirimi) is not null)
            .Select(h => new HazirDeger(h.Id, h.Ad, h.Tur, h.ParaBirimi, h.Bakiye, Tl(h.Bakiye, h.ParaBirimi)!.Value))
            .OrderByDescending(h => h.BakiyeTl)
            .ToList();
        foreach (var h in hesaplar)
        {
            if (h.Tur == HesapTuru.Pos && h.Bakiye > 0)
            {
                Ekle(Valor(bugun, h.PosGunSayisi ?? 0, h.PosGunTipi), NakitKaynagi.PosAktarim, $"POS: {h.Ad}", null, h.ParaBirimi, h.Bakiye,
                    NakitBaglantisi.Hesap, h.Id);
            }
            else if (h.Tur == HesapTuru.KrediKarti && h.Bakiye < 0)
            {
                Ekle(NakitAkisHesaplayici.SonrakiSonOdeme(bugun, h.SonOdemeGunu ?? 31), NakitKaynagi.KrediKartiOdeme, $"Kredi kartı: {h.Ad}", null,
                    h.ParaBirimi, -h.Bakiye, NakitBaglantisi.Hesap, h.Id);
            }
        }

        foreach (var e in evraklar)
        {
            var kim = unvanlar.GetValueOrDefault(e.CariHesapId) ?? e.BorcluAdi ?? "";
            var ad = $"{(e.Tur == EvrakTuru.Cek ? "Çek" : "Senet")} {e.SeriNo} · {kim}";
            Ekle(e.VadeTarihi, e.Yon == EvrakYonu.Alinan ? NakitKaynagi.CekSenetTahsil : NakitKaynagi.CekSenetOdeme, ad, e.Kod, e.ParaBirimi, e.Kalan,
                NakitBaglantisi.Evrak, e.Id);
        }

        foreach (var k in cariKalemler)
        {
            var fatura = k.KaynakBelgeId is not null && FaturaHareketiMi(k.Tur);
            Ekle(k.Vade, k.Yon == HareketYonu.Borc ? NakitKaynagi.CariTahsilat : NakitKaynagi.CariOdeme, k.Unvan, k.BelgeNo, k.ParaBirimi, k.Kalan,
                fatura ? NakitBaglantisi.Fatura : NakitBaglantisi.Cari, fatura ? k.KaynakBelgeId!.Value : k.CariHesapId);
        }

        var dahil = gecikmisAlacak == true;
        var acilis = hazirDegerler.Sum(h => h.BakiyeTl);
        var sonuc = NakitAkisHesaplayici.Hesapla(acilis, kalemler, donemler, dahil);
        return TypedResults.Ok(new NakitAkisRaporu(
            bugun, p, dahil, acilis, hazirDegerler, sonuc,
            kalemler.Where(k => k.GecikmisMi && NakitKaynaklari.GirisMi(k.Kaynak)).Sum(k => k.TutarTl),
            kalemler.Where(k => k.GecikmisMi && !NakitKaynaklari.GirisMi(k.Kaynak)).Sum(k => k.TutarTl),
            sonuc[^1].Kumulatif,
            Math.Min(acilis, sonuc.Min(d => d.Kumulatif)),
            sonuc.FirstOrDefault(d => d.Kumulatif < 0)?.Baslangic,
            kalemler.OrderBy(k => k.Tarih).ToList(),
            kurlar.Where(k => k.Value is null).Select(k => k.Key).ToList()));
    }

    private static bool FaturaHareketiMi(CariHareketTuru tur) => tur is CariHareketTuru.SatisFaturasi or CariHareketTuru.AlisFaturasi
        or CariHareketTuru.SatistanIadeFaturasi or CariHareketTuru.AlistanIadeFaturasi or CariHareketTuru.GiderPusulasi or CariHareketTuru.SerbestMeslekMakbuzu;

    // POS valörü: iş günü sayımında hafta sonu atlanır (resmi tatiller hesaba katılmaz; yaklaşık).
    private static DateOnly Valor(DateOnly bugun, int gun, BlokeGunTipi? tip)
    {
        if (tip != BlokeGunTipi.IsGunu)
        {
            return bugun.AddDays(gun);
        }

        var tarih = bugun;
        while (gun > 0)
        {
            tarih = tarih.AddDays(1);
            if (tarih.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                gun--;
            }
        }

        return tarih;
    }
}
