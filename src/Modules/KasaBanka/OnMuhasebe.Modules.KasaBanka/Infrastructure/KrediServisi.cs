using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.KasaBanka.Domain;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.KasaBanka.Infrastructure;

// Kredinin kasa etkileri. Kasa hareketlerinin kaynak belgesi kredidir: kasa ekranından tek başına iptal edilemez, buradan geri alınır.
// Taksit tek kasa çıkışıdır (banka ekstresindeki tek borç kaydıyla eşleşir); faiz ve vergiler aynı işleme bağlı gider kaydıdır.
internal sealed class KrediServisi(KasaBankaDbContext db, CariDovizServisi doviz, KasaIslemServisi islemServisi, IIslemKapsami islemKapsami)
{
    public const string GiderKategorisi = "Kredi faiz ve masrafları";

    public Task<Sonuc<Kredi>> OlusturAsync(KrediKaydetIstegi istek, CancellationToken cancellationToken) =>
        islemKapsami.CalistirAsync<Kredi>(async ct =>
        {
            if (await db.Hesaplar.SingleOrDefaultAsync(h => h.Id == istek.HesapId, ct) is not { } hesap)
            {
                return KasaBankaHatalari.HesapBulunamadi;
            }

            var kur = await doviz.KurAsync(hesap.ParaBirimi, istek.KullandirimTarihi, null, ct);
            if (!kur.BasariliMi)
            {
                return kur.Hata;
            }

            var islemId = Guid.CreateVersion7();
            var olusturma = Kredi.Olustur(await KodAsync(ct), istek, hesap, islemId);
            if (!olusturma.BasariliMi)
            {
                return olusturma.Hata;
            }

            var kredi = olusturma.Deger;
            db.Krediler.Add(kredi);

            var giris = KasaHareketi.Olustur(hesap, islemId, KasaHareketTuru.KrediKullanim, true, istek.KullandirimTarihi, istek.Tutar, kur.Deger,
                kredi.Kod, $"Kredi kullanımı: {kredi.Ad}", kaynakBelgeId: kredi.Id);
            if (!giris.BasariliMi)
            {
                return giris.Hata;
            }

            db.KasaHareketleri.Add(giris.Deger);
            if (istek.KullandirimMasrafi > 0)
            {
                var kategori = await KategoriAsync(ct);
                var masraf = KasaHareketi.Olustur(hesap, islemId, KasaHareketTuru.Gider, false, istek.KullandirimTarihi, istek.KullandirimMasrafi,
                    kur.Deger, kredi.Kod, $"Kredi kullandırım masrafı: {kredi.Ad}", kategoriId: kategori.Id, kaynakBelgeId: kredi.Id);
                if (!masraf.BasariliMi)
                {
                    return masraf.Hata;
                }

                db.KasaHareketleri.Add(masraf.Deger);
                db.GelirGiderKayitlari.Add(GelirGiderKaydi.Olustur(islemId, GelirGiderTuru.Gider, GelirGiderBicimi.Odendi, kategori.Id,
                    istek.KullandirimTarihi, hesap.ParaBirimi, istek.KullandirimMasrafi, kur.Deger, hesap.Id, null, kredi.Kod,
                    $"Kredi kullandırım masrafı: {kredi.Ad}"));
            }

            await db.SaveChangesAsync(ct);
            return kredi;
        }, cancellationToken);

    public Task<Sonuc<Kredi>> TaksitOdeAsync(Guid krediId, int sira, TaksitOdemeIstegi istek, CancellationToken cancellationToken) =>
        islemKapsami.CalistirAsync<Kredi>(async ct =>
        {
            if (await KrediAsync(krediId, ct) is not { } kredi)
            {
                return KasaBankaHatalari.KrediBulunamadi;
            }

            if (await db.Hesaplar.SingleOrDefaultAsync(h => h.Id == (istek.HesapId ?? kredi.HesapId), ct) is not { } hesap)
            {
                return KasaBankaHatalari.HesapBulunamadi;
            }

            if (hesap.ParaBirimi != kredi.ParaBirimi)
            {
                return KasaBankaHatalari.TaksitHesapDovizi;
            }

            var islemId = Guid.CreateVersion7();
            var odeme = kredi.TaksitOde(sira, islemId, istek.Tarih);
            if (!odeme.BasariliMi)
            {
                return odeme.Hata;
            }

            var taksit = odeme.Deger;
            var kur = await doviz.KurAsync(hesap.ParaBirimi, istek.Tarih, null, ct);
            if (!kur.BasariliMi)
            {
                return kur.Hata;
            }

            var aciklama = $"Kredi taksiti {taksit.Sira}/{kredi.Taksitler.Count}: {kredi.Ad}";
            var cikis = KasaHareketi.Olustur(hesap, islemId, KasaHareketTuru.KrediOdeme, false, istek.Tarih, taksit.Toplam, kur.Deger, kredi.Kod,
                aciklama, kaynakBelgeId: kredi.Id);
            if (!cikis.BasariliMi)
            {
                return cikis.Hata;
            }

            db.KasaHareketleri.Add(cikis.Deger);
            if (taksit.Faiz + taksit.Vergi > 0)
            {
                var kategori = await KategoriAsync(ct);
                db.GelirGiderKayitlari.Add(GelirGiderKaydi.Olustur(islemId, GelirGiderTuru.Gider, GelirGiderBicimi.Odendi, kategori.Id, istek.Tarih,
                    hesap.ParaBirimi, taksit.Faiz + taksit.Vergi, kur.Deger, hesap.Id, null, kredi.Kod, $"{aciklama} (faiz ve vergiler)"));
            }

            await db.SaveChangesAsync(ct);
            return kredi;
        }, cancellationToken);

    public Task<Sonuc<Kredi>> TaksitGeriAlAsync(Guid krediId, int sira, CancellationToken cancellationToken) =>
        islemKapsami.CalistirAsync<Kredi>(async ct =>
        {
            if (await KrediAsync(krediId, ct) is not { } kredi)
            {
                return KasaBankaHatalari.KrediBulunamadi;
            }

            var geriAlma = kredi.TaksitOdemesiniGeriAl(sira);
            if (!geriAlma.BasariliMi)
            {
                return geriAlma.Hata;
            }

            var iptal = await islemServisi.IptalAsync(geriAlma.Deger, belgedenMi: true, ct);
            if (!iptal.BasariliMi)
            {
                return iptal.Hata;
            }

            await db.SaveChangesAsync(ct);
            return kredi;
        }, cancellationToken);

    public Task<Sonuc<Kredi>> IptalAsync(Guid krediId, CancellationToken cancellationToken) =>
        islemKapsami.CalistirAsync<Kredi>(async ct =>
        {
            if (await KrediAsync(krediId, ct) is not { } kredi)
            {
                return KasaBankaHatalari.KrediBulunamadi;
            }

            var sonuc = kredi.IptalEt();
            if (!sonuc.BasariliMi)
            {
                return sonuc.Hata;
            }

            var iptal = await islemServisi.IptalAsync(kredi.KullandirimIslemId, belgedenMi: true, ct);
            if (!iptal.BasariliMi)
            {
                return iptal.Hata;
            }

            await db.SaveChangesAsync(ct);
            return kredi;
        }, cancellationToken);

    private Task<Kredi?> KrediAsync(Guid id, CancellationToken ct) => db.Krediler.Include(k => k.Taksitler).SingleOrDefaultAsync(k => k.Id == id, ct);

    // Faiz ve masraflar gelir/gider raporunda kendi kategorisinde görünür; kategori yoksa açılır.
    private async Task<GelirGiderKategorisi> KategoriAsync(CancellationToken ct)
    {
        if (await db.Kategoriler.FirstOrDefaultAsync(k => k.Ad == GiderKategorisi && k.Tur == GelirGiderTuru.Gider, ct) is { } mevcut)
        {
            return mevcut;
        }

        var kategori = GelirGiderKategorisi.Olustur(GiderKategorisi, GelirGiderTuru.Gider);
        db.Kategoriler.Add(kategori);
        return kategori;
    }

    private async Task<string> KodAsync(CancellationToken ct)
    {
        var sayi = await db.Krediler.IgnoreQueryFilters([ModulDbContext.SilinmeFiltresi]).CountAsync(ct);
        return $"KR{sayi + 1:D5}";
    }
}
