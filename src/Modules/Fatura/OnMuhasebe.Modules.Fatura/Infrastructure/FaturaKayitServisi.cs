using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Modules.Fatura.Domain;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Fatura.Infrastructure;

internal sealed record FaturaIslemSonucu(FaturaBelgesi Fatura, string? Uyari);

// Faturayı kesinleştiren işlemler. Fatura, cari hareketi ve stok hareketleri tek transaction'da yazılır:
// biri başarısız olursa (ör. eksi stok engeli) hiçbiri kalıcı olmaz.
internal sealed class FaturaKayitServisi(
    FaturaDbContext db,
    FaturaHazirlayici hazirlayici,
    FaturaAyarServisi ayarServisi,
    FaturaNumaraUretici numaraUretici,
    ICariHareketYazici cariYazici,
    IStokHareketYazici stokYazici,
    IKasaBankaYazici kasaYazici,
    IrsaliyeKayitServisi irsaliyeServisi,
    SiparisKarsilamaServisi siparisServisi,
    IIslemKapsami islemKapsami,
    TimeProvider zaman)
{
    public async Task<Sonuc<FaturaIslemSonucu>> OlusturAsync(FaturaKaydetIstegi istek, CancellationToken cancellationToken)
    {
        var bilgiler = await hazirlayici.HazirlaAsync(istek, cancellationToken);
        if (!bilgiler.BasariliMi)
        {
            return bilgiler.Hata;
        }

        var olusturma = FaturaBelgesi.Olustur(bilgiler.Deger);
        if (!olusturma.BasariliMi)
        {
            return olusturma.Hata;
        }

        var fatura = olusturma.Deger;
        db.Faturalar.Add(fatura);
        var ayar = await ayarServisi.GetirAsync(cancellationToken);
        if (ayar.KayitAkisi == KayitAkisi.TaslakOnay)
        {
            await db.SaveChangesAsync(cancellationToken);
            return new FaturaIslemSonucu(fatura, null);
        }

        return await islemKapsami.CalistirAsync(ct => KesinlestirAsync(fatura, ayar, ct), cancellationToken);
    }

    public async Task<Sonuc<FaturaIslemSonucu>> GuncelleAsync(Guid id, FaturaKaydetIstegi istek, CancellationToken cancellationToken)
    {
        var fatura = await db.Faturalar.Include(f => f.Satirlar).SingleOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (fatura is null)
        {
            return FaturaHatalari.FaturaBulunamadi;
        }

        // İstemcinin gördüğü sürüm; arada başkası değiştirdiyse kayıt 409 ile reddedilir.
        db.Entry(fatura).Property(f => f.Surum).OriginalValue = istek.Surum;

        var ayar = await ayarServisi.GetirAsync(cancellationToken);
        if (fatura.Durum == FaturaDurumu.Onayli && ayar.KayitAkisi == KayitAkisi.TaslakOnay)
        {
            return FaturaHatalari.OnayliDuzenlenemez;
        }

        var bilgiler = await hazirlayici.HazirlaAsync(istek, cancellationToken);
        if (!bilgiler.BasariliMi)
        {
            return bilgiler.Hata;
        }

        // Düzenleme satırları yeniden kurar; eski irsaliye bağları da yeniden hesaplanabilsin diye önce alınır.
        var eskiBaglar = IrsaliyeBaglari(fatura);
        var eskiSiparisBaglari = SiparisKarsilamaServisi.BagIdleri(fatura);
        var guncelleme = fatura.Guncelle(bilgiler.Deger);
        if (!guncelleme.BasariliMi)
        {
            return guncelleme.Hata;
        }

        if (fatura.Durum == FaturaDurumu.Taslak)
        {
            await db.SaveChangesAsync(cancellationToken);
            return new FaturaIslemSonucu(fatura, null);
        }

        // Doğrudan kayıtta onaylı fatura düzenlenebilir: eski hareketler ters kayıtla kapatılıp yenileri yazılır.
        return await islemKapsami.CalistirAsync<FaturaIslemSonucu>(async ct =>
        {
            if (await NumaraKontroluAsync(fatura, ct) is { } hata)
            {
                return hata;
            }

            await db.SaveChangesAsync(ct);
            var iptal = await HareketleriIptalEtAsync(fatura.Id, ct);
            if (!iptal.BasariliMi)
            {
                return iptal.Hata;
            }

            var yazim = await HareketleriYazAsync(fatura, ct);
            if (!yazim.BasariliMi)
            {
                return yazim.Hata;
            }

            var irsaliye = await IrsaliyeleriEsitleAsync(fatura, eskiBaglar, ct);
            if (!irsaliye.BasariliMi)
            {
                return irsaliye.Hata;
            }

            var siparis = await SiparisleriEsitleAsync(fatura, eskiSiparisBaglari, ct);
            return siparis.BasariliMi ? new FaturaIslemSonucu(fatura, Birlestir(yazim.Deger, irsaliye.Deger)) : siparis.Hata;
        }, cancellationToken);
    }

    public async Task<Sonuc<FaturaIslemSonucu>> OnaylaAsync(Guid id, CancellationToken cancellationToken)
    {
        var fatura = await db.Faturalar.Include(f => f.Satirlar).SingleOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (fatura is null)
        {
            return FaturaHatalari.FaturaBulunamadi;
        }

        if (fatura.Durum != FaturaDurumu.Taslak)
        {
            return fatura.Durum == FaturaDurumu.Onayli ? FaturaHatalari.ZatenOnayli : FaturaHatalari.IptalEdilmis;
        }

        var ayar = await ayarServisi.GetirAsync(cancellationToken);
        return await islemKapsami.CalistirAsync(ct => KesinlestirAsync(fatura, ayar, ct), cancellationToken);
    }

    public async Task<Sonuc<FaturaIslemSonucu>> IptalEtAsync(Guid id, CancellationToken cancellationToken)
    {
        var fatura = await db.Faturalar.Include(f => f.Satirlar).SingleOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (fatura is null)
        {
            return FaturaHatalari.FaturaBulunamadi;
        }

        var sonuc = fatura.IptalEt(zaman.GetUtcNow().UtcDateTime);
        if (!sonuc.BasariliMi)
        {
            return sonuc.Hata;
        }

        return await islemKapsami.CalistirAsync<FaturaIslemSonucu>(async ct =>
        {
            await db.SaveChangesAsync(ct);
            var iptal = await HareketleriIptalEtAsync(fatura.Id, ct);
            if (!iptal.BasariliMi)
            {
                return iptal.Hata;
            }

            // İptal edilen faturanın irsaliye bağları düşer; irsaliyeler yeniden faturalanabilir hale gelir.
            var irsaliye = await IrsaliyeleriEsitleAsync(fatura, [], ct);
            if (!irsaliye.BasariliMi)
            {
                return irsaliye.Hata;
            }

            var siparis = await SiparisleriEsitleAsync(fatura, SiparisKarsilamaServisi.BagIdleri(fatura), ct);
            return siparis.BasariliMi ? new FaturaIslemSonucu(fatura, null) : siparis.Hata;
        }, cancellationToken);
    }

    // Numara transaction içinde alınır: işlem geri alınırsa sayaç da geri alınır, sıra boşluk vermez.
    private async Task<Sonuc<FaturaIslemSonucu>> KesinlestirAsync(FaturaBelgesi fatura, FaturaAyari ayar, CancellationToken cancellationToken)
    {
        var numara = fatura.GidenMi && fatura.Numara is null
            ? await numaraUretici.SonrakiAsync(ayar.Seri(fatura.Tur, fatura.SatisSekli), fatura.Tarih.Year, cancellationToken)
            : null;

        var onay = fatura.Onayla(numara, zaman.GetUtcNow().UtcDateTime);
        if (!onay.BasariliMi)
        {
            return onay.Hata;
        }

        if (await NumaraKontroluAsync(fatura, cancellationToken) is { } hata)
        {
            return hata;
        }

        await db.SaveChangesAsync(cancellationToken);
        var yazim = await HareketleriYazAsync(fatura, cancellationToken);
        if (!yazim.BasariliMi)
        {
            return yazim.Hata;
        }

        var irsaliye = await IrsaliyeleriEsitleAsync(fatura, [], cancellationToken);
        if (!irsaliye.BasariliMi)
        {
            return irsaliye.Hata;
        }

        var siparis = await SiparisleriEsitleAsync(fatura, [], cancellationToken);
        return siparis.BasariliMi ? new FaturaIslemSonucu(fatura, Birlestir(yazim.Deger, irsaliye.Deger)) : siparis.Hata;
    }

    private Task<Sonuc> SiparisleriEsitleAsync(FaturaBelgesi f, IEnumerable<Guid> eskiBaglar, CancellationToken cancellationToken) =>
        siparisServisi.EsitleAsync(f.CariHesapId, TeklifSiparisTurleri.SiparisTuru(f.Tur), SiparisKarsilamaServisi.Baglar(f), eskiBaglar, cancellationToken);

    private static List<Guid> IrsaliyeBaglari(FaturaBelgesi f) =>
        f.Satirlar.Where(s => s.IrsaliyeSatiriId is not null).Select(s => s.IrsaliyeSatiriId!.Value).Distinct().ToList();

    private static string? Birlestir(string? a, string? b) => a is null ? b : b is null ? a : $"{a} {b}";

    // Faturanın (ve düzenlemede eski hâlinin) bağlı olduğu irsaliye satırlarının faturalanan miktarı, onaylı faturalardaki
    // bağlı satırların toplamından yeniden hesaplanır. Onaylı faturada bağların geçerliliği denetlenir; alış irsaliyesinin
    // stok maliyeti fatura tutarıyla ağırlıklandırılıp yeniden yazılır. Sevkten 7 gün geçmişse uyarı döner (VUK 231/5).
    private async Task<Sonuc<string?>> IrsaliyeleriEsitleAsync(FaturaBelgesi f, IReadOnlyList<Guid> ekBaglar, CancellationToken cancellationToken)
    {
        var idler = IrsaliyeBaglari(f).Concat(ekBaglar).Distinct().ToList();
        if (idler.Count == 0)
        {
            return (string?)null;
        }

        var irsaliyeler = await db.Irsaliyeler.Include(i => i.Satirlar)
            .Where(i => i.Satirlar.Any(s => idler.Contains(s.Id)))
            .ToListAsync(cancellationToken);
        var satirlar = irsaliyeler
            .SelectMany(i => i.Satirlar.Select(s => (Irsaliye: i, Satir: s)))
            .Where(x => idler.Contains(x.Satir.Id))
            .ToDictionary(x => x.Satir.Id);

        var faturaBaglari = f.Durum == FaturaDurumu.Onayli ? f.Satirlar.Where(s => s.IrsaliyeSatiriId is not null).ToList() : [];
        foreach (var fs in faturaBaglari)
        {
            if (!satirlar.TryGetValue(fs.IrsaliyeSatiriId!.Value, out var x)
                || x.Irsaliye.Durum != IrsaliyeDurumu.Onayli
                || IrsaliyeTurleri.Tur(f.Tur) != x.Irsaliye.Tur
                || x.Irsaliye.CariHesapId != f.CariHesapId
                || x.Satir.StokKartiId != fs.StokKartiId || x.Satir.Birim != fs.Birim || x.Satir.DepoId != fs.DepoId)
            {
                return FaturaHatalari.IrsaliyeBagiGecersiz;
            }
        }

        var toplamlar = await db.FaturaSatirlari
            .Where(s => s.IrsaliyeSatiriId != null && idler.Contains(s.IrsaliyeSatiriId.Value)
                        && db.Faturalar.Any(fa => fa.Id == s.FaturaId && fa.Durum == FaturaDurumu.Onayli))
            .GroupBy(s => s.IrsaliyeSatiriId!.Value)
            .Select(g => new { Id = g.Key, Miktar = g.Sum(s => s.Miktar) })
            .ToDictionaryAsync(g => g.Id, g => g.Miktar, cancellationToken);

        foreach (var (id, x) in satirlar)
        {
            var ayar = x.Irsaliye.FaturalananAyarla(id, toplamlar.GetValueOrDefault(id));
            if (!ayar.BasariliMi)
            {
                return ayar.Hata;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var maliyet = await AlisMaliyetleriniYenileAsync(irsaliyeler, cancellationToken);
        if (!maliyet.BasariliMi)
        {
            return maliyet.Hata;
        }

        var gecikenler = faturaBaglari
            .Select(fs => satirlar[fs.IrsaliyeSatiriId!.Value].Irsaliye)
            .Distinct()
            .Where(i => IrsaliyeHesaplari.FaturaSuresiGectiMi(i.SevkTarihi, f.Tarih))
            .Select(i => i.Numara)
            .ToList();
        return gecikenler.Count == 0
            ? (string?)null
            : $"{string.Join(", ", gecikenler)} numaralı irsaliyenin sevkinden bu yana {IrsaliyeHesaplari.FaturaSuresiGun} günlük fatura süresi geçmiş (VUK 231/5).";
    }

    // Alış irsaliyesi stoğa tahmini maliyetle girmişti; bağlı onaylı faturaların gerçek maliyeti (iskontolu, KDV hariç TL)
    // faturalanan miktar kadar, kalanı tahmini maliyetle ağırlıklandırılır ve irsaliyenin stok hareketleri yeniden yazılır.
    private async Task<Sonuc> AlisMaliyetleriniYenileAsync(IReadOnlyList<Irsaliye> irsaliyeler, CancellationToken cancellationToken)
    {
        var alislar = irsaliyeler.Where(i => i.Tur == IrsaliyeTuru.Alis && i.Durum == IrsaliyeDurumu.Onayli).ToList();
        if (alislar.Count == 0)
        {
            return Sonuc.Basarili();
        }

        var satirIdleri = alislar.SelectMany(i => i.Satirlar).Select(s => s.Id).ToList();
        var parcalar = await (
                from s in db.FaturaSatirlari
                join fa in db.Faturalar on s.FaturaId equals fa.Id
                where fa.Durum == FaturaDurumu.Onayli && s.IrsaliyeSatiriId != null && satirIdleri.Contains(s.IrsaliyeSatiriId.Value)
                select new { Id = s.IrsaliyeSatiriId!.Value, s.Miktar, BirimMaliyet = s.Matrah * fa.Kur / s.Miktar })
            .ToListAsync(cancellationToken);

        foreach (var i in alislar)
        {
            var maliyetler = i.Satirlar.ToDictionary(s => s.Id, s => IrsaliyeHesaplari.AgirlikliMaliyet(
                s.Miktar, IrsaliyeKayitServisi.TahminiMaliyet(i, s),
                parcalar.Where(p => p.Id == s.Id).Select(p => (p.Miktar, p.BirimMaliyet))));

            var iptal = await stokYazici.IptalEtAsync(i.Id, cancellationToken);
            if (!iptal.BasariliMi)
            {
                return iptal;
            }

            var yazim = await irsaliyeServisi.StokuYazAsync(i, maliyetler, cancellationToken);
            if (!yazim.BasariliMi)
            {
                return yazim.Hata;
            }
        }

        return Sonuc.Basarili();
    }

    // Gelen faturanın numarası karşı tarafındır; aynı carinin aynı türde aynı numaralı ikinci faturası kabul edilmez.
    // Giden faturada numara seri + yıl + sıradır: yıl fatura yılıyla aynı kalmalı ve aynı seride tarih sırası bozulmamalıdır.
    // Transaction içinde, sayaç satırı kilitliyken çağrılır; eşzamanlı numaralandırma sırayı bozamaz.
    private async Task<Hata?> NumaraKontroluAsync(FaturaBelgesi fatura, CancellationToken cancellationToken)
    {
        if (!fatura.GidenMi)
        {
            var cakisiyor = fatura.CariHesapId is not null && await db.Faturalar.AnyAsync(
                f => f.Id != fatura.Id && f.Tur == fatura.Tur && f.CariHesapId == fatura.CariHesapId
                     && f.Numara == fatura.Numara && f.Durum != FaturaDurumu.Iptal, cancellationToken);
            return cakisiyor ? FaturaHatalari.NumaraKullaniliyor : null;
        }

        var numara = fatura.Numara!;
        if (numara.Length < 7 || numara[3..7] != fatura.Tarih.Year.ToString(CultureInfo.InvariantCulture))
        {
            return FaturaHatalari.NumaraYiliDegistirilemez;
        }

        var onek = numara[..7];
        var ayniSeri = db.Faturalar.Where(f => f.Id != fatura.Id && f.Durum != FaturaDurumu.Taslak
                                               && f.Numara != null && f.Numara.StartsWith(onek) && f.Numara.Length == numara.Length);
        var oncekiEnGec = await ayniSeri.Where(f => string.Compare(f.Numara, numara) < 0).MaxAsync(f => (DateOnly?)f.Tarih, cancellationToken);
        if (oncekiEnGec > fatura.Tarih)
        {
            return FaturaHatalari.TarihSirasi(oncekiEnGec.Value, oncekiMi: true);
        }

        var sonrakiEnErken = await ayniSeri.Where(f => string.Compare(f.Numara, numara) > 0).MinAsync(f => (DateOnly?)f.Tarih, cancellationToken);
        return sonrakiEnErken < fatura.Tarih ? FaturaHatalari.TarihSirasi(sonrakiEnErken.Value, oncekiMi: false) : null;
    }

    private async Task<Sonuc<string?>> HareketleriYazAsync(FaturaBelgesi f, CancellationToken cancellationToken)
    {
        var aciklama = $"{FaturaMetinleri.TurAdi(f.Tur)} - {f.CariUnvan}";
        if (f.CariHesapId is { } cariId)
        {
            var (cariTuru, yon) = CariEtkisi(f.Tur);
            // Cariye yansıyan tutar ödenecek tutardır: tevkif edilen KDV ve stopaj vergi dairesine gider, cariye değil.
            var cari = await cariYazici.YazAsync(f.Id,
                new BelgeCariHareketi(cariId, cariTuru, yon, f.Tarih, f.VadeTarihi, f.OdenecekTutar, f.Kur, f.Numara, aciklama,
                    OtomatikKapama: f.PesinHesapId is null),
                cancellationToken);
            if (!cari.BasariliMi)
            {
                return cari.Hata;
            }
        }

        // Serbest meslek makbuzu yalnızca hizmettir; stokla ilişkili türlerde (satış/alış/iade/gider pusulası) ürün satırları hareket yaratır.
        Sonuc<string?> stok = (string?)null;
        if (FaturaTurleri.StoklaIliskiliMi(f.Tur))
        {
            var stokTuru = StokEtkisi(f.Tur);
            // İrsaliyeden aktarılan satırın stok hareketi irsaliyededir; fatura yalnızca irsaliyesiz ürün satırları için stok yazar.
            var stokHareketleri = f.Satirlar
                .Where(s => s.StokluMu && s.IrsaliyeSatiriId is null)
                .OrderBy(s => s.Sira)
                .Select(s => new BelgeStokHareketi(
                    s.StokKartiId!.Value, s.DepoId!.Value, stokTuru, f.Tarih, s.Birim, s.Miktar, StokMaliyeti(f, s), f.Numara, aciklama,
                    s.LotNo, s.SonKullanmaTarihi, s.SeriNolari))
                .ToList();

            stok = await stokYazici.YazAsync(f.Id, stokHareketleri, cancellationToken);
        }

        if (!stok.BasariliMi || f.PesinHesapId is not { } hesapId)
        {
            return stok;
        }

        // Peşin tahsilat/ödeme: cari varsa tahsilat önce bu faturayı kapatır; nihai tüketicide para doğrudan kasaya girer.
        var pesin = await kasaYazici.BelgeTahsilatiYazAsync(f.Id,
            new BelgeTahsilati(hesapId, f.CariHesapId, FaturaTurleri.ParaGirisiMi(f.Tur), f.Tarih, f.OdenecekTutar, f.Kur, f.Numara, aciklama),
            cancellationToken);
        return pesin.BasariliMi ? stok : pesin.Hata;
    }

    private async Task<Sonuc> HareketleriIptalEtAsync(Guid faturaId, CancellationToken cancellationToken)
    {
        var kasa = await kasaYazici.BelgeIslemleriniIptalEtAsync(faturaId, cancellationToken);
        if (!kasa.BasariliMi)
        {
            return kasa;
        }

        var cari = await cariYazici.IptalEtAsync(faturaId, cancellationToken);
        return cari.BasariliMi ? await stokYazici.IptalEtAsync(faturaId, cancellationToken) : cari;
    }

    // Alışta stok maliyeti, satırın iskontolar ve genel iskonto payı düşülmüş KDV hariç TL tutarıdır (girilen birim başına).
    // Satıştan iade maliyetini stok modülü iade tarihindeki birim maliyetten bulur; çıkışların maliyeti rapor anında hesaplanır.
    // Alış ve gider pusulasında stok maliyeti, satırın iskontolu KDV hariç TL tutarıdır (girilen birim başına).
    private static decimal? StokMaliyeti(FaturaBelgesi f, FaturaSatiri s) => f.Tur is FaturaTuru.Alis or FaturaTuru.GiderPusulasi
        ? Math.Round(s.Matrah * f.Kur / s.Miktar, 4, MidpointRounding.AwayFromZero)
        : null;

    // Gider pusulası ve SMM cariyi alacaklandırır (biz borçlanırız), alış faturası gibi.
    private static (CariHareketTuru Tur, HareketYonu Yon) CariEtkisi(FaturaTuru tur) => tur switch
    {
        FaturaTuru.Satis => (CariHareketTuru.SatisFaturasi, HareketYonu.Borc),
        FaturaTuru.Alis => (CariHareketTuru.AlisFaturasi, HareketYonu.Alacak),
        FaturaTuru.SatistanIade => (CariHareketTuru.SatistanIadeFaturasi, HareketYonu.Alacak),
        FaturaTuru.AlistanIade => (CariHareketTuru.AlistanIadeFaturasi, HareketYonu.Borc),
        FaturaTuru.GiderPusulasi => (CariHareketTuru.GiderPusulasi, HareketYonu.Alacak),
        FaturaTuru.SerbestMeslekMakbuzu => (CariHareketTuru.SerbestMeslekMakbuzu, HareketYonu.Alacak),
        _ => throw new ArgumentOutOfRangeException(nameof(tur))
    };

    private static StokHareketTuru StokEtkisi(FaturaTuru tur) => tur switch
    {
        FaturaTuru.Satis => StokHareketTuru.SatisFaturasi,
        FaturaTuru.Alis => StokHareketTuru.AlisFaturasi,
        FaturaTuru.SatistanIade => StokHareketTuru.SatistanIade,
        FaturaTuru.AlistanIade => StokHareketTuru.AlistanIade,
        FaturaTuru.GiderPusulasi => StokHareketTuru.AlisFaturasi,
        _ => throw new ArgumentOutOfRangeException(nameof(tur))
    };
}

internal static class FaturaMetinleri
{
    public static string TurAdi(FaturaTuru tur) => tur switch
    {
        FaturaTuru.Satis => "Satış faturası",
        FaturaTuru.Alis => "Alış faturası",
        FaturaTuru.SatistanIade => "Satıştan iade faturası",
        FaturaTuru.AlistanIade => "Alıştan iade faturası",
        FaturaTuru.GiderPusulasi => "Gider pusulası",
        FaturaTuru.SerbestMeslekMakbuzu => "Serbest meslek makbuzu",
        _ => tur.ToString()
    };
}
