using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.Doviz.Contracts;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.KasaBanka.Domain;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.KasaBanka.Infrastructure;

// Kasa/banka işlemleri. Her işlem (bacakları ve cari hareketi dahil) tek transaction'da yazılır ve tek IslemId ile iptal edilir.
// Cari hareketlerinin kaynak belgesi IslemId'dir; böylece cari ekstresinden işleme ulaşılır ve iptal birlikte yapılır.
internal sealed class KasaIslemServisi(
    KasaBankaDbContext db,
    ICariSorgusu cariSorgusu,
    ICariHareketYazici cariYazici,
    IKurSorgusu kurSorgusu,
    ILisansSorgusu lisansSorgusu,
    IMevcutKullanici mevcutKullanici,
    IIslemKapsami islemKapsami,
    TimeProvider zaman)
{
    public Task<Sonuc<Guid>> AcilisAsync(AcilisIstegi istek, CancellationToken cancellationToken) =>
        islemKapsami.CalistirAsync<Guid>(async ct =>
        {
            if (await HesapAsync(istek.HesapId, ct) is not { } hesap)
            {
                return KasaBankaHatalari.HesapBulunamadi;
            }

            if (await db.KasaHareketleri.AnyAsync(h => h.HesapId == hesap.Id && h.Tur == KasaHareketTuru.Acilis
                                                       && h.IptalHareketId == null && h.IptalEdilenHareketId == null, ct))
            {
                return KasaBankaHatalari.AcilisZatenVar;
            }

            var kur = await KurAsync(hesap.ParaBirimi, istek.Tarih, istek.Kur, ct);
            if (!kur.BasariliMi)
            {
                return kur.Hata;
            }

            var islemId = Guid.CreateVersion7();
            var hareket = KasaHareketi.Olustur(hesap, islemId, KasaHareketTuru.Acilis, istek.Tutar > 0, istek.Tarih, Math.Abs(istek.Tutar),
                kur.Deger, null, istek.Aciklama);
            return await KaydetAsync(islemId, ct, hareket);
        }, cancellationToken);

    // Tahsilat: hesaba giriş, cari alacaklanır. Ödeme: hesaptan çıkış, cari borçlanır.
    public Task<Sonuc<Guid>> TahsilatOdemeAsync(
        TahsilatOdemeIstegi istek, bool tahsilatMi, CancellationToken cancellationToken, Guid? kaynakBelgeId = null) =>
        islemKapsami.CalistirAsync<Guid>(async ct =>
        {
            if (await HesapAsync(istek.HesapId, ct) is not { } hesap)
            {
                return KasaBankaHatalari.HesapBulunamadi;
            }

            var kur = await KurAsync(hesap.ParaBirimi, istek.Tarih, istek.Kur, ct);
            if (!kur.BasariliMi)
            {
                return kur.Hata;
            }

            var islemId = Guid.CreateVersion7();
            var cari = await CariTutariAsync(istek.CariHesapId, hesap, istek.Tutar, kur.Deger, istek.CariKuru, istek.Tarih, ct);
            if (!cari.BasariliMi)
            {
                return cari.Hata;
            }

            var hareket = KasaHareketi.Olustur(hesap, islemId, tahsilatMi ? KasaHareketTuru.Tahsilat : KasaHareketTuru.Odeme, tahsilatMi,
                istek.Tarih, istek.Tutar, kur.Deger, istek.BelgeNo, istek.Aciklama ?? cari.Deger.Unvan, istek.CariHesapId,
                kaynakBelgeId: kaynakBelgeId);
            var kayit = await KaydetAsync(islemId, ct, hareket);
            if (!kayit.BasariliMi)
            {
                return kayit;
            }

            var cariSonuc = await cariYazici.YazAsync(islemId, new BelgeCariHareketi(
                istek.CariHesapId,
                tahsilatMi ? CariHareketTuru.Tahsilat : CariHareketTuru.Odeme,
                tahsilatMi ? HareketYonu.Alacak : HareketYonu.Borc,
                istek.Tarih, null, cari.Deger.Tutar, cari.Deger.Kur, istek.BelgeNo, $"{hesap.Ad}{Ek(istek.Aciklama)}",
                OncelikliKapamaBelgeId: kaynakBelgeId), ct);
            return cariSonuc.BasariliMi ? islemId : cariSonuc.Hata;
        }, cancellationToken);

    // Kaynak hesaptan çıkış, hedefe giriş. Dövizler farklıysa hedef tutar girilen tutardan ya da kurlardan hesaplanır.
    public Task<Sonuc<Guid>> VirmanAsync(VirmanIstegi istek, CancellationToken cancellationToken) =>
        islemKapsami.CalistirAsync<Guid>(async ct =>
        {
            if (istek.KaynakHesapId == istek.HedefHesapId)
            {
                return KasaBankaHatalari.AyniHesap;
            }

            if (await HesapAsync(istek.KaynakHesapId, ct) is not { } kaynak || await HesapAsync(istek.HedefHesapId, ct) is not { } hedef)
            {
                return KasaBankaHatalari.HesapBulunamadi;
            }

            var kaynakKur = await KurAsync(kaynak.ParaBirimi, istek.Tarih, istek.Kur, ct);
            if (!kaynakKur.BasariliMi)
            {
                return kaynakKur.Hata;
            }

            var hedefKur = kaynak.ParaBirimi == hedef.ParaBirimi
                ? kaynakKur
                : await KurAsync(hedef.ParaBirimi, istek.Tarih, istek.HedefKur, ct);
            if (!hedefKur.BasariliMi)
            {
                return hedefKur.Hata;
            }

            var hedefTutar = kaynak.ParaBirimi == hedef.ParaBirimi
                ? istek.Tutar
                : istek.HedefTutar ?? Para.Yuvarla(istek.Tutar * kaynakKur.Deger / hedefKur.Deger);
            var aciklama = istek.Aciklama ?? $"{kaynak.Ad} → {hedef.Ad}";
            var islemId = Guid.CreateVersion7();
            return await KaydetAsync(islemId, ct,
                KasaHareketi.Olustur(kaynak, islemId, KasaHareketTuru.VirmanCikis, false, istek.Tarih, istek.Tutar, kaynakKur.Deger, null, aciklama),
                KasaHareketi.Olustur(hedef, islemId, KasaHareketTuru.VirmanGiris, true, istek.Tarih, hedefTutar, hedefKur.Deger, null, aciklama));
        }, cancellationToken);

    public Task<Sonuc<Guid>> GelirGiderAsync(GelirGiderIstegi istek, CancellationToken cancellationToken) =>
        islemKapsami.CalistirAsync<Guid>(async ct =>
        {
            var kategori = await db.Kategoriler.SingleOrDefaultAsync(k => k.Id == istek.KategoriId, ct);
            if (kategori is null)
            {
                return KasaBankaHatalari.KategoriBulunamadi;
            }

            if (kategori.Tur != istek.Tur)
            {
                return KasaBankaHatalari.KategoriTuruUyusmuyor;
            }

            var odendi = istek.Bicim == GelirGiderBicimi.Odendi;
            if ((odendi && istek.HesapId is null) || (!odendi && (istek.CariHesapId is null || istek.HesapId is not null)))
            {
                return KasaBankaHatalari.BicimGecersiz;
            }

            var gider = istek.Tur == GelirGiderTuru.Gider;
            var islemId = Guid.CreateVersion7();
            var aciklama = istek.Aciklama ?? kategori.Ad;
            return odendi
                ? await OdenenGelirGiderAsync(istek, kategori, gider, islemId, aciklama, ct)
                : await TahakkukAsync(istek, kategori, gider, islemId, aciklama, ct);
        }, cancellationToken);

    // Faturadan gelen işlemler belgesinden iptal edilir (belgedenMi = true ile).
    public Task<Sonuc<Guid>> IptalAsync(Guid islemId, bool belgedenMi, CancellationToken cancellationToken) =>
        islemKapsami.CalistirAsync<Guid>(async ct =>
        {
            var hareketler = await db.KasaHareketleri
                .Where(h => h.IslemId == islemId && h.IptalHareketId == null && h.IptalEdilenHareketId == null)
                .ToListAsync(ct);
            var gelirGider = await db.GelirGiderKayitlari.SingleOrDefaultAsync(g => g.IslemId == islemId && g.IptalTarihi == null, ct);
            if (hareketler.Count == 0 && gelirGider is null)
            {
                return await db.KasaHareketleri.AnyAsync(h => h.IslemId == islemId, ct)
                       || await db.GelirGiderKayitlari.AnyAsync(g => g.IslemId == islemId, ct)
                    ? KasaBankaHatalari.ZatenIptalEdilmis
                    : KasaBankaHatalari.IslemBulunamadi;
            }

            if (!belgedenMi && hareketler.Any(h => h.KaynakBelgeId is not null))
            {
                return KasaBankaHatalari.KaynakBelgedenIptal;
            }

            foreach (var hareket in hareketler)
            {
                var ters = hareket.IptalEt();
                if (!ters.BasariliMi)
                {
                    return ters.Hata;
                }

                db.KasaHareketleri.Add(ters.Deger);
            }

            gelirGider?.IptalEt(zaman.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync(ct);

            var cari = await cariYazici.IptalEtAsync(islemId, ct);
            return cari.BasariliMi ? islemId : cari.Hata;
        }, cancellationToken);

    private async Task<Sonuc<Guid>> OdenenGelirGiderAsync(
        GelirGiderIstegi istek, GelirGiderKategorisi kategori, bool gider, Guid islemId, string aciklama, CancellationToken ct)
    {
        if (await HesapAsync(istek.HesapId!.Value, ct) is not { } hesap)
        {
            return KasaBankaHatalari.HesapBulunamadi;
        }

        var kur = await KurAsync(hesap.ParaBirimi, istek.Tarih, istek.Kur, ct);
        if (!kur.BasariliMi)
        {
            return kur.Hata;
        }

        var hareket = KasaHareketi.Olustur(hesap, islemId, gider ? KasaHareketTuru.Gider : KasaHareketTuru.Gelir, !gider, istek.Tarih,
            istek.Tutar, kur.Deger, istek.BelgeNo, aciklama, istek.CariHesapId, kategori.Id);
        db.GelirGiderKayitlari.Add(GelirGiderKaydi.Olustur(islemId, istek.Tur, GelirGiderBicimi.Odendi, kategori.Id, istek.Tarih,
            hesap.ParaBirimi, istek.Tutar, kur.Deger, hesap.Id, istek.CariHesapId, istek.BelgeNo, aciklama));
        var kayit = await KaydetAsync(islemId, ct, hareket);
        if (!kayit.BasariliMi || istek.CariHesapId is not { } cariId)
        {
            return kayit;
        }

        // Cari yalnızca kime ödendiğini gösterir: gider tahakkuku ve ödemesi birlikte yazılır, ödeme önce bu gideri kapatır.
        var cari = await CariTutariAsync(cariId, hesap, istek.Tutar, kur.Deger, istek.CariKuru, istek.Tarih, ct);
        if (!cari.BasariliMi)
        {
            return cari.Hata;
        }

        var tahakkuk = await cariYazici.YazAsync(islemId, new BelgeCariHareketi(
            cariId, gider ? CariHareketTuru.Gider : CariHareketTuru.Gelir, gider ? HareketYonu.Alacak : HareketYonu.Borc,
            istek.Tarih, null, cari.Deger.Tutar, cari.Deger.Kur, istek.BelgeNo, aciklama, OtomatikKapama: false), ct);
        if (!tahakkuk.BasariliMi)
        {
            return tahakkuk.Hata;
        }

        var odeme = await cariYazici.YazAsync(islemId, new BelgeCariHareketi(
            cariId, gider ? CariHareketTuru.Odeme : CariHareketTuru.Tahsilat, gider ? HareketYonu.Borc : HareketYonu.Alacak,
            istek.Tarih, null, cari.Deger.Tutar, cari.Deger.Kur, istek.BelgeNo, $"{hesap.Ad} - {aciklama}", OncelikliKapamaBelgeId: islemId), ct);
        return odeme.BasariliMi ? islemId : odeme.Hata;
    }

    // Kasa etkilenmez: gider cariye borç (cari alacaklanır), gelir cariden alacak (cari borçlanır) olarak yazılır.
    private async Task<Sonuc<Guid>> TahakkukAsync(
        GelirGiderIstegi istek, GelirGiderKategorisi kategori, bool gider, Guid islemId, string aciklama, CancellationToken ct)
    {
        var cariSonuc = await CariAsync(istek.CariHesapId!.Value, ct);
        if (!cariSonuc.BasariliMi)
        {
            return cariSonuc.Hata;
        }

        var cari = cariSonuc.Deger;
        var kur = await KurAsync(cari.ParaBirimi, istek.Tarih, istek.Kur, ct);
        if (!kur.BasariliMi)
        {
            return kur.Hata;
        }

        db.GelirGiderKayitlari.Add(GelirGiderKaydi.Olustur(islemId, istek.Tur, GelirGiderBicimi.Tahakkuk, kategori.Id, istek.Tarih,
            cari.ParaBirimi, istek.Tutar, kur.Deger, null, cari.Id, istek.BelgeNo, aciklama));
        await db.SaveChangesAsync(ct);

        var yazim = await cariYazici.YazAsync(islemId, new BelgeCariHareketi(
            cari.Id, gider ? CariHareketTuru.Gider : CariHareketTuru.Gelir, gider ? HareketYonu.Alacak : HareketYonu.Borc,
            istek.Tarih, istek.VadeTarihi, istek.Tutar, kur.Deger, istek.BelgeNo, aciklama), ct);
        return yazim.BasariliMi ? islemId : yazim.Hata;
    }

    private sealed record CariTutari(string Unvan, decimal Tutar, decimal Kur);

    // Tutar hesabın dövizindedir. Cari aynı dövizdeyse aynen, farklıysa (cari izin veriyorsa) TL karşılığı üzerinden cari dövizine çevrilir.
    private async Task<Sonuc<CariTutari>> CariTutariAsync(
        Guid cariId, KasaHesabi hesap, decimal tutar, decimal hesapKuru, decimal? cariKuru, DateOnly tarih, CancellationToken ct)
    {
        var cariSonuc = await CariAsync(cariId, ct);
        if (!cariSonuc.BasariliMi)
        {
            return cariSonuc.Hata;
        }

        var cari = cariSonuc.Deger;
        if (cari.ParaBirimi == hesap.ParaBirimi)
        {
            return new CariTutari(cari.Unvan, tutar, hesapKuru);
        }

        if (!cari.FarkliDovizdeIslem)
        {
            return KasaBankaHatalari.FarkliDoviz;
        }

        var kur = await KurAsync(cari.ParaBirimi, tarih, cariKuru, ct);
        return kur.BasariliMi ? new CariTutari(cari.Unvan, Para.Yuvarla(tutar * hesapKuru / kur.Deger), kur.Deger) : kur.Hata;
    }

    private async Task<Sonuc<CariBilgisi>> CariAsync(Guid cariId, CancellationToken ct)
    {
        var lisans = await lisansSorgusu.GetirAsync(mevcutKullanici.FirmaId!.Value, ct);
        if (lisans?.Moduller.Contains(ModulKodlari.Cari) != true)
        {
            return KasaBankaHatalari.CariLisansiYok;
        }

        var cari = await cariSorgusu.GetirAsync(cariId, ct);
        if (cari is null)
        {
            return KasaBankaHatalari.CariBulunamadi;
        }

        return cari.AktifMi ? cari : KasaBankaHatalari.PasifCari;
    }

    private Task<KasaHesabi?> HesapAsync(Guid id, CancellationToken ct) => db.Hesaplar.SingleOrDefaultAsync(h => h.Id == id, ct);

    // TL için 1; dövizde girilen kur ya da işlem tarihinin TCMB döviz alış kuru.
    private async Task<Sonuc<decimal>> KurAsync(string paraBirimi, DateOnly tarih, decimal? girilen, CancellationToken ct)
    {
        if (paraBirimi == ParaBirimleri.Tl)
        {
            return 1m;
        }

        if (girilen is > 0)
        {
            return girilen.Value;
        }

        return (await kurSorgusu.IslemKuruAsync(paraBirimi, tarih, ct))?.DovizAlis is { } kur ? kur : KasaBankaHatalari.KurGerekli;
    }

    private async Task<Sonuc<Guid>> KaydetAsync(Guid islemId, CancellationToken ct, params Sonuc<KasaHareketi>[] hareketler)
    {
        foreach (var hareket in hareketler)
        {
            if (!hareket.BasariliMi)
            {
                return hareket.Hata;
            }

            db.KasaHareketleri.Add(hareket.Deger);
        }

        await db.SaveChangesAsync(ct);
        return islemId;
    }

    private static string Ek(string? aciklama) => string.IsNullOrWhiteSpace(aciklama) ? "" : $" - {aciklama}";
}
