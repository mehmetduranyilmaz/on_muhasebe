using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.KasaBanka.Domain;
using OnMuhasebe.Modules.KasaBanka.Domain.Ekstre;
using OnMuhasebe.Shared.Domain.Results;
using EkstreSatiri = OnMuhasebe.Modules.KasaBanka.Domain.EkstreSatiri;

namespace OnMuhasebe.Modules.KasaBanka.Infrastructure.Ekstre;

// Ekstre aktarım süreci: dosyayı çöz, satırları öner/eşleştir, kullanıcının onayladıklarını kasa/banka işlemi olarak yaz.
internal sealed class EkstreServisi(
    KasaBankaDbContext db,
    KasaIslemServisi islemServisi,
    ICariSorgusu cariSorgusu,
    TimeProvider zaman)
{
    public const int OnizlemeSatirSayisi = 15;

    public async Task<Sonuc<EkstreAktarimi>> YukleAsync(Guid hesapId, string dosyaAdi, byte[] icerik, CancellationToken ct)
    {
        var hesap = await db.Hesaplar.SingleOrDefaultAsync(h => h.Id == hesapId, ct);
        if (hesap is null)
        {
            return KasaBankaHatalari.HesapBulunamadi;
        }

        if (hesap.Tur == HesapTuru.Kasa)
        {
            return KasaBankaHatalari.KasayaEkstreYok;
        }

        var bicim = Mt940Okuyucu.Mt940Mu(icerik) ? EkstreBicimi.Mt940 : ExcelOkuyucu.ExcelMi(icerik) ? EkstreBicimi.Excel : EkstreBicimi.Csv;
        var ad = Path.GetFileName(dosyaAdi);
        var aktarim = EkstreAktarimi.Olustur(hesapId, ad.Length > 255 ? ad[..255] : ad, bicim, icerik);
        db.EkstreAktarimlari.Add(aktarim);

        if (bicim == EkstreBicimi.Mt940)
        {
            var okunan = OkumayiDene(() => Mt940Okuyucu.Oku(icerik));
            if (!okunan.BasariliMi)
            {
                return okunan.Hata!;
            }

            var mt940 = okunan.Deger;
            if (mt940.Satirlar.Count == 0)
            {
                return KasaBankaHatalari.EkstreBos;
            }

            await HazirlaAsync(aktarim, hesap, null, mt940.Satirlar, BakiyeKontrolu.Kontrol(mt940.Satirlar, mt940.AcilisBakiyesi, mt940.KapanisBakiyesi), ct);
        }
        else
        {
            var okunan = OkumayiDene(() => Tablo(aktarim));
            if (!okunan.BasariliMi)
            {
                return okunan.Hata!;
            }

            var tablo = okunan.Deger;
            if (tablo.Count == 0)
            {
                return KasaBankaHatalari.DosyaOkunamadi;
            }

            // Kayıtlı şablon (yoksa başlıklardan tahmin) satır veriyorsa eşleme ekranı atlanır.
            var sablon = await db.EkstreSablonlari.AsNoTracking().SingleOrDefaultAsync(s => s.HesapId == hesapId, ct);
            if (sablon is not null && OkumayiDene(() => SatirCozucu.Coz(tablo, sablon.Esleme)) is { BasariliMi: true, Deger: { Satirlar.Count: > 0 } cozum })
            {
                await HazirlaAsync(aktarim, hesap, sablon.Esleme, cozum.Satirlar, BakiyeKontrolu.Kontrol(cozum.Satirlar), ct);
            }
        }

        await db.SaveChangesAsync(ct);
        return aktarim;
    }

    public async Task<Sonuc<EkstreAktarimi>> EslemeUygulaAsync(Guid aktarimId, EslemeIstegi istek, CancellationToken ct)
    {
        var aktarim = await AktarimAsync(aktarimId, ct);
        if (aktarim is null)
        {
            return KasaBankaHatalari.AktarimBulunamadi;
        }

        if (aktarim.Bicim == EkstreBicimi.Mt940 || aktarim.Icerik is null
            || aktarim.Satirlar.Any(s => s.Durum is EkstreSatirDurumu.Aktarildi))
        {
            return KasaBankaHatalari.EslemeDegistirilemez;
        }

        var okunan = OkumayiDene(() => SatirCozucu.Coz(Tablo(aktarim)!, istek.Esleme));
        if (!okunan.BasariliMi)
        {
            return okunan.Hata!;
        }

        var cozum = okunan.Deger;
        if (cozum.Satirlar.Count == 0)
        {
            return KasaBankaHatalari.EslemeSatirVermedi;
        }

        var hesap = await db.Hesaplar.SingleAsync(h => h.Id == aktarim.HesapId, ct);
        await HazirlaAsync(aktarim, hesap, istek.Esleme, cozum.Satirlar, BakiyeKontrolu.Kontrol(cozum.Satirlar), ct);

        if (istek.SablonuKaydet)
        {
            var sablon = await db.EkstreSablonlari.SingleOrDefaultAsync(s => s.HesapId == aktarim.HesapId, ct);
            if (sablon is null)
            {
                db.EkstreSablonlari.Add(EkstreSablonu.Olustur(aktarim.HesapId, istek.Esleme));
            }
            else
            {
                sablon.Guncelle(istek.Esleme);
            }
        }

        await db.SaveChangesAsync(ct);
        return aktarim;
    }

    public async Task<Sonuc<EkstreAktarimi>> SatirGuncelleAsync(Guid aktarimId, Guid satirId, SatirGuncelleIstegi istek, CancellationToken ct)
    {
        var aktarim = await AktarimAsync(aktarimId, ct);
        if (aktarim?.Satirlar.FirstOrDefault(s => s.Id == satirId) is not { } satir)
        {
            return KasaBankaHatalari.AktarimBulunamadi;
        }

        if (satir.Durum is not (EkstreSatirDurumu.Bekliyor or EkstreSatirDurumu.Hatali or EkstreSatirDurumu.ZatenKayitli))
        {
            return KasaBankaHatalari.SatirDegistirilemez;
        }

        satir.Duzenle(istek.IslemTuru, istek.CariHesapId, istek.KategoriId, istek.KarsiHesapId, istek.EslesmeyiKaldir);

        if (!string.IsNullOrWhiteSpace(istek.HatirlanacakKelime) && istek.IslemTuru != EkstreIslemTuru.Atla)
        {
            var yon = satir.GirisMi ? KuralYonu.Giris : KuralYonu.Cikis;
            var anahtar = EkstreMetni.Normalize(istek.HatirlanacakKelime);
            var kural = await db.EkstreKurallari.SingleOrDefaultAsync(k => k.AnahtarKelime == anahtar && k.Yon == yon, ct);
            if (kural is null)
            {
                kural = EkstreKurali.Olustur(anahtar, yon);
                db.EkstreKurallari.Add(kural);
            }

            kural.Ayarla(istek.IslemTuru, istek.CariHesapId, istek.KategoriId, istek.KarsiHesapId);

            // Yeni kural aynı aktarımda elle dokunulmamış bekleyen satırlara hemen uygulanır.
            foreach (var diger in aktarim.Satirlar.Where(s => s.Id != satir.Id && s.Durum == EkstreSatirDurumu.Bekliyor && !s.ElleDuzenlendi
                         && s.GirisMi == satir.GirisMi && EkstreMetni.Normalize(s.Aciklama).Contains(anahtar, StringComparison.Ordinal)))
            {
                diger.OneriUygula(new Oneri(kural.IslemTuru, kural.CariHesapId, kural.KategoriId, kural.KarsiHesapId, "Kural", kural.Id));
            }
        }

        await db.SaveChangesAsync(ct);
        return aktarim;
    }

    // Satırlar sırayla, her biri kendi işlemi olarak yazılır; hatalı satır diğerlerini durdurmaz ve hatasıyla bekler.
    public async Task<Sonuc<(EkstreAktarimi Aktarim, int Aktarilan, int ZatenKayitli, int Atlanan, int Hatali)>> AktarAsync(Guid aktarimId, CancellationToken ct)
    {
        var aktarim = await AktarimAsync(aktarimId, ct);
        if (aktarim is null)
        {
            return KasaBankaHatalari.AktarimBulunamadi;
        }

        if (aktarim.Durum != AktarimDurumu.OnayBekliyor)
        {
            return KasaBankaHatalari.AktarimHazirDegil;
        }

        var hesap = await db.Hesaplar.AsNoTracking().SingleAsync(h => h.Id == aktarim.HesapId, ct);
        int aktarilan = 0, zatenKayitli = 0, atlanan = 0, hatali = 0;
        var simdi = zaman.GetUtcNow().UtcDateTime;
        foreach (var satirId in aktarim.Satirlar.OrderBy(s => s.Sira).Select(s => s.Id).ToList())
        {
            var satir = aktarim.Satirlar.First(s => s.Id == satirId);
            switch (satir.Durum)
            {
                case EkstreSatirDurumu.ZatenKayitli:
                    var hareket = await db.KasaHareketleri.SingleOrDefaultAsync(h => h.Id == satir.EslesenHareketId, ct);
                    if (hareket is { EkstreSatiriId: null })
                    {
                        hareket.EkstreyleEslestir(satir.Id);
                        await db.SaveChangesAsync(ct);
                    }

                    zatenKayitli++;
                    continue;
                case EkstreSatirDurumu.Bekliyor or EkstreSatirDurumu.Hatali when satir.IslemTuru == EkstreIslemTuru.Atla:
                    satir.Atlandi();
                    await db.SaveChangesAsync(ct);
                    atlanan++;
                    continue;
                case EkstreSatirDurumu.Bekliyor or EkstreSatirDurumu.Hatali:
                    break;
                default:
                    continue;
            }

            var sonuc = await SatiriIsleAsync(satir, hesap, ct);
            if (!sonuc.BasariliMi)
            {
                // Başarısız işlem geri alındı; izlenen varlıklar temizlenip aktarım yeniden yüklenir.
                db.ChangeTracker.Clear();
                aktarim = (await AktarimAsync(aktarimId, ct))!;
                aktarim.Satirlar.First(s => s.Id == satirId).Basarisiz(sonuc.Hata.Mesaj);
                await db.SaveChangesAsync(ct);
                hatali++;
                continue;
            }

            satir.Aktarildi(sonuc.Deger);
            foreach (var yeni in await db.KasaHareketleri.Where(h => h.IslemId == sonuc.Deger && h.HesapId == hesap.Id).ToListAsync(ct))
            {
                yeni.EkstreyleEslestir(satir.Id);
            }

            if (satir.KuralId is { } kuralId && await db.EkstreKurallari.SingleOrDefaultAsync(k => k.Id == kuralId, ct) is { } kural)
            {
                kural.Kullanildi(simdi);
            }

            await db.SaveChangesAsync(ct);
            aktarilan++;
        }

        aktarim.TamamlandiysaKapat(zaman.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        return (aktarim, aktarilan, zatenKayitli, atlanan, hatali);
    }

    // Satırın türüne göre kasa/banka işlemi. Tutar her zaman hesabın dövizindedir.
    private async Task<Sonuc<Guid>> SatiriIsleAsync(EkstreSatiri s, KasaHesabi hesap, CancellationToken ct)
    {
        var tutar = Math.Abs(s.Tutar);
        var aciklama = s.Aciklama.Length > 400 ? s.Aciklama[..400] : s.Aciklama;
        switch (s.IslemTuru)
        {
            case EkstreIslemTuru.Tahsilat or EkstreIslemTuru.Odeme:
                if (s.CariHesapId is not { } cariId)
                {
                    return KasaBankaHatalari.SatirCariGerekli;
                }

                if ((s.IslemTuru == EkstreIslemTuru.Tahsilat) != s.GirisMi)
                {
                    return KasaBankaHatalari.SatirYonuUyusmuyor;
                }

                return await islemServisi.TahsilatOdemeAsync(
                    new TahsilatOdemeIstegi(hesap.Id, cariId, s.Tarih, tutar, null, null, s.Referans, aciklama), s.GirisMi, ct);

            case EkstreIslemTuru.Gelir or EkstreIslemTuru.Gider:
                if (s.KategoriId is not { } kategoriId)
                {
                    return KasaBankaHatalari.SatirKategoriGerekli;
                }

                if ((s.IslemTuru == EkstreIslemTuru.Gelir) != s.GirisMi)
                {
                    return KasaBankaHatalari.SatirYonuUyusmuyor;
                }

                return await islemServisi.GelirGiderAsync(new GelirGiderIstegi(
                    s.GirisMi ? GelirGiderTuru.Gelir : GelirGiderTuru.Gider, GelirGiderBicimi.Odendi, kategoriId, hesap.Id, s.CariHesapId,
                    s.Tarih, null, tutar, null, null, s.Referans, aciklama), ct);

            case EkstreIslemTuru.Virman:
                if (s.KarsiHesapId is not { } karsiId)
                {
                    return KasaBankaHatalari.SatirKarsiHesapGerekli;
                }

                var karsi = await db.Hesaplar.AsNoTracking().SingleOrDefaultAsync(h => h.Id == karsiId, ct);
                if (karsi is null || karsi.ParaBirimi != hesap.ParaBirimi)
                {
                    return KasaBankaHatalari.SatirVirmanDovizi;
                }

                return await islemServisi.VirmanAsync(s.GirisMi
                    ? new VirmanIstegi(karsiId, hesap.Id, s.Tarih, tutar, null, null, null, aciklama)
                    : new VirmanIstegi(hesap.Id, karsiId, s.Tarih, tutar, null, null, null, aciklama), ct);

            default:
                return KasaBankaHatalari.SatirDegistirilemez;
        }
    }

    // Satırlar için parmak izi, tekrar kontrolü, mutabakat ve öneri hesaplanır.
    private async Task HazirlaAsync(
        EkstreAktarimi aktarim, KasaHesabi hesap, KolonEslemesi? esleme, IReadOnlyList<CozulmusSatir> cozulmus, string? uyari, CancellationToken ct)
    {
        var izler = ParmakIzi.Hesapla(hesap.Id, cozulmus);
        var onceden = (await db.EkstreSatirlari.AsNoTracking()
                .Where(s => s.HesapId == hesap.Id && izler.Contains(s.ParmakIzi)
                            && (s.Durum == EkstreSatirDurumu.Aktarildi || s.Durum == EkstreSatirDurumu.ZatenKayitli))
                .Select(s => s.ParmakIzi)
                .ToListAsync(ct))
            .ToHashSet();

        var bas = cozulmus.Min(s => s.Tarih).AddDays(-Mutabakat.GunToleransi);
        var son = cozulmus.Max(s => s.Tarih).AddDays(Mutabakat.GunToleransi);
        var adaylar = await db.KasaHareketleri.AsNoTracking()
            .Where(h => h.HesapId == hesap.Id && h.Tarih >= bas && h.Tarih <= son && h.EkstreSatiriId == null
                        && h.IptalHareketId == null && h.IptalEdilenHareketId == null)
            .Select(h => new MutabakatAdayi(h.Id, h.Tarih, h.Giris, h.Cikis))
            .ToListAsync(ct);

        var kurallar = (await db.EkstreKurallari.AsNoTracking().ToListAsync(ct)).Select(k => k.Veri()).ToList();
        var kendiHesaplari = await db.Hesaplar.AsNoTracking()
            .Where(h => h.Id != hesap.Id && h.AktifMi)
            .Select(h => new KendiHesabi(h.Id, h.Iban, h.HesapNo))
            .ToListAsync(ct);
        var cariler = await cariSorgusu.EslestirmeAdaylariAsync(ct);

        var satirlar = new List<EkstreSatiri>(cozulmus.Count);
        for (var i = 0; i < cozulmus.Count; i++)
        {
            var c = cozulmus[i];
            var satir = EkstreSatiri.Olustur(aktarim.Id, hesap.Id, i + 1, c, izler[i]);
            satir.OneriUygula(OneriMotoru.Oner(c, hesap.Tur, kurallar, cariler, kendiHesaplari));
            if (onceden.Contains(izler[i]))
            {
                satir.DahaOnceAktarildiIsaretle();
            }
            else if (Mutabakat.Bul(c, adaylar) is { } eslesen)
            {
                satir.EslesmeAta(eslesen.HareketId);
                adaylar.Remove(eslesen);
            }

            satirlar.Add(satir);
        }

        aktarim.SatirlariYukle(esleme, satirlar, uyari);
    }

    public static List<string[]>? Tablo(EkstreAktarimi aktarim) =>
        aktarim.Icerik is not { } icerik ? null
        : aktarim.Bicim == EkstreBicimi.Excel ? ExcelOkuyucu.Oku(icerik)
        : aktarim.Bicim == EkstreBicimi.Csv ? CsvOkuyucu.Oku(icerik)
        : null;

    // Bozuk ya da desteklenmeyen dosya 500 değil anlaşılır doğrulama hatası olarak döner; sınırı aşan dosya ayrıca söylenir.
    private static Sonuc<T> OkumayiDene<T>(Func<T?> oku) where T : class
    {
        try
        {
            return oku() is { } sonuc ? sonuc : KasaBankaHatalari.DosyaOkunamadi;
        }
        catch (EkstreSiniriAsildiException hata)
        {
            return KasaBankaHatalari.EkstreCokBuyuk(hata.Message);
        }
        catch (Exception hata) when (hata is FormatException or InvalidDataException or IndexOutOfRangeException or ArgumentException
                                       or ExcelDataReader.Exceptions.ExcelReaderException or OverflowException or System.Xml.XmlException
                                       or NotSupportedException or IOException or InvalidOperationException)
        {
            return KasaBankaHatalari.DosyaOkunamadi;
        }
    }

    public static KolonEslemesi? Tahmin(EkstreAktarimi aktarim) => Tablo(aktarim) is { } tablo ? KolonTahmincisi.Tahmin(tablo) : null;

    private Task<EkstreAktarimi?> AktarimAsync(Guid id, CancellationToken ct) =>
        db.EkstreAktarimlari.Include(a => a.Satirlar).SingleOrDefaultAsync(a => a.Id == id, ct);
}
