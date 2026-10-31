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
            return yazim.BasariliMi ? new FaturaIslemSonucu(fatura, yazim.Deger) : yazim.Hata;
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
            return iptal.BasariliMi ? new FaturaIslemSonucu(fatura, null) : iptal.Hata;
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
        return yazim.BasariliMi ? new FaturaIslemSonucu(fatura, yazim.Deger) : yazim.Hata;
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

        var stokTuru = StokEtkisi(f.Tur);
        var stokHareketleri = f.Satirlar
            .Where(s => s.StokluMu)
            .OrderBy(s => s.Sira)
            .Select(s => new BelgeStokHareketi(
                s.StokKartiId!.Value, s.DepoId!.Value, stokTuru, f.Tarih, s.Birim, s.Miktar, StokMaliyeti(f, s), f.Numara, aciklama))
            .ToList();

        var stok = await stokYazici.YazAsync(f.Id, stokHareketleri, cancellationToken);
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
    private static decimal? StokMaliyeti(FaturaBelgesi f, FaturaSatiri s) => f.Tur == FaturaTuru.Alis
        ? Math.Round(s.Matrah * f.Kur / s.Miktar, 4, MidpointRounding.AwayFromZero)
        : null;

    private static (CariHareketTuru Tur, HareketYonu Yon) CariEtkisi(FaturaTuru tur) => tur switch
    {
        FaturaTuru.Satis => (CariHareketTuru.SatisFaturasi, HareketYonu.Borc),
        FaturaTuru.Alis => (CariHareketTuru.AlisFaturasi, HareketYonu.Alacak),
        FaturaTuru.SatistanIade => (CariHareketTuru.SatistanIadeFaturasi, HareketYonu.Alacak),
        FaturaTuru.AlistanIade => (CariHareketTuru.AlistanIadeFaturasi, HareketYonu.Borc),
        _ => throw new ArgumentOutOfRangeException(nameof(tur))
    };

    private static StokHareketTuru StokEtkisi(FaturaTuru tur) => tur switch
    {
        FaturaTuru.Satis => StokHareketTuru.SatisFaturasi,
        FaturaTuru.Alis => StokHareketTuru.AlisFaturasi,
        FaturaTuru.SatistanIade => StokHareketTuru.SatistanIade,
        FaturaTuru.AlistanIade => StokHareketTuru.AlistanIade,
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
