using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Modules.Stok.Domain;
using OnMuhasebe.Modules.Stok.Domain.Maliyet;
using OnMuhasebe.Modules.Stok.Infrastructure;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Contracts.Sayfalama;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;

namespace OnMuhasebe.Modules.Stok.Features;

internal static class StokRaporlari
{
    private const int OzetListeBoyutu = 5;

    public static void Map(RouteGroupBuilder modul)
    {
        var grup = modul.MapGroup("").Yetki(StokYetkileri.Rapor);
        grup.MapGet("/raporlar/durum", DurumAsync);
        grup.MapGet("/raporlar/ozet", OzetAsync);
        grup.MapGet("/kartlar/{kartId:guid}/ekstre", EkstreAsync);
    }

    // Stok durumu: her ürünün (depo filtresiyle) miktarı, birim maliyeti ve değeri. Tutarlar TL.
    private static async Task<IResult> DurumAsync(
        StokDbContext db, StokHesaplayici hesaplayici, StokAyarServisi ayarServisi, TimeProvider zaman,
        DateOnly? tarih, Guid? depoId, Guid? grupId, string? arama, bool? sadeceKritik, bool? sadeceEksi, int? sayfa, int? boyut,
        IYetkiDenetimi yetki, CancellationToken cancellationToken)
    {
        var raporTarihi = tarih ?? TurkiyeSaati.Bugun(zaman);
        var ayar = await ayarServisi.GetirAsync(cancellationToken);

        var kartlar = db.StokKartlari.AsNoTracking().Where(k => k.Tur == StokTuru.Urun && k.AktifMi);
        if (grupId is not null)
        {
            kartlar = kartlar.Where(k => k.GrupId == grupId);
        }

        if (!string.IsNullOrWhiteSpace(arama))
        {
            var desen = AramaDeseni.Icerir(arama);
            kartlar = kartlar.Where(k => EF.Functions.ILike(k.Ad, desen) || EF.Functions.ILike(k.Kod, desen));
        }

        var kartListesi = await kartlar
            .Select(k => new { k.Id, k.Kod, k.Ad, k.TemelBirim, GrupAdi = k.Grup != null ? k.Grup.Ad : null, k.KritikSeviye })
            .ToListAsync(cancellationToken);
        var hesaplar = await hesaplayici.HesaplaAsync(kartlar.Select(k => k.Id), raporTarihi, ayar.MaliyetYontemi, cancellationToken);
        var maliyetGorur = await yetki.GorurMuAsync(cancellationToken);

        var satirlar = kartListesi.Select(k =>
        {
            var hesap = hesaplar.GetValueOrDefault(k.Id);
            var miktar = depoId is { } d ? hesap?.DepoMiktarlari.GetValueOrDefault(d) ?? 0m : hesap?.Miktar ?? 0m;
            var birimMaliyet = maliyetGorur ? hesap?.BirimMaliyet ?? 0m : 0m;
            return new StokDurumuSatiri(k.Id, k.Kod, k.Ad, k.TemelBirim, k.GrupAdi, miktar,
                Math.Round(birimMaliyet, 4), Para.Yuvarla(miktar * birimMaliyet), k.KritikSeviye);
        }).ToList();

        var filtreli = satirlar
            .Where(s => sadeceKritik != true || s.KritikMi)
            .Where(s => sadeceEksi != true || s.EksiMi)
            .OrderBy(s => s.Ad)
            .ToList();

        var istek = new SayfalamaIstegi(sayfa ?? 1, boyut ?? SayfalamaIstegi.VarsayilanBoyut);
        var sayfaKayitlari = filtreli.Skip((istek.GecerliSayfa - 1) * istek.GecerliBoyut).Take(istek.GecerliBoyut).ToList();

        return TypedResults.Ok(new StokDurumuCevabi(
            raporTarihi, depoId, ayar.MaliyetYontemi,
            new SayfaliSonuc<StokDurumuSatiri>(sayfaKayitlari, istek.GecerliSayfa, istek.GecerliBoyut, filtreli.Count),
            filtreli.Sum(s => s.Deger), satirlar.Count(s => s.KritikMi), satirlar.Count(s => s.EksiMi)));
    }

    // Ürün hareket dökümü: devir, giriş/çıkış ve yürüyen bakiye; çıkışlar seçili maliyet yöntemiyle değerlenir.
    private static async Task<IResult> EkstreAsync(
        Guid kartId, DateOnly? baslangic, DateOnly? bitis, Guid? depoId,
        StokDbContext db, StokAyarServisi ayarServisi, TimeProvider zaman, IYetkiDenetimi yetki, CancellationToken cancellationToken)
    {
        var bugun = TurkiyeSaati.Bugun(zaman);
        var bas = baslangic ?? new DateOnly(bugun.Year, 1, 1);
        var son = bitis ?? bugun;
        if (bas > son)
        {
            return Hata.Dogrulama("tarih_araligi", "Başlangıç tarihi bitiş tarihinden sonra olamaz.").Problem();
        }

        var kart = await db.StokKartlari.AsNoTracking()
            .Where(k => k.Id == kartId)
            .Select(k => new { k.Id, k.Kod, k.Ad, k.TemelBirim })
            .SingleOrDefaultAsync(cancellationToken);
        if (kart is null)
        {
            return StokHatalari.KartBulunamadi.Problem();
        }

        var ayar = await ayarServisi.GetirAsync(cancellationToken);
        var hareketler = await (
                from h in db.StokHareketleri.AsNoTracking()
                join d in db.Depolar on h.DepoId equals d.Id
                where h.StokKartiId == kartId && h.Tarih <= son
                orderby h.Tarih, h.Id
                select new
                {
                    h.Id, h.Tarih, h.Tur, h.DepoId, DepoAdi = d.Ad, h.BelgeNo, h.Aciklama, h.GirisMiktari, h.CikisMiktari, h.BirimFiyat, h.KaynakBelgeId,
                    IptalliMi = h.IptalHareketId != null || h.IptalEdilenHareketId != null
                })
            .ToListAsync(cancellationToken);

        var maliyet = MaliyetHesaplayicilari.Getir(ayar.MaliyetYontemi).Hesapla(hareketler
            .Where(h => !h.IptalliMi && !StokHareket.TransferMi(h.Tur))
            .Select(h => new MaliyetHareketi(h.Id, h.GirisMiktari, h.CikisMiktari, h.BirimFiyat)));

        var maliyetGorur = await yetki.GorurMuAsync(cancellationToken);
        var kapsam = hareketler.Where(h => depoId is null || h.DepoId == depoId).ToList();
        var bakiye = kapsam.Where(h => h.Tarih < bas).Sum(h => h.GirisMiktari - h.CikisMiktari);
        var devir = bakiye;
        var satirlar = new List<StokEkstreSatiri>();
        foreach (var h in kapsam.Where(h => h.Tarih >= bas))
        {
            bakiye += h.GirisMiktari - h.CikisMiktari;
            var birimMaliyet = !maliyetGorur ? 0m
                : h.CikisMiktari > 0 ? maliyet.CikisBirimMaliyetleri.GetValueOrDefault(h.Id, h.BirimFiyat ?? 0m) : h.BirimFiyat ?? 0m;
            satirlar.Add(new StokEkstreSatiri(h.Id, h.Tarih, h.Tur, h.DepoAdi, h.BelgeNo, h.Aciklama, h.GirisMiktari, h.CikisMiktari,
                bakiye, Math.Round(birimMaliyet, 4), Para.Yuvarla((h.GirisMiktari + h.CikisMiktari) * birimMaliyet), h.KaynakBelgeId));
        }

        return TypedResults.Ok(new StokEkstresi(
            kart.Id, kart.Kod, kart.Ad, kart.TemelBirim, depoId, bas, son, ayar.MaliyetYontemi, devir, satirlar,
            satirlar.Sum(s => s.Giris), satirlar.Sum(s => s.Cikis), bakiye,
            maliyetGorur ? Math.Round(maliyet.BirimMaliyet, 4) : 0m, maliyetGorur ? Para.Yuvarla(bakiye * maliyet.BirimMaliyet) : 0m));
    }

    // Ana sayfa özet paneli.
    private static async Task<IResult> OzetAsync(
        StokDbContext db, StokHesaplayici hesaplayici, StokAyarServisi ayarServisi, IYetkiDenetimi yetki, CancellationToken cancellationToken)
    {
        var ayar = await ayarServisi.GetirAsync(cancellationToken);
        var maliyetGorur = await yetki.GorurMuAsync(cancellationToken);
        var kartlar = db.StokKartlari.AsNoTracking().Where(k => k.Tur == StokTuru.Urun && k.AktifMi);
        var kartListesi = await kartlar.Select(k => new { k.Id, k.Ad, k.TemelBirim, k.KritikSeviye }).ToListAsync(cancellationToken);
        var hesaplar = await hesaplayici.HesaplaAsync(kartlar.Select(k => k.Id), null, ayar.MaliyetYontemi, cancellationToken);
        var depolar = await db.Depolar.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Ad, cancellationToken);

        var durumlar = kartListesi.Select(k =>
        {
            var hesap = hesaplar.GetValueOrDefault(k.Id);
            return new { k.Id, k.Ad, k.TemelBirim, k.KritikSeviye, Miktar = hesap?.Miktar ?? 0m, Hesap = hesap };
        }).ToList();

        var kritikler = durumlar.Where(d => d.KritikSeviye is { } s && d.Miktar <= s).ToList();
        var depoDegerleri = durumlar
            .Where(d => maliyetGorur && d.Hesap is not null)
            .SelectMany(d => d.Hesap!.DepoMiktarlari.Select(m => (DepoId: m.Key, Deger: m.Value * d.Hesap.BirimMaliyet)))
            .GroupBy(x => x.DepoId)
            .Select(g => new DepoDegeri(depolar.GetValueOrDefault(g.Key, "?"), Para.Yuvarla(g.Sum(x => x.Deger))))
            .OrderByDescending(d => d.Deger)
            .ToList();

        var sonHareketler = await (
                from h in db.StokHareketleri.AsNoTracking()
                join k in db.StokKartlari on h.StokKartiId equals k.Id
                orderby h.Id descending
                select new SonStokHareketi(k.Id, k.Ad, k.TemelBirim, h.Tarih, h.Tur, h.GirisMiktari, h.CikisMiktari))
            .Take(OzetListeBoyutu)
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new StokOzetRaporu(
            durumlar.Count,
            maliyetGorur ? Para.Yuvarla(durumlar.Sum(d => d.Hesap?.Deger ?? 0m)) : 0m,
            kritikler.Count,
            durumlar.Count(d => d.Miktar < 0),
            ayar.MaliyetYontemi,
            depoDegerleri,
            kritikler.OrderBy(d => d.KritikSeviye == 0 ? 0 : d.Miktar / d.KritikSeviye!.Value).Take(OzetListeBoyutu)
                .Select(d => new KritikStok(d.Id, d.Ad, d.TemelBirim, d.Miktar, d.KritikSeviye!.Value)).ToList(),
            sonHareketler));
    }
}
