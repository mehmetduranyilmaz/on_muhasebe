using Microsoft.EntityFrameworkCore;
using Npgsql;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.Doviz.Contracts;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Modules.Fatura.Domain;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Contracts.Vergi;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Lisans;

namespace OnMuhasebe.Modules.Fatura.Infrastructure;

internal sealed class FaturaAyarServisi(FaturaDbContext db)
{
    public async Task<FaturaAyari> GetirAsync(CancellationToken cancellationToken) =>
        await db.FaturaAyarlari.SingleOrDefaultAsync(cancellationToken) ?? FaturaAyari.Varsayilan();
}

// Numara: seri (3) + yıl (4) + sıra (9) = GİB biçimi, ör. TPT2026000000001. Açık bir IIslemKapsami içinde çağrılmalıdır;
// sayaç satırı transaction bitene kadar kilitli kalır.
internal sealed class FaturaNumaraUretici(FaturaDbContext db, IMevcutKullanici mevcutKullanici)
{
    private const string Sql = """
        INSERT INTO fatura.fatura_seri_sayaci (firma_id, seri, yil, son_numara) VALUES (@firma, @seri, @yil, 1)
        ON CONFLICT (firma_id, seri, yil) DO UPDATE SET son_numara = fatura.fatura_seri_sayaci.son_numara + 1
        RETURNING son_numara AS "Value"
        """;

    public async Task<string> SonrakiAsync(string seri, int yil, CancellationToken cancellationToken)
    {
        // Sorgu birleştirilmeden (ToListAsync) çalıştırılır: INSERT ... RETURNING alt sorgu içine alınamaz.
        var sonuc = await db.Database
            .SqlQueryRaw<long>(Sql,
                new NpgsqlParameter("firma", mevcutKullanici.FirmaId!.Value),
                new NpgsqlParameter("seri", seri),
                new NpgsqlParameter("yil", yil))
            .ToListAsync(cancellationToken);

        return $"{seri}{yil}{sonuc.Single():D9}";
    }
}

// İsteği cari, stok, depo ve kur bilgileriyle çözümler. Diğer modüllere yalnızca sözleşmeleri üzerinden erişir.
internal sealed class FaturaHazirlayici(
    ICariSorgusu cariSorgusu,
    IStokSorgusu stokSorgusu,
    IKurSorgusu kurSorgusu,
    IKasaBankaSorgusu kasaSorgusu,
    ILisansSorgusu lisansSorgusu,
    IMevcutKullanici mevcutKullanici)
{
    public async Task<Sonuc<FaturaBilgileri>> HazirlaAsync(FaturaKaydetIstegi istek, CancellationToken cancellationToken)
    {
        var lisans = await lisansSorgusu.GetirAsync(mevcutKullanici.FirmaId!.Value, cancellationToken);
        var moduller = lisans?.Moduller ?? [];
        var satisSekli = FaturaTurleri.SatisTarafiMi(istek.Tur) ? istek.SatisSekli ?? SatisSekli.Toptan : (SatisSekli?)null;

        var cari = await CariAsync(istek, satisSekli, moduller, cancellationToken);
        if (!cari.BasariliMi)
        {
            return cari.Hata;
        }

        var (faturaCarisi, paraBirimi) = cari.Deger;
        var kur = 1m;
        if (paraBirimi != ParaBirimleri.Tl)
        {
            if ((istek.Kur ?? (await kurSorgusu.IslemKuruAsync(paraBirimi, istek.Tarih, cancellationToken))?.DovizAlis) is not { } bulunan)
            {
                return FaturaHatalari.KurGerekli;
            }

            kur = bulunan;
        }

        var satirlar = await SatirlarAsync(istek.Satirlar, moduller, cancellationToken);
        if (!satirlar.BasariliMi)
        {
            return satirlar.Hata;
        }

        if (istek.PesinHesapId is { } pesinHesapId
            && await PesinHesapHatasiAsync(pesinHesapId, istek.Tur, paraBirimi, moduller, cancellationToken) is { } pesinHatasi)
        {
            return pesinHatasi;
        }

        return new FaturaBilgileri(
            istek.Tur, satisSekli, faturaCarisi, istek.Tarih, istek.VadeTarihi,
            FaturaTurleri.GidenMi(istek.Tur) ? null : Temiz(istek.Numara)?.ToUpperInvariant(),
            paraBirimi, kur, istek.GenelIskontoTuru, istek.GenelIskonto, Temiz(istek.Aciklama), satirlar.Deger, istek.PesinHesapId,
            istek.StopajOrani);
    }

    // Satış tarafında para hesaba girer (kredi kartına tahsilat olmaz), alış tarafında çıkar (POS'tan ödeme olmaz).
    private async Task<Hata?> PesinHesapHatasiAsync(
        Guid hesapId, FaturaTuru tur, string paraBirimi, IReadOnlyList<string> moduller, CancellationToken cancellationToken)
    {
        if (!moduller.Contains(ModulKodlari.KasaBanka))
        {
            return FaturaHatalari.KasaLisansiYok;
        }

        var hesap = await kasaSorgusu.HesapGetirAsync(hesapId, cancellationToken);
        if (hesap is not { AktifMi: true })
        {
            return FaturaHatalari.PesinHesapBulunamadi;
        }

        if (hesap.ParaBirimi != paraBirimi)
        {
            return FaturaHatalari.PesinHesapDovizi;
        }

        var girisMi = FaturaTurleri.ParaGirisiMi(tur);
        return (girisMi && hesap.Tur == HesapTuru.KrediKarti) || (!girisMi && hesap.Tur == HesapTuru.Pos) ? FaturaHatalari.PesinHesapTuru : null;
    }

    private async Task<Sonuc<(FaturaCarisi Cari, string ParaBirimi)>> CariAsync(
        FaturaKaydetIstegi istek, SatisSekli? satisSekli, IReadOnlyList<string> moduller, CancellationToken cancellationToken)
    {
        if (istek.CariHesapId is { } cariId)
        {
            if (!moduller.Contains(ModulKodlari.Cari))
            {
                return FaturaHatalari.CariLisansiYok;
            }

            var c = await cariSorgusu.GetirAsync(cariId, cancellationToken);
            if (c is null)
            {
                return FaturaHatalari.CariBulunamadi;
            }

            if (!c.AktifMi)
            {
                return FaturaHatalari.PasifCari;
            }

            return (new FaturaCarisi(c.Id, c.Kod, c.Unvan, c.VergiNo, c.VergiDairesi, c.Il, c.Ilce, c.Adres), c.ParaBirimi);
        }

        // Perakende satış nihai tüketiciye; gider pusulası/SMM cari seçilmezse serbest kişiye (ad+TCKN) kesilebilir.
        var serbestKisi = FaturaTurleri.StopajliMi(istek.Tur);
        if (satisSekli != SatisSekli.Perakende && !serbestKisi)
        {
            return FaturaHatalari.CariVeyaNihaiTuketici;
        }

        var nihai = istek.NihaiTuketici;
        var unvan = Temiz(nihai?.AdSoyad) ?? (serbestKisi ? FaturaMetinleri.TurAdi(istek.Tur) : FaturaBelgesi.NihaiTuketiciUnvani);
        var tckn = Temiz(nihai?.Tckn) ?? (serbestKisi ? null : FaturaBelgesi.NihaiTuketiciTckn);
        return (new FaturaCarisi(null, null, unvan, tckn, null, null, null, null), ParaBirimleri.Tl);
    }

    private async Task<Sonuc<IReadOnlyList<FaturaSatiriBilgisi>>> SatirlarAsync(
        IReadOnlyList<FaturaSatiriIstegi> istekler, IReadOnlyList<string> moduller, CancellationToken cancellationToken)
    {
        var kartIdleri = istekler.Where(s => s.StokKartiId is not null).Select(s => s.StokKartiId!.Value).Distinct().ToList();
        Dictionary<Guid, StokKartiBilgisi> kartlar = [];
        if (kartIdleri.Count > 0)
        {
            if (!moduller.Contains(ModulKodlari.Stok))
            {
                return FaturaHatalari.StokLisansiYok;
            }

            kartlar = (await stokSorgusu.KartlariGetirAsync(kartIdleri, cancellationToken)).ToDictionary(k => k.Id);
            if (kartlar.Count != kartIdleri.Count)
            {
                return FaturaHatalari.StokKartiBulunamadi;
            }
        }

        var depolar = new Dictionary<Guid, Guid?>();
        Guid? varsayilanDepo = null;
        var sonuc = new List<FaturaSatiriBilgisi>(istekler.Count);
        foreach (var s in istekler)
        {
            if (s.StokKartiId is not { } kartId)
            {
                if (Temiz(s.Ad) is not { } serbestAd)
                {
                    return FaturaHatalari.SatirAdiGerekli;
                }

                sonuc.Add(Satir(s, null, null, serbestAd, false, null));
                continue;
            }

            var kart = kartlar[kartId];
            if (!kart.AktifMi)
            {
                return FaturaHatalari.PasifStokKarti;
            }

            if (!kart.BirimTanimliMi(s.Birim))
            {
                return FaturaHatalari.BirimTanimsiz;
            }

            var stokluMu = kart.Tur == StokTuru.Urun;
            Guid? depoId = null;
            if (stokluMu)
            {
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
            }

            sonuc.Add(Satir(s, kart.Id, kart.Kod, Temiz(s.Ad) ?? kart.Ad, stokluMu, depoId));
        }

        return sonuc;
    }

    private static FaturaSatiriBilgisi Satir(FaturaSatiriIstegi s, Guid? kartId, string? kod, string ad, bool stokluMu, Guid? depoId)
    {
        // Oran açıkça gelmişse ona uyulur; yalnızca kod gelmişse referanstaki varsayılan oran kullanılır.
        var (pay, payda) = s.TevkifatPayda > 0
            ? (s.TevkifatPay, s.TevkifatPayda)
            : TevkifatKodlari.Bul(Temiz(s.TevkifatKodu)) is { } t ? (t.Pay, t.Payda) : (0, 0);

        return new(kartId, kod, ad, stokluMu, depoId, s.Birim, s.Miktar, s.BirimFiyat, s.KdvDahilMi, s.KdvOrani, s.Iskonto1, s.Iskonto2, s.Iskonto3,
            Temiz(s.TevkifatKodu), pay, payda);
    }

    private static string? Temiz(string? deger) => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();
}
