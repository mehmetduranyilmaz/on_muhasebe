using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.Doviz.Contracts;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Modules.Fatura.Domain;
using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Fatura.Infrastructure;

internal sealed record IrsaliyeIslemSonucu(Irsaliye Irsaliye, string? Uyari);

// İsteği cari, stok, depo ve kur bilgileriyle çözümler. İrsaliye stok hareketi oluşturduğu için Stok lisansı şarttır.
internal sealed class IrsaliyeHazirlayici(
    ICariSorgusu cariSorgusu,
    IStokSorgusu stokSorgusu,
    IKurSorgusu kurSorgusu,
    ILisansSorgusu lisansSorgusu,
    IMevcutKullanici mevcutKullanici)
{
    public const string DepoSevkUnvani = "Depolar arası sevk";

    public async Task<Sonuc<IrsaliyeBilgileri>> HazirlaAsync(IrsaliyeKaydetIstegi istek, CancellationToken cancellationToken)
    {
        var lisans = await lisansSorgusu.GetirAsync(mevcutKullanici.FirmaId!.Value, cancellationToken);
        var moduller = lisans?.Moduller ?? [];
        if (!moduller.Contains(ModulKodlari.Stok))
        {
            return IrsaliyeHatalari.StokLisansiYok;
        }

        FaturaCarisi? cari = null;
        var paraBirimi = ParaBirimleri.Tl;
        if (istek.CariHesapId is { } cariId)
        {
            if (!moduller.Contains(ModulKodlari.Cari))
            {
                return IrsaliyeHatalari.CariLisansiYok;
            }

            var c = await cariSorgusu.GetirAsync(cariId, cancellationToken);
            if (c is null)
            {
                return FaturaHatalari.CariBulunamadi;
            }

            if (!c.AktifMi)
            {
                return IrsaliyeHatalari.PasifCari;
            }

            cari = new FaturaCarisi(c.Id, c.Kod, c.Unvan, c.VergiNo, c.VergiDairesi, c.Il, c.Ilce, c.Adres);
            paraBirimi = c.ParaBirimi;
        }

        var kur = 1m;
        if (paraBirimi != ParaBirimleri.Tl)
        {
            if ((istek.Kur ?? (await kurSorgusu.IslemKuruAsync(paraBirimi, istek.SevkTarihi, cancellationToken))?.DovizAlis) is not { } bulunan)
            {
                return FaturaHatalari.KurGerekli;
            }

            kur = Math.Round(bulunan, FaturaBelgesi.KurOndalik, MidpointRounding.AwayFromZero);
        }

        var satirlar = await SatirlarAsync(istek.Satirlar, cancellationToken);
        if (!satirlar.BasariliMi)
        {
            return satirlar.Hata;
        }

        // Hedef depo yalnızca açıkça seçilirse çözülür; boşsa varsayılan depoya düşmemeli (domain "hedef depo gerekli" der).
        Guid? hedefDepo = null;
        if (istek.Tur == IrsaliyeTuru.DepoSevk && istek.HedefDepoId is { } hedef)
        {
            hedefDepo = await stokSorgusu.DepoCozAsync(hedef, cancellationToken);
            if (hedefDepo is null)
            {
                return FaturaHatalari.DepoBulunamadi;
            }
        }

        return new IrsaliyeBilgileri(
            istek.Tur, cari, istek.Tur == IrsaliyeTuru.DepoSevk ? DepoSevkUnvani : null, paraBirimi, kur,
            istek.Tarih, istek.SevkTarihi, istek.SevkSaati,
            IrsaliyeTurleri.GidenMi(istek.Tur) ? null : Temiz(istek.Numara)?.ToUpperInvariant(),
            hedefDepo, Temiz(istek.TeslimAdresi), Temiz(istek.AracPlakasi)?.ToUpperInvariant().Replace(" ", ""), Temiz(istek.SoforAdi),
            Temiz(istek.SoforTckn), Temiz(istek.TasiyiciUnvani), Temiz(istek.TasiyiciVkn), Temiz(istek.Aciklama), satirlar.Deger);
    }

    private async Task<Sonuc<IReadOnlyList<IrsaliyeSatiriBilgisi>>> SatirlarAsync(
        IReadOnlyList<IrsaliyeSatiriIstegi> istekler, CancellationToken cancellationToken)
    {
        var kartIdleri = istekler.Select(s => s.StokKartiId).Distinct().ToList();
        var kartlar = (await stokSorgusu.KartlariGetirAsync(kartIdleri, cancellationToken)).ToDictionary(k => k.Id);
        if (kartlar.Count != kartIdleri.Count)
        {
            return IrsaliyeHatalari.StokKartiBulunamadi;
        }

        var depolar = new Dictionary<Guid, Guid?>();
        Guid? varsayilanDepo = null;
        var sonuc = new List<IrsaliyeSatiriBilgisi>(istekler.Count);
        foreach (var s in istekler)
        {
            var kart = kartlar[s.StokKartiId];
            if (kart.Tur != StokTuru.Urun)
            {
                return IrsaliyeHatalari.UrunKartiGerekli;
            }

            if (!kart.AktifMi)
            {
                return IrsaliyeHatalari.PasifStokKarti;
            }

            if (!kart.BirimTanimliMi(s.Birim))
            {
                return FaturaHatalari.BirimTanimsiz;
            }

            Guid? depoId;
            if (s.DepoId is { } istenen)
            {
                if (!depolar.TryGetValue(istenen, out depoId))
                {
                    depoId = await stokSorgusu.DepoCozAsync(istenen, cancellationToken);
                    depolar[istenen] = depoId;
                }
            }
            else
            {
                depoId = varsayilanDepo ??= await stokSorgusu.DepoCozAsync(null, cancellationToken);
            }

            if (depoId is null)
            {
                return FaturaHatalari.DepoBulunamadi;
            }

            sonuc.Add(new IrsaliyeSatiriBilgisi(kart.Id, kart.Kod, Temiz(s.Ad) ?? kart.Ad, depoId.Value, s.Birim, s.Miktar, s.BirimFiyat, kart.KdvOrani));
        }

        return sonuc;
    }

    private static string? Temiz(string? deger) => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();
}

// İrsaliyeyi kesinleştiren işlemler. İrsaliye ve stok hareketleri tek transaction'da yazılır.
internal sealed class IrsaliyeKayitServisi(
    FaturaDbContext db,
    IrsaliyeHazirlayici hazirlayici,
    FaturaAyarServisi ayarServisi,
    FaturaNumaraUretici numaraUretici,
    IStokHareketYazici stokYazici,
    IIslemKapsami islemKapsami,
    TimeProvider zaman)
{
    public async Task<Sonuc<IrsaliyeIslemSonucu>> OlusturAsync(IrsaliyeKaydetIstegi istek, CancellationToken cancellationToken)
    {
        var bilgiler = await hazirlayici.HazirlaAsync(istek, cancellationToken);
        if (!bilgiler.BasariliMi)
        {
            return bilgiler.Hata;
        }

        var olusturma = Irsaliye.Olustur(bilgiler.Deger);
        if (!olusturma.BasariliMi)
        {
            return olusturma.Hata;
        }

        var irsaliye = olusturma.Deger;
        db.Irsaliyeler.Add(irsaliye);
        var ayar = await ayarServisi.GetirAsync(cancellationToken);
        if (ayar.KayitAkisi == KayitAkisi.TaslakOnay)
        {
            await db.SaveChangesAsync(cancellationToken);
            return new IrsaliyeIslemSonucu(irsaliye, null);
        }

        return await islemKapsami.CalistirAsync(ct => KesinlestirAsync(irsaliye, ayar, ct), cancellationToken);
    }

    public async Task<Sonuc<IrsaliyeIslemSonucu>> GuncelleAsync(Guid id, IrsaliyeKaydetIstegi istek, CancellationToken cancellationToken)
    {
        var irsaliye = await db.Irsaliyeler.Include(i => i.Satirlar).SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (irsaliye is null)
        {
            return IrsaliyeHatalari.IrsaliyeBulunamadi;
        }

        db.Entry(irsaliye).Property(i => i.Surum).OriginalValue = istek.Surum;

        var ayar = await ayarServisi.GetirAsync(cancellationToken);
        if (irsaliye.Durum == IrsaliyeDurumu.Onayli && ayar.KayitAkisi == KayitAkisi.TaslakOnay)
        {
            return IrsaliyeHatalari.OnayliDuzenlenemez;
        }

        var bilgiler = await hazirlayici.HazirlaAsync(istek, cancellationToken);
        if (!bilgiler.BasariliMi)
        {
            return bilgiler.Hata;
        }

        var guncelleme = irsaliye.Guncelle(bilgiler.Deger);
        if (!guncelleme.BasariliMi)
        {
            return guncelleme.Hata;
        }

        if (irsaliye.Durum == IrsaliyeDurumu.Taslak)
        {
            await db.SaveChangesAsync(cancellationToken);
            return new IrsaliyeIslemSonucu(irsaliye, null);
        }

        // Doğrudan kayıtta onaylı (faturalanmamış) irsaliye düzenlenebilir: stok hareketleri ters kayıtla yenilenir.
        return await islemKapsami.CalistirAsync<IrsaliyeIslemSonucu>(async ct =>
        {
            if (await NumaraKontroluAsync(irsaliye, ct) is { } hata)
            {
                return hata;
            }

            await db.SaveChangesAsync(ct);
            var iptal = await stokYazici.IptalEtAsync(irsaliye.Id, ct);
            if (!iptal.BasariliMi)
            {
                return iptal.Hata;
            }

            var yazim = await StokuYazAsync(irsaliye, null, ct);
            return yazim.BasariliMi ? new IrsaliyeIslemSonucu(irsaliye, yazim.Deger) : yazim.Hata;
        }, cancellationToken);
    }

    public async Task<Sonuc<IrsaliyeIslemSonucu>> OnaylaAsync(Guid id, CancellationToken cancellationToken)
    {
        var irsaliye = await db.Irsaliyeler.Include(i => i.Satirlar).SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (irsaliye is null)
        {
            return IrsaliyeHatalari.IrsaliyeBulunamadi;
        }

        if (irsaliye.Durum != IrsaliyeDurumu.Taslak)
        {
            return irsaliye.Durum == IrsaliyeDurumu.Onayli ? IrsaliyeHatalari.ZatenOnayli : IrsaliyeHatalari.IptalEdilmis;
        }

        var ayar = await ayarServisi.GetirAsync(cancellationToken);
        return await islemKapsami.CalistirAsync(ct => KesinlestirAsync(irsaliye, ayar, ct), cancellationToken);
    }

    public async Task<Sonuc<IrsaliyeIslemSonucu>> IptalEtAsync(Guid id, CancellationToken cancellationToken)
    {
        var irsaliye = await db.Irsaliyeler.Include(i => i.Satirlar).SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (irsaliye is null)
        {
            return IrsaliyeHatalari.IrsaliyeBulunamadi;
        }

        var sonuc = irsaliye.IptalEt(zaman.GetUtcNow().UtcDateTime);
        if (!sonuc.BasariliMi)
        {
            return sonuc.Hata;
        }

        return await islemKapsami.CalistirAsync<IrsaliyeIslemSonucu>(async ct =>
        {
            await db.SaveChangesAsync(ct);
            var iptal = await stokYazici.IptalEtAsync(irsaliye.Id, ct);
            return iptal.BasariliMi ? new IrsaliyeIslemSonucu(irsaliye, null) : iptal.Hata;
        }, cancellationToken);
    }

    // Stok hareketleri: alım/satım irsaliyeleri fatura ile aynı hareket türlerini kullanır (belge no ve açıklama irsaliyeyi gösterir);
    // depo sevki transfer çıkışı + girişidir (transfer firma geneli maliyeti etkilemez).
    // Alış irsaliyesinde maliyet, verilmişse satır bazında (fatura gelince ağırlıklandırılmış), yoksa irsaliyedeki tahmini maliyettir.
    public async Task<Sonuc<string?>> StokuYazAsync(Irsaliye i, IReadOnlyDictionary<Guid, decimal>? alisMaliyetleri, CancellationToken cancellationToken)
    {
        var aciklama = $"{IrsaliyeMetinleri.TurAdi(i.Tur)} - {i.CariUnvan}";
        var hareketler = new List<BelgeStokHareketi>();
        foreach (var s in i.Satirlar.OrderBy(s => s.Sira))
        {
            if (i.Tur == IrsaliyeTuru.DepoSevk)
            {
                hareketler.Add(new BelgeStokHareketi(s.StokKartiId, s.DepoId, StokHareketTuru.TransferCikis, i.SevkTarihi, s.Birim, s.Miktar, null, i.Numara, aciklama));
                hareketler.Add(new BelgeStokHareketi(s.StokKartiId, i.HedefDepoId!.Value, StokHareketTuru.TransferGiris, i.SevkTarihi, s.Birim, s.Miktar, null, i.Numara, aciklama));
                continue;
            }

            decimal? maliyet = null;
            if (i.Tur == IrsaliyeTuru.Alis)
            {
                maliyet = alisMaliyetleri is not null && alisMaliyetleri.TryGetValue(s.Id, out var agirlikli) ? agirlikli : TahminiMaliyet(i, s);
            }

            hareketler.Add(new BelgeStokHareketi(s.StokKartiId, s.DepoId, StokEtkisi(i.Tur), i.SevkTarihi, s.Birim, s.Miktar, maliyet, i.Numara, aciklama));
        }

        return await stokYazici.YazAsync(i.Id, hareketler, cancellationToken);
    }

    // Girilen birim başına TL maliyet.
    public static decimal TahminiMaliyet(Irsaliye i, IrsaliyeSatiri s) =>
        Math.Round((s.BirimFiyat ?? 0m) * i.Kur, IrsaliyeHesaplari.MaliyetOndalik, MidpointRounding.AwayFromZero);

    private async Task<Sonuc<IrsaliyeIslemSonucu>> KesinlestirAsync(Irsaliye irsaliye, FaturaAyari ayar, CancellationToken cancellationToken)
    {
        var numara = irsaliye.GidenMi && irsaliye.Numara is null
            ? await numaraUretici.SonrakiAsync(ayar.IrsaliyeSeri, irsaliye.Tarih.Year, cancellationToken)
            : null;

        var onay = irsaliye.Onayla(numara, zaman.GetUtcNow().UtcDateTime);
        if (!onay.BasariliMi)
        {
            return onay.Hata;
        }

        if (await NumaraKontroluAsync(irsaliye, cancellationToken) is { } hata)
        {
            return hata;
        }

        await db.SaveChangesAsync(cancellationToken);
        var yazim = await StokuYazAsync(irsaliye, null, cancellationToken);
        return yazim.BasariliMi ? new IrsaliyeIslemSonucu(irsaliye, yazim.Deger) : yazim.Hata;
    }

    // Gelen irsaliyede aynı carinin aynı türde aynı numaralı ikinci irsaliyesi kabul edilmez. Giden irsaliyede numara
    // seri + yıl + sıradır: yıl irsaliye yılıyla aynı kalmalı ve aynı seride tarih sırası bozulmamalıdır (VUK).
    private async Task<Hata?> NumaraKontroluAsync(Irsaliye irsaliye, CancellationToken cancellationToken)
    {
        if (!irsaliye.GidenMi)
        {
            var cakisiyor = await db.Irsaliyeler.AnyAsync(
                i => i.Id != irsaliye.Id && i.Tur == irsaliye.Tur && i.CariHesapId == irsaliye.CariHesapId
                     && i.Numara == irsaliye.Numara && i.Durum != IrsaliyeDurumu.Iptal, cancellationToken);
            return cakisiyor ? IrsaliyeHatalari.NumaraKullaniliyor : null;
        }

        var numara = irsaliye.Numara!;
        if (numara.Length < 7 || numara[3..7] != irsaliye.Tarih.Year.ToString(CultureInfo.InvariantCulture))
        {
            return FaturaHatalari.NumaraYiliDegistirilemez;
        }

        var onek = numara[..7];
        var ayniSeri = db.Irsaliyeler.Where(i => i.Id != irsaliye.Id && i.Durum != IrsaliyeDurumu.Taslak
                                                 && i.Numara != null && i.Numara.StartsWith(onek) && i.Numara.Length == numara.Length);
        var oncekiEnGec = await ayniSeri.Where(i => string.Compare(i.Numara, numara) < 0).MaxAsync(i => (DateOnly?)i.Tarih, cancellationToken);
        if (oncekiEnGec > irsaliye.Tarih)
        {
            return IrsaliyeHatalari.TarihSirasi(oncekiEnGec.Value, oncekiMi: true);
        }

        var sonrakiEnErken = await ayniSeri.Where(i => string.Compare(i.Numara, numara) > 0).MinAsync(i => (DateOnly?)i.Tarih, cancellationToken);
        return sonrakiEnErken < irsaliye.Tarih ? IrsaliyeHatalari.TarihSirasi(sonrakiEnErken.Value, oncekiMi: false) : null;
    }

    private static StokHareketTuru StokEtkisi(IrsaliyeTuru tur) => tur switch
    {
        IrsaliyeTuru.Satis => StokHareketTuru.SatisFaturasi,
        IrsaliyeTuru.Alis => StokHareketTuru.AlisFaturasi,
        IrsaliyeTuru.SatistanIade => StokHareketTuru.SatistanIade,
        IrsaliyeTuru.AlistanIade => StokHareketTuru.AlistanIade,
        _ => throw new ArgumentOutOfRangeException(nameof(tur))
    };
}

internal static class IrsaliyeMetinleri
{
    public static string TurAdi(IrsaliyeTuru tur) => tur switch
    {
        IrsaliyeTuru.Satis => "Satış irsaliyesi",
        IrsaliyeTuru.Alis => "Alış irsaliyesi",
        IrsaliyeTuru.SatistanIade => "Satıştan iade irsaliyesi",
        IrsaliyeTuru.AlistanIade => "Alıştan iade irsaliyesi",
        IrsaliyeTuru.DepoSevk => "Depolar arası sevk irsaliyesi",
        _ => tur.ToString()
    };
}
