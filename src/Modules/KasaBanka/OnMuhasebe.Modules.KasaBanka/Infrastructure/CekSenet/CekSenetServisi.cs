using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.KasaBanka.Domain;
using OnMuhasebe.Modules.KasaBanka.Domain.CekSenet;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Domain.Dogrulama;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.KasaBanka.Infrastructure.CekSenet;

// Çek/senet bordroları ve durum işlemleri. Her evrak hareketinin kasa ve cari kayıtları hareketin IslemId'siyle yazılır;
// geri almada aynı kimlikle ters kayıt atılır. Bordrodaki tüm evraklar tek transaction'dadır: biri başarısızsa hiçbiri yazılmaz.
internal sealed class CekSenetServisi(
    KasaBankaDbContext db,
    ICariHareketYazici cariYazici,
    CariDovizServisi doviz,
    EvrakRiskServisi risk,
    IMevcutKullanici mevcutKullanici,
    IIslemKapsami islemKapsami,
    TimeProvider zaman)
{
    public async Task<CekSenetAyari> AyarAsync(CancellationToken ct) =>
        await db.CekSenetAyarlari.AsNoTracking().SingleOrDefaultAsync(ct) ?? CekSenetAyari.Varsayilan();

    public Task<Sonuc<BordroSonucu>> GirisAsync(AlinanBordroIstegi istek, CancellationToken cancellationToken) =>
        islemKapsami.CalistirAsync<BordroSonucu>(async ct =>
        {
            var cariSonuc = await doviz.CariAsync(istek.CariHesapId, ct);
            if (!cariSonuc.BasariliMi)
            {
                return cariSonuc.Hata;
            }

            var cari = cariSonuc.Deger;
            var ayar = await AyarAsync(ct);
            var yeniKartlar = new Dictionary<string, EvrakBorclusu>();
            var borclular = new List<EvrakBorclusu>();
            foreach (var g in istek.Evraklar)
            {
                var borclu = await BorcluCozAsync(g, ayar, yeniKartlar, ct);
                if (!borclu.BasariliMi)
                {
                    return borclu.Hata;
                }

                borclular.Add(borclu.Deger);
            }

            var tekrar = await TekrarEdenCekAsync(istek.Evraklar, ct);
            if (tekrar is not null)
            {
                return tekrar;
            }

            // Onay yalnızca riskli keşideci/kefile ait evraklara işlenir; riskli olan getiren cariyse bordrodaki hepsine.
            IReadOnlyList<RiskliBorclu> riskliler = [];
            if (ayar.RiskDavranisi != RiskDavranisi.Kapali)
            {
                var kimlikler = borclular.Select(b => b.KimlikNo)
                    .Concat(istek.Evraklar.Select(e => Temiz(e.KefilKimlikNo)))
                    .Append(cari.VergiNo)
                    .OfType<string>().Distinct().ToList();
                riskliler = await risk.RiskliBorclularAsync(
                    borclular.Select(b => b.Id).ToList(), kimlikler, borclular.Where(b => b.KimlikNo is null).Select(b => b.Ad).ToList(), ct);
                if (riskliler.Count > 0)
                {
                    var mesaj = RiskMesaji.Olustur(riskliler);
                    if (ayar.RiskDavranisi == RiskDavranisi.Engelle)
                    {
                        return KasaBankaHatalari.RiskliBorcluEngellendi(mesaj);
                    }

                    if (!istek.RiskOnaylandi)
                    {
                        return KasaBankaHatalari.RiskOnayiGerekli(mesaj);
                    }
                }
            }

            var riskliKartlar = riskliler.Select(r => r.BorcluId).ToHashSet();
            var riskliKimlikler = riskliler.Select(r => r.KimlikNo).OfType<string>().ToHashSet();
            var riskliAdlar = riskliler.Where(r => r.KimlikNo is null).Select(r => r.Ad.ToUpperInvariant()).ToHashSet();
            var cariRiskli = cari.VergiNo is { } vergiNo && riskliKimlikler.Contains(vergiNo);
            bool RiskliMi(EvrakBorclusu b, AlinanEvrakGirdisi g) => cariRiskli || riskliKartlar.Contains(b.Id)
                || (b.KimlikNo is { } k && riskliKimlikler.Contains(k)) || (b.KimlikNo is null && riskliAdlar.Contains(b.Ad.ToUpperInvariant()))
                || (Temiz(g.KefilKimlikNo) is { } kefil && riskliKimlikler.Contains(kefil));

            var bordro = await BordroAsync(EvrakIslemi.Giris, istek.Tarih, cari.Id, null, istek.Aciklama, ct);
            var idler = new List<Guid>();
            for (var i = 0; i < istek.Evraklar.Count; i++)
            {
                var g = istek.Evraklar[i];
                var borclu = borclular[i];
                if (g.Tur == EvrakTuru.Cek)
                {
                    borclu.HesapHatirla(g.BankaAdi, g.Sube, g.HesapNo);
                }

                var kur = await doviz.KurAsync(g.ParaBirimi, istek.Tarih, g.Kur, ct);
                if (!kur.BasariliMi)
                {
                    return kur.Hata;
                }

                var cariTutar = await doviz.CariTutariAsync(cari.Id, g.ParaBirimi, Para.Yuvarla(g.Tutar), kur.Deger, g.CariKuru, istek.Tarih, ct);
                if (!cariTutar.BasariliMi)
                {
                    return cariTutar.Hata;
                }

                var evrak = Evrak.Alinan(await KodAsync("kasabanka.evrak", Evrak.KodOneki(EvrakYonu.Alinan, g.Tur), ct), g, istek.Tarih, cari.Id,
                    kur.Deger, cariTutar.Deger.Tutar, cariTutar.Deger.Kur, borclu, null, null);
                if (riskliler.Count > 0 && RiskliMi(borclu, g))
                {
                    evrak.RiskOnayla(mevcutKullanici.KullaniciId!.Value, zaman.GetUtcNow().UtcDateTime);
                }

                db.Evraklar.Add(evrak);
                var hareket = evrak.Ilk(bordro.Id, Guid.CreateVersion7(), istek.Tarih, istek.Aciklama);
                await db.SaveChangesAsync(ct);

                var yazim = await cariYazici.YazAsync(hareket.IslemId, new BelgeCariHareketi(
                    cari.Id, CariHareketTuru.CekSenetGirisi, HareketYonu.Alacak, istek.Tarih, g.VadeTarihi, evrak.CariTutar, evrak.CariKur,
                    BelgeNo(evrak), CariAciklamasi(evrak, "alındı", istek.Aciklama)), ct);
                if (!yazim.BasariliMi)
                {
                    return yazim.Hata;
                }

                idler.Add(evrak.Id);
            }

            return new BordroSonucu(bordro.Id, bordro.Kod, idler);
        }, cancellationToken);

    public Task<Sonuc<BordroSonucu>> CikisAsync(VerilenBordroIstegi istek, CancellationToken cancellationToken) =>
        islemKapsami.CalistirAsync<BordroSonucu>(async ct =>
        {
            var cariSonuc = await doviz.CariAsync(istek.CariHesapId, ct);
            if (!cariSonuc.BasariliMi)
            {
                return cariSonuc.Hata;
            }

            var bordro = await BordroAsync(EvrakIslemi.Cikis, istek.Tarih, istek.CariHesapId, null, istek.Aciklama, ct);
            var idler = new List<Guid>();
            foreach (var g in istek.Evraklar)
            {
                KasaHesabi? hesap = null;
                if (g.Tur == EvrakTuru.Cek)
                {
                    hesap = g.KendiHesapId is { } hId ? await db.Hesaplar.SingleOrDefaultAsync(h => h.Id == hId, ct) : null;
                    if (hesap is not { Tur: HesapTuru.Banka })
                    {
                        return KasaBankaHatalari.VerilenCekHesabi;
                    }

                    if (!hesap.AktifMi)
                    {
                        return KasaBankaHatalari.PasifHesap;
                    }

                    var seri = g.SeriNo.Trim();
                    if (await db.Evraklar.AnyAsync(e => e.Yon == EvrakYonu.Verilen && e.KendiHesapId == hesap.Id && e.SeriNo == seri
                                                        && e.Durum != EvrakDurumu.Iptal, ct))
                    {
                        return KasaBankaHatalari.EvrakZatenKayitli;
                    }
                }

                var paraBirimi = hesap?.ParaBirimi ?? g.ParaBirimi ?? Shared.Contracts.Doviz.ParaBirimleri.Tl;
                var kur = await doviz.KurAsync(paraBirimi, istek.Tarih, g.Kur, ct);
                if (!kur.BasariliMi)
                {
                    return kur.Hata;
                }

                var cariTutar = await doviz.CariTutariAsync(istek.CariHesapId, paraBirimi, Para.Yuvarla(g.Tutar), kur.Deger, g.CariKuru, istek.Tarih, ct);
                if (!cariTutar.BasariliMi)
                {
                    return cariTutar.Hata;
                }

                var evrak = Evrak.Verilen(await KodAsync("kasabanka.evrak", Evrak.KodOneki(EvrakYonu.Verilen, g.Tur), ct), g, paraBirimi, istek.Tarih,
                    istek.CariHesapId, kur.Deger, cariTutar.Deger.Tutar, cariTutar.Deger.Kur, hesap);
                db.Evraklar.Add(evrak);
                var hareket = evrak.Ilk(bordro.Id, Guid.CreateVersion7(), istek.Tarih, istek.Aciklama);
                await db.SaveChangesAsync(ct);

                var yazim = await cariYazici.YazAsync(hareket.IslemId, new BelgeCariHareketi(
                    istek.CariHesapId, CariHareketTuru.CekSenetCikisi, HareketYonu.Borc, istek.Tarih, g.VadeTarihi, evrak.CariTutar, evrak.CariKur,
                    BelgeNo(evrak), CariAciklamasi(evrak, "verildi", istek.Aciklama)), ct);
                if (!yazim.BasariliMi)
                {
                    return yazim.Hata;
                }

                idler.Add(evrak.Id);
            }

            return new BordroSonucu(bordro.Id, bordro.Kod, idler);
        }, cancellationToken);

    public Task<Sonuc<BordroSonucu>> IslemAsync(EvrakIslemIstegi istek, CancellationToken cancellationToken) =>
        islemKapsami.CalistirAsync<BordroSonucu>(async ct =>
        {
            var idler = istek.EvrakIdleri.Distinct().ToList();
            if (istek.Tutar is not null && idler.Count > 1)
            {
                return KasaBankaHatalari.EvrakKismiTahsilTekEvrak;
            }

            var evraklar = await db.Evraklar.Include(e => e.Hareketler).Where(e => idler.Contains(e.Id)).ToListAsync(ct);
            if (evraklar.Count != idler.Count)
            {
                return KasaBankaHatalari.EvrakBulunamadi;
            }

            var gecisler = new Dictionary<Guid, Gecis>();
            foreach (var e in evraklar)
            {
                if (EvrakGecisleri.Bul(e.Yon, e.Durum, istek.Islem) is not { } g)
                {
                    return KasaBankaHatalari.EvrakIslemiGecersizDetay(e.Kod, EvrakAdlari.Durum(e.Durum));
                }

                gecisler[e.Id] = g;
            }

            var ornek = gecisler[evraklar[0].Id];
            KasaHesabi? secilenHesap = null;
            if (istek.HesapId is { } hesapId)
            {
                secilenHesap = await db.Hesaplar.SingleOrDefaultAsync(h => h.Id == hesapId, ct);
                if (secilenHesap is null)
                {
                    return KasaBankaHatalari.HesapBulunamadi;
                }
            }

            CariBilgisi? ciroCari = null;
            if (ornek.CariGerekli)
            {
                if (istek.CariHesapId is not { } ciroId)
                {
                    return KasaBankaHatalari.EvrakCariGerekli;
                }

                var c = await doviz.CariAsync(ciroId, ct);
                if (!c.BasariliMi)
                {
                    return c.Hata;
                }

                ciroCari = c.Deger;
                if (evraklar.Any(e => e.CariHesapId == ciroId))
                {
                    return KasaBankaHatalari.EvrakAyniCariyeCiro;
                }
            }

            var bordro = await BordroAsync(istek.Islem, istek.Tarih, ciroCari?.Id, secilenHesap?.Id, istek.Aciklama, ct);
            foreach (var evrak in evraklar.OrderBy(e => e.Kod))
            {
                var sonuc = await EvrakaUygulaAsync(evrak, gecisler[evrak.Id], istek, bordro, secilenHesap, ciroCari, ct);
                if (!sonuc.BasariliMi)
                {
                    return sonuc.Hata;
                }
            }

            return new BordroSonucu(bordro.Id, bordro.Kod, evraklar.Select(e => e.Id).ToList());
        }, cancellationToken);

    // Son hareketin kasa ve cari kayıtları ters kayıtla kapatılır, evrak önceki durumuna döner. Giriş/çıkış geri alınırsa evrak iptal olur.
    public Task<Sonuc<Guid>> GeriAlAsync(Guid evrakId, CancellationToken cancellationToken) =>
        islemKapsami.CalistirAsync<Guid>(async ct =>
        {
            var evrak = await db.Evraklar.Include(e => e.Hareketler).SingleOrDefaultAsync(e => e.Id == evrakId, ct);
            if (evrak is null)
            {
                return KasaBankaHatalari.EvrakBulunamadi;
            }

            var geri = evrak.SonHareketiGeriAl(zaman.GetUtcNow().UtcDateTime);
            if (!geri.BasariliMi)
            {
                return geri.Hata;
            }

            var hareket = geri.Deger;
            var kasaHareketleri = await db.KasaHareketleri
                .Where(h => h.IslemId == hareket.IslemId && h.IptalHareketId == null && h.IptalEdilenHareketId == null)
                .ToListAsync(ct);
            foreach (var kh in kasaHareketleri)
            {
                var ters = kh.IptalEt();
                if (!ters.BasariliMi)
                {
                    return ters.Hata;
                }

                db.KasaHareketleri.Add(ters.Deger);
            }

            await db.SaveChangesAsync(ct);
            var cari = await cariYazici.IptalEtAsync(hareket.IslemId, ct);
            return cari.BasariliMi ? evrak.Id : cari.Hata;
        }, cancellationToken);

    private async Task<Sonuc> EvrakaUygulaAsync(
        Evrak evrak, Gecis gecis, EvrakIslemIstegi istek, EvrakBordrosu bordro, KasaHesabi? secilenHesap, CariBilgisi? ciroCari, CancellationToken ct)
    {
        var hesapSonuc = await IslemHesabiAsync(evrak, gecis, secilenHesap, ct);
        if (!hesapSonuc.BasariliMi)
        {
            return hesapSonuc.Hata;
        }

        var hesap = hesapSonuc.Deger;
        var kur = await doviz.KurAsync(evrak.ParaBirimi, istek.Tarih, istek.Kur, ct);
        if (!kur.BasariliMi)
        {
            return kur.Hata;
        }

        (Guid Id, decimal Tutar, decimal Kur)? ciro = null;
        if (ciroCari is not null)
        {
            var ct2 = await doviz.CariTutariAsync(ciroCari.Id, evrak.ParaBirimi, evrak.Tutar, kur.Deger, istek.CariKuru, istek.Tarih, ct);
            if (!ct2.BasariliMi)
            {
                return ct2.Hata;
            }

            ciro = (ciroCari.Id, ct2.Deger.Tutar, ct2.Deger.Kur);
        }

        var uygulama = evrak.Uygula(gecis, bordro.Id, Guid.CreateVersion7(), istek.Tarih, hesap?.Id, ciro, istek.Tutar, istek.Aciklama);
        if (!uygulama.BasariliMi)
        {
            return uygulama.Hata;
        }

        var hareket = uygulama.Deger;
        db.EvrakHareketleri.Add(hareket);
        if (gecis.ParaGirisi is { } giris)
        {
            var kasa = KasaHareketi.Olustur(hesap!, hareket.IslemId, giris ? KasaHareketTuru.CekSenetTahsil : KasaHareketTuru.CekSenetOdeme, giris,
                istek.Tarih, hareket.Tutar, kur.Deger, BelgeNo(evrak), $"{IslemAdi(gecis.Islem)}: {BelgeNo(evrak)}", evrak.CariHesapId,
                kaynakBelgeId: evrak.Id);
            if (!kasa.BasariliMi)
            {
                return kasa.Hata;
            }

            db.KasaHareketleri.Add(kasa.Deger);
        }

        await db.SaveChangesAsync(ct);
        return await CariYazAsync(evrak, gecis.Cari, hareket, istek, kur.Deger, ct);
    }

    private async Task<Sonuc> CariYazAsync(Evrak e, CariEtkisi etki, EvrakHareketi h, EvrakIslemIstegi istek, decimal kur, CancellationToken ct)
    {
        Task<Sonuc> Yaz(Guid cariId, CariHareketTuru tur, HareketYonu yon, decimal tutar, decimal cariKur, DateOnly? vade, string fiil) =>
            cariYazici.YazAsync(h.IslemId, new BelgeCariHareketi(cariId, tur, yon, h.Tarih, vade, tutar, cariKur, BelgeNo(e),
                CariAciklamasi(e, fiil, istek.Aciklama)), ct);

        switch (etki)
        {
            case CariEtkisi.MusteriIade:
                return await Yaz(e.CariHesapId, CariHareketTuru.CekSenetIadesi, HareketYonu.Borc, e.CariTutar, e.CariKur, null, "iade edildi");
            case CariEtkisi.MusteriKarsiliksiz:
                return await Yaz(e.CariHesapId, CariHareketTuru.KarsiliksizCekSenet, HareketYonu.Borc, e.CariTutar, e.CariKur, null, "karşılıksız çıktı");
            case CariEtkisi.MusteriTahsilat:
                var tahsil = await doviz.CariTutariAsync(e.CariHesapId, e.ParaBirimi, h.Tutar, kur, istek.CariKuru, h.Tarih, ct);
                return tahsil.BasariliMi
                    ? await Yaz(e.CariHesapId, CariHareketTuru.Tahsilat, HareketYonu.Alacak, tahsil.Deger.Tutar, tahsil.Deger.Kur, null, "karşılıksızdan tahsil edildi")
                    : tahsil.Hata;
            case CariEtkisi.CiroBorc:
                return await Yaz(e.CiroCariId!.Value, CariHareketTuru.CekSenetCikisi, HareketYonu.Borc, e.CiroCariTutar!.Value, e.CiroCariKur!.Value,
                    e.VadeTarihi, "ciro edildi");
            case CariEtkisi.CiroIade:
                return await Yaz(h.OncekiCiroCariId!.Value, CariHareketTuru.CekSenetIadesi, HareketYonu.Alacak, h.OncekiCiroCariTutar!.Value,
                    h.OncekiCiroCariKur!.Value, null, "cirodan geri alındı");
            case CariEtkisi.CiroKarsiliksiz:
                var iade = await Yaz(h.OncekiCiroCariId!.Value, CariHareketTuru.CekSenetIadesi, HareketYonu.Alacak, h.OncekiCiroCariTutar!.Value,
                    h.OncekiCiroCariKur!.Value, null, "karşılıksız iade alındı");
                return iade.BasariliMi
                    ? await Yaz(e.CariHesapId, CariHareketTuru.KarsiliksizCekSenet, HareketYonu.Borc, e.CariTutar, e.CariKur, null, "karşılıksız çıktı")
                    : iade;
            case CariEtkisi.TedarikciIade:
                return await Yaz(e.CariHesapId, CariHareketTuru.CekSenetIadesi, HareketYonu.Alacak, e.CariTutar, e.CariKur, null, "iade alındı");
            case CariEtkisi.TedarikciKarsiliksiz:
                return await Yaz(e.CariHesapId, CariHareketTuru.KarsiliksizCekSenet, HareketYonu.Alacak, e.CariTutar, e.CariKur, null, "karşılıksız çıktı");
            default:
                return Sonuc.Basarili();
        }
    }

    // İşlemin kullandığı hesap: seçilen ya da evrakta kayıtlı olan; tür ve döviz uyumu kontrol edilir.
    private async Task<Sonuc<KasaHesabi?>> IslemHesabiAsync(Evrak evrak, Gecis gecis, KasaHesabi? secilen, CancellationToken ct)
    {
        KasaHesabi? hesap;
        switch (gecis.Hesap)
        {
            case HesapIhtiyaci.Yok:
                return (KasaHesabi?)null;
            case HesapIhtiyaci.Banka:
                hesap = secilen;
                if (hesap is not null && hesap.Tur != HesapTuru.Banka)
                {
                    return KasaBankaHatalari.EvrakHesapTuru;
                }

                break;
            case HesapIhtiyaci.Kayitli when evrak.Yon == EvrakYonu.Alinan:
                hesap = await db.Hesaplar.SingleOrDefaultAsync(h => h.Id == evrak.KonumHesapId, ct);
                break;
            case HesapIhtiyaci.Kayitli when evrak.Tur == EvrakTuru.Cek:
                hesap = await db.Hesaplar.SingleOrDefaultAsync(h => h.Id == evrak.KendiHesapId, ct);
                break;
            default:
                hesap = secilen;
                if (hesap is not null && hesap.Tur is not (HesapTuru.Kasa or HesapTuru.Banka))
                {
                    return KasaBankaHatalari.EvrakHesapTuru;
                }

                break;
        }

        if (hesap is null)
        {
            return KasaBankaHatalari.EvrakHesapGerekli;
        }

        return hesap.ParaBirimi == evrak.ParaBirimi ? hesap : KasaBankaHatalari.EvrakHesapDovizi;
    }

    // Kayıtlı kart (id ya da kimlik no ile) bulunur; yoksa kart açılır. Aynı bordrodaki aynı kişi tek kartla eşleşir.
    private async Task<Sonuc<EvrakBorclusu>> BorcluCozAsync(
        AlinanEvrakGirdisi g, CekSenetAyari ayar, Dictionary<string, EvrakBorclusu> yeniKartlar, CancellationToken ct)
    {
        var kimlik = Temiz(g.Borclu.KimlikNo);
        if (kimlik is not null && !VergiKimlik.GecerliMi(kimlik))
        {
            return KasaBankaHatalari.KimlikNoGecersiz;
        }

        if (Temiz(g.KefilKimlikNo) is { } kefil && !VergiKimlik.GecerliMi(kefil))
        {
            return KasaBankaHatalari.KimlikNoGecersiz;
        }

        EvrakBorclusu? kart;
        if (g.Borclu.BorcluId is { } id)
        {
            kart = await db.EvrakBorclulari.Include(b => b.Hesaplar).SingleOrDefaultAsync(b => b.Id == id, ct);
            if (kart is null)
            {
                return KasaBankaHatalari.BorcluBulunamadi;
            }
        }
        else
        {
            var ad = Temiz(g.Borclu.Ad);
            var anahtar = kimlik ?? ad?.ToUpperInvariant();
            if (anahtar is not null && yeniKartlar.TryGetValue(anahtar, out var yeni))
            {
                return yeni;
            }

            kart = kimlik is not null
                ? await db.EvrakBorclulari.Include(b => b.Hesaplar).SingleOrDefaultAsync(b => b.KimlikNo == kimlik, ct)
                : ad is null ? null
                : await db.EvrakBorclulari.Include(b => b.Hesaplar).FirstOrDefaultAsync(b => b.KimlikNo == null && b.Ad.ToUpper() == ad.ToUpper(), ct);
            if (kart is null)
            {
                if (ad is null)
                {
                    return KasaBankaHatalari.BorcluAdiGerekli;
                }

                if (kimlik is null && ayar.KimlikZorunluMu(g.Tur))
                {
                    return KasaBankaHatalari.KimlikNoGerekli;
                }

                kart = EvrakBorclusu.Olustur(ad, kimlik, g.Borclu.Telefon);
                db.EvrakBorclulari.Add(kart);
                yeniKartlar[anahtar!] = kart;
            }
        }

        return kart.KimlikNo is null && ayar.KimlikZorunluMu(g.Tur) ? KasaBankaHatalari.KimlikNoGerekli : kart;
    }

    // Aynı hesaptan aynı seri numaralı çek ikinci kez portföye giremez (iptal edilenler hariç).
    private async Task<Hata?> TekrarEdenCekAsync(IReadOnlyList<AlinanEvrakGirdisi> evraklar, CancellationToken ct)
    {
        var cekler = evraklar.Where(e => e.Tur == EvrakTuru.Cek && !string.IsNullOrWhiteSpace(e.HesapNo))
            .Select(e => (Seri: e.SeriNo.Trim(), HesapNo: e.HesapNo!.Trim())).ToList();
        if (cekler.Distinct().Count() != cekler.Count)
        {
            return KasaBankaHatalari.EvrakZatenKayitli;
        }

        var seriler = cekler.Select(c => c.Seri).ToList();
        var mevcut = await db.Evraklar.AsNoTracking()
            .Where(e => e.Yon == EvrakYonu.Alinan && e.Tur == EvrakTuru.Cek && seriler.Contains(e.SeriNo) && e.Durum != EvrakDurumu.Iptal)
            .Select(e => new { e.SeriNo, e.HesapNo })
            .ToListAsync(ct);
        return mevcut.Any(m => cekler.Contains((m.SeriNo, m.HesapNo ?? ""))) ? KasaBankaHatalari.EvrakZatenKayitli : null;
    }

    private async Task<EvrakBordrosu> BordroAsync(EvrakIslemi islem, DateOnly tarih, Guid? cariId, Guid? hesapId, string? aciklama, CancellationToken ct)
    {
        var bordro = EvrakBordrosu.Olustur(await KodAsync("kasabanka.evrak_bordrosu", EvrakBordrosu.KodOneki, ct), islem, tarih, cariId, hesapId, aciklama);
        db.EvrakBordrolari.Add(bordro);
        await db.SaveChangesAsync(ct);
        return bordro;
    }

    private Task<string> KodAsync(string tablo, string onek, CancellationToken ct) =>
        KodUretici.SonrakiAsync(db, tablo, onek, mevcutKullanici.FirmaId!.Value, ct);

    private static string BelgeNo(Evrak e) => $"{e.Kod}/{e.SeriNo}";

    private static string CariAciklamasi(Evrak e, string fiil, string? ek) =>
        $"{(e.Tur == EvrakTuru.Cek ? "Çek" : "Senet")} {fiil} · vade {e.VadeTarihi:dd.MM.yyyy}{(string.IsNullOrWhiteSpace(ek) ? "" : $" - {ek.Trim()}")}";

    private static string IslemAdi(EvrakIslemi islem) => islem switch
    {
        EvrakIslemi.Tahsil => "Çek/senet tahsili",
        EvrakIslemi.ElTahsil => "Elden tahsil",
        EvrakIslemi.KarsiliksizdanTahsil => "Karşılıksızdan tahsil",
        EvrakIslemi.Odeme => "Çek/senet ödemesi",
        _ => islem.ToString()
    };

    private static string? Temiz(string? deger) => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();
}
