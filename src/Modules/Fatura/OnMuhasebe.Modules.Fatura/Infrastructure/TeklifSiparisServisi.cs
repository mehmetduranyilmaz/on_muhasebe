using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Doviz.Contracts;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Modules.Fatura.Domain;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Fatura.Infrastructure;

// Teklif ve sipariş kaydı. Cari ve satır çözümlemesi faturayla ortaktır; stok ve cari hareketi yazılmaz.
internal sealed class TeklifSiparisServisi(
    FaturaDbContext db,
    FaturaHazirlayici hazirlayici,
    IKurSorgusu kurSorgusu,
    FaturaNumaraUretici numaraUretici,
    IIslemKapsami islemKapsami,
    IsyeriSecici isyeriSecici,
    TimeProvider zaman)
{
    // İç belgelerdir (GİB numaralama kuralına tabi değil); seri + yıl + sıra biçimi faturayla aynıdır.
    public static string Seri(TeklifSiparisTuru tur) => tur switch
    {
        TeklifSiparisTuru.SatisTeklifi => "TKL",
        TeklifSiparisTuru.SatisSiparisi => "SSP",
        _ => "SAS"
    };

    public async Task<Sonuc<TeklifSiparis>> OlusturAsync(TeklifSiparisKaydetIstegi istek, CancellationToken cancellationToken)
    {
        var bilgiler = await HazirlaAsync(istek, cancellationToken);
        if (!bilgiler.BasariliMi)
        {
            return bilgiler.Hata;
        }

        var olusturma = TeklifSiparis.Olustur(bilgiler.Deger);
        if (!olusturma.BasariliMi)
        {
            return olusturma.Hata;
        }

        var isyeri = await isyeriSecici.BelirleAsync(istek.IsyeriId, cancellationToken);
        if (!isyeri.BasariliMi)
        {
            return isyeri.Hata;
        }

        olusturma.Deger.IsyeriAta(isyeri.Deger);

        db.TeklifSiparisler.Add(olusturma.Deger);
        await db.SaveChangesAsync(cancellationToken);
        return olusturma.Deger;
    }

    public async Task<Sonuc<TeklifSiparis>> GuncelleAsync(Guid id, TeklifSiparisKaydetIstegi istek, CancellationToken cancellationToken)
    {
        if (await BelgeAsync(id, cancellationToken) is not { } belge)
        {
            return TeklifSiparisHatalari.Bulunamadi;
        }

        db.Entry(belge).Property(b => b.Surum).OriginalValue = istek.Surum;
        var bilgiler = await HazirlaAsync(istek, cancellationToken);
        if (!bilgiler.BasariliMi)
        {
            return bilgiler.Hata;
        }

        var isyeri = await isyeriSecici.DegisiklikAsync(belge.IsyeriId, istek.IsyeriId, cancellationToken);
        if (!isyeri.BasariliMi)
        {
            return isyeri.Hata;
        }

        var sonuc = belge.Guncelle(bilgiler.Deger);
        if (!sonuc.BasariliMi)
        {
            return sonuc.Hata;
        }

        belge.IsyeriAta(isyeri.Deger);

        await db.SaveChangesAsync(cancellationToken);
        return belge;
    }

    public async Task<Sonuc<TeklifSiparis>> OnaylaAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await BelgeAsync(id, cancellationToken) is not { } belge)
        {
            return TeklifSiparisHatalari.Bulunamadi;
        }

        return await islemKapsami.CalistirAsync<TeklifSiparis>(async ct =>
        {
            var numara = await numaraUretici.SonrakiAsync(Seri(belge.Tur), belge.Tarih.Year, ct);
            var sonuc = belge.Onayla(numara, zaman.GetUtcNow().UtcDateTime);
            if (!sonuc.BasariliMi)
            {
                return sonuc.Hata;
            }

            await db.SaveChangesAsync(ct);
            return belge;
        }, cancellationToken);
    }

    // Teklifin cari, koşul ve satırlarıyla taslak satış siparişi açılır; teklif kabul edilmiş sayılır.
    // Aday müşteriye verilmiş teklif önce bir cariye bağlanmalıdır (sipariş cari ister).
    public async Task<Sonuc<TeklifSiparis>> SipariseDonusturAsync(Guid teklifId, Guid? cariHesapId, CancellationToken cancellationToken)
    {
        if (await BelgeAsync(teklifId, cancellationToken) is not { } teklif)
        {
            return TeklifSiparisHatalari.Bulunamadi;
        }

        var donusum = teklif.SipariseDonustu();
        if (!donusum.BasariliMi)
        {
            return donusum.Hata;
        }

        var istek = new TeklifSiparisKaydetIstegi(TeklifSiparisTuru.SatisSiparisi, teklif.CariHesapId ?? cariHesapId, null, TurkiyeBugun(), null, null,
            teklif.ParaBirimi, teklif.Kur, teklif.GenelIskontoTuru, teklif.GenelIskonto, teklif.OdemeKosulu, teklif.TeslimKosulu,
            $"{teklif.Numara} numaralı tekliften",
            teklif.Satirlar.OrderBy(s => s.Sira).Select(s => new FaturaSatiriIstegi(s.StokKartiId, s.Ad, s.DepoId, s.Birim, s.Miktar, s.BirimFiyat,
                s.KdvDahilMi, s.KdvOrani, s.Iskonto1, s.Iskonto2, s.Iskonto3)).ToList());
        var bilgiler = await HazirlaAsync(istek, cancellationToken);
        if (!bilgiler.BasariliMi)
        {
            return bilgiler.Hata;
        }

        var siparis = TeklifSiparis.Olustur(bilgiler.Deger, teklif.Id);
        if (!siparis.BasariliMi)
        {
            return siparis.Hata;
        }

        // Teklifin işyerinde sipariş açılır.
        siparis.Deger.IsyeriAta(teklif.IsyeriId);

        db.TeklifSiparisler.Add(siparis.Deger);
        await db.SaveChangesAsync(cancellationToken);
        return siparis.Deger;
    }

    public async Task<Sonuc<TeklifSiparis>> DurumAsync(Guid id, Func<TeklifSiparis, Sonuc> islem, CancellationToken cancellationToken)
    {
        if (await BelgeAsync(id, cancellationToken) is not { } belge)
        {
            return TeklifSiparisHatalari.Bulunamadi;
        }

        var sonuc = islem(belge);
        if (!sonuc.BasariliMi)
        {
            return sonuc.Hata;
        }

        await db.SaveChangesAsync(cancellationToken);
        return belge;
    }

    private Task<TeklifSiparis?> BelgeAsync(Guid id, CancellationToken ct) =>
        db.TeklifSiparisler.Include(b => b.Satirlar).SingleOrDefaultAsync(b => b.Id == id, ct);

    private DateOnly TurkiyeBugun() => OnMuhasebe.Shared.Domain.TurkiyeSaati.Bugun(zaman);

    private async Task<Sonuc<TeklifSiparisBilgileri>> HazirlaAsync(TeklifSiparisKaydetIstegi istek, CancellationToken cancellationToken)
    {
        var moduller = await hazirlayici.ModullerAsync(cancellationToken);
        FaturaCarisi cari;
        string paraBirimi;
        if (istek.CariHesapId is { } cariId)
        {
            var c = await hazirlayici.KayitliCariAsync(cariId, moduller, cancellationToken);
            if (!c.BasariliMi)
            {
                return c.Hata;
            }

            (cari, paraBirimi) = c.Deger;
        }
        else
        {
            if (!TeklifSiparisTurleri.TeklifMi(istek.Tur))
            {
                return TeklifSiparisHatalari.CariGerekli;
            }

            cari = new FaturaCarisi(null, null, istek.AdayUnvan?.Trim() ?? "", null, null, null, null, null);
            paraBirimi = string.IsNullOrWhiteSpace(istek.ParaBirimi) ? ParaBirimleri.Tl : istek.ParaBirimi;
        }

        var kur = 1m;
        if (paraBirimi != ParaBirimleri.Tl)
        {
            if ((istek.Kur ?? (await kurSorgusu.IslemKuruAsync(paraBirimi, istek.Tarih, cancellationToken))?.DovizAlis) is not { } bulunan)
            {
                return FaturaHatalari.KurGerekli;
            }

            kur = bulunan;
        }

        var satirlar = await hazirlayici.SatirlarAsync(istek.Satirlar, moduller, cancellationToken);
        if (!satirlar.BasariliMi)
        {
            return satirlar.Hata;
        }

        static string? Temiz(string? d) => string.IsNullOrWhiteSpace(d) ? null : d.Trim();
        return new TeklifSiparisBilgileri(istek.Tur, cari, istek.Tarih, istek.GecerlilikTarihi, istek.TerminTarihi, paraBirimi, kur,
            istek.GenelIskontoTuru, istek.GenelIskonto, Temiz(istek.OdemeKosulu), Temiz(istek.TeslimKosulu), Temiz(istek.Aciklama), satirlar.Deger);
    }
}
