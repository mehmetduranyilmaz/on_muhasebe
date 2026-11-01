using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Modules.Stok.Domain;
using OnMuhasebe.Shared.Contracts.Birimler;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.TopluAktarim;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.TopluAktarim;

namespace OnMuhasebe.Modules.Stok.Infrastructure;

// Excel'den stok kartları ve açılış stokları. Elle kayıtla aynı doğrulamadan geçer; hatalı ve mükerrer satırlar atlanır, tek işlemde kaydedilir.
internal sealed class StokAktarimServisi(
    StokDbContext db, IValidator<StokKartiKaydetIstegi> dogrulayici, StokKodUretici kodUretici, DepoServisi depoServisi, IYetkiDenetimi yetki)
{
    private const string KodOnEki = "S";

    public static IReadOnlyList<AktarimSutunu> Sutunlar { get; } =
    [
        new("kod", "Stok Kodu", Esanlamlilar: ["Kod", "Ürün Kodu", "Malzeme Kodu"]),
        new("ad", "Stok Adı", Zorunlu: true, Esanlamlilar: ["Ad", "Adı", "Ürün Adı", "Malzeme Adı", "Stok Açıklaması", "Ürün"], Genislik: 40),
        new("tur", "Tür", Esanlamlilar: ["Stok Türü", "Stok Tipi"]),
        new("birim", "Birim", Esanlamlilar: ["Ana Birim", "Temel Birim", "Ölçü Birimi"]),
        new("barkod", "Barkod", Esanlamlilar: ["Barkod No", "EAN"]),
        new("kdv", "KDV Oranı", Zorunlu: true, Esanlamlilar: ["KDV", "KDV %", "KDV Yüzdesi", "Satış KDV"]),
        new("alis", "Alış Fiyatı", Esanlamlilar: ["Alış"]),
        new("satis", "Satış Fiyatı", Esanlamlilar: ["Satış", "Toptan Fiyat", "Liste Fiyatı"]),
        new("perakende", "Perakende Fiyatı", Esanlamlilar: ["Perakende"]),
        new("paraBirimi", "Para Birimi", Esanlamlilar: ["Döviz", "Döviz Cinsi", "PB"]),
        new("grup", "Grup", Esanlamlilar: ["Stok Grubu", "Grubu", "Kategori"]),
        new("kritik", "Kritik Seviye", Esanlamlilar: ["Minimum Stok", "Min Stok", "Kritik Stok"]),
        new("aciklama", "Açıklama", Genislik: 30),
        new("miktar", "Açılış Miktarı", Esanlamlilar: ["Miktar", "Stok Miktarı", "Mevcut", "Bakiye"]),
        new("maliyet", "Birim Maliyet", Esanlamlilar: ["Maliyet", "Ortalama Maliyet", "Açılış Maliyeti"]),
        new("depo", "Depo", Esanlamlilar: ["Depo Adı"])
    ];

    public async Task<Sonuc<TopluAktarimSonucu>> IsleAsync(byte[] icerik, DateOnly? acilisTarihi, bool kaydet, CancellationToken ct)
    {
        var okuma = AktarimTablosu.Oku(icerik, Sutunlar);
        if (!okuma.BasariliMi)
        {
            return okuma.Hata;
        }

        var mevcut = await db.StokKartlari.IgnoreQueryFilters([ModulDbContext.SilinmeFiltresi]).AsNoTracking()
            .Select(k => new { k.Kod, k.Ad, k.Barkod, k.SilindiMi, Barkodlar = k.Birimler.Select(b => b.Barkod).ToList() }).ToListAsync(ct);
        var silinmisKodlar = mevcut.Where(k => k.SilindiMi).Select(k => k.Kod).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var kodlar = mevcut.Where(k => !k.SilindiMi).Select(k => k.Kod).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var adlar = mevcut.Where(k => !k.SilindiMi).Select(k => AktarimTablosu.Sadele(k.Ad)).ToHashSet();

        // Elle kayıttaki kural: barkod silinmiş kartlarda aranmaz.
        var barkodlar = mevcut.Where(k => !k.SilindiMi).SelectMany(k => k.Barkodlar.Append(k.Barkod))
            .Where(b => !string.IsNullOrWhiteSpace(b)).Select(b => b!).ToHashSet();
        var gruplar = (await db.StokGruplari.ToListAsync(ct)).ToDictionary(g => AktarimTablosu.Sadele(g.Ad), g => (g.Id, Yeni: (StokGrubu?)null));
        var depolar = (await db.Depolar.AsNoTracking().Where(d => d.AktifMi).ToListAsync(ct))
            .GroupBy(d => AktarimTablosu.Sadele(d.Ad)).ToDictionary(g => g.Key, g => g.First().Id);
        var acilisYetkisi = await yetki.VarMiAsync(StokYetkileri.Hareket.Yetki(YetkiIslemleri.Ekle), ct);
        var maliyetGorur = await yetki.GorurMuAsync(ct);
        Guid? varsayilanDepo = null;

        var ozetler = new List<AktarimSatiriOzeti>();
        var eklenecekler = new List<(StokKartiKaydetIstegi Istek, StokGrubu? YeniGrup, decimal Miktar, decimal? Maliyet, Guid? DepoId)>();
        foreach (var satir in okuma.Deger.Satirlar)
        {
            var hatalar = new List<string>();
            var ad = satir.Metin("ad") ?? "";
            var kod = satir.Metin("kod");
            var barkod = satir.Metin("barkod");
            var miktar = satir.Sayi("miktar", hatalar) ?? 0;
            var acilis = miktar == 0 ? (decimal?)null : miktar;

            if (MukerrerNedeni(kod, barkod, ad, kodlar, barkodlar, adlar) is { } neden)
            {
                ozetler.Add(new(satir.SatirNo, kod, ad, AktarimSatirDurumu.Mukerrer, [neden], acilis));
                continue;
            }

            if (kod is not null && silinmisKodlar.Contains(kod))
            {
                hatalar.Add($"\"{kod}\" kodu silinmiş bir kartın; başka kod verin ya da boş bırakın.");
            }

            var tur = TurCoz(satir.Metin("tur"), hatalar);
            var birim = BirimCoz(satir.Metin("birim"), hatalar);
            var (grupId, yeniGrup) = GrupCoz(satir.Metin("grup"), gruplar, hatalar);
            var kdv = KdvCoz(satir, hatalar);

            // Maliyeti görmeyen kullanıcının alış fiyatı elle kayıtta olduğu gibi yok sayılır.
            var alis = satir.Sayi("alis", hatalar);
            var maliyet = satir.Sayi("maliyet", hatalar);
            Guid? depoId = null;
            if (miktar != 0)
            {
                if (miktar < 0)
                {
                    hatalar.Add("Açılış miktarı eksi olamaz.");
                }

                if (tur == StokTuru.Hizmet)
                {
                    hatalar.Add("Hizmet kartına açılış miktarı girilemez.");
                }

                if (acilisTarihi is null)
                {
                    hatalar.Add("Açılış stoku için açılış tarihi seçin.");
                }

                if (!acilisYetkisi)
                {
                    hatalar.Add("Açılış stoku girmek için stok hareketi ekleme yetkiniz yok.");
                }

                if (!maliyetGorur)
                {
                    hatalar.Add("Açılış stoku girmek için maliyet görme yetkisi gerekli.");
                }

                if (maliyet is null or < 0)
                {
                    hatalar.Add("Açılış stoku için Birim Maliyet gerekli.");
                }

                if (satir.Metin("depo") is { } depoAdi)
                {
                    if (depolar.TryGetValue(AktarimTablosu.Sadele(depoAdi), out var bulunan))
                    {
                        depoId = bulunan;
                    }
                    else
                    {
                        hatalar.Add($"\"{depoAdi}\" adlı depo bulunamadı; önce depoyu açın.");
                    }
                }
                else
                {
                    depoId = varsayilanDepo ??= (await depoServisi.VarsayilanAsync(ct)).Id;
                }
            }

            var istek = new StokKartiKaydetIstegi(kod, ad, tur, birim, barkod, kdv, maliyetGorur ? alis : null, satir.Sayi("satis", hatalar),
                satir.Sayi("perakende", hatalar), ParaBirimiCoz(satir.Metin("paraBirimi")), grupId, satir.Sayi("kritik", hatalar), satir.Metin("aciklama"), null);
            var dogrulama = await dogrulayici.ValidateAsync(istek, ct);
            hatalar.AddRange(dogrulama.Errors.Select(e => e.ErrorMessage));

            // Kesirli olmayan birimde küsuratlı miktar gibi hareket kuralları: geçici kartla satırda gösterilir, kayıtta yeniden kurulur.
            if (hatalar.Count == 0 && miktar != 0)
            {
                var kart = StokKarti.Olustur("GECICI", StokKartiBilgileri.Istekten(istek), []);
                if (!kart.BasariliMi)
                {
                    hatalar.Add(kart.Hata.Mesaj);
                }
                else if (StokHareket.Olustur(kart.Deger, depoId!.Value, StokHareketTuru.Acilis, acilisTarihi!.Value, birim, miktar, maliyet, null, null) is { BasariliMi: false } hareket)
                {
                    hatalar.Add(hareket.Hata.Mesaj);
                }
            }

            if (hatalar.Count > 0)
            {
                ozetler.Add(new(satir.SatirNo, kod, ad, AktarimSatirDurumu.Hatali, hatalar.Distinct().ToList(), acilis));
                continue;
            }

            if (kod is not null)
            {
                kodlar.Add(kod);
            }

            if (barkod is not null)
            {
                barkodlar.Add(barkod);
            }

            adlar.Add(AktarimTablosu.Sadele(ad));
            ozetler.Add(new(satir.SatirNo, kod, ad, AktarimSatirDurumu.Eklenecek, [], acilis));
            eklenecekler.Add((istek, yeniGrup, miktar, maliyet, depoId));
        }

        if (kaydet && eklenecekler.Count > 0)
        {
            var kayit = await KaydetAsync(eklenecekler, acilisTarihi, kodlar, silinmisKodlar, ct);
            if (!kayit.BasariliMi)
            {
                return kayit.Hata;
            }
        }

        return AktarimTablosu.Ozetle(kaydet, ozetler, okuma.Deger.TaninmayanSutunlar);
    }

    private async Task<Sonuc> KaydetAsync(
        List<(StokKartiKaydetIstegi Istek, StokGrubu? YeniGrup, decimal Miktar, decimal? Maliyet, Guid? DepoId)> satirlar, DateOnly? acilisTarihi,
        HashSet<string> kodlar, HashSet<string> silinmisKodlar, CancellationToken ct)
    {
        foreach (var grup in satirlar.Where(s => s.YeniGrup is not null).Select(s => s.YeniGrup!).Distinct())
        {
            db.StokGruplari.Add(grup);
        }

        string? sonKod = null;
        foreach (var (istek, _, miktar, maliyet, depoId) in satirlar)
        {
            var kod = istek.Kod;
            if (string.IsNullOrWhiteSpace(kod))
            {
                // Satır başına sorgu yerine tek sorgu; dosyada elle verilmiş kodlar atlanır.
                do
                {
                    sonKod = sonKod is null ? await kodUretici.SonrakiAsync(ct) : KodUretici.Artir(sonKod, KodOnEki);
                }
                while (kodlar.Contains(sonKod) || silinmisKodlar.Contains(sonKod));

                kod = sonKod;
            }

            var kart = StokKarti.Olustur(kod.Trim(), StokKartiBilgileri.Istekten(istek), []);
            if (!kart.BasariliMi)
            {
                return kart.Hata;
            }

            db.StokKartlari.Add(kart.Deger);
            if (miktar != 0)
            {
                var hareket = StokHareket.Olustur(kart.Deger, depoId!.Value, StokHareketTuru.Acilis, acilisTarihi!.Value, istek.TemelBirim, miktar, maliyet,
                    null, "Excel aktarımı açılış stoku");
                if (!hareket.BasariliMi)
                {
                    return hareket.Hata;
                }

                db.StokHareketleri.Add(hareket.Deger);
            }
        }

        await db.SaveChangesAsync(ct);
        return Sonuc.Basarili();
    }

    private static string? MukerrerNedeni(string? kod, string? barkod, string ad, HashSet<string> kodlar, HashSet<string> barkodlar, HashSet<string> adlar)
    {
        if (kod is not null && kodlar.Contains(kod))
        {
            return $"\"{kod}\" kodlu kart zaten var.";
        }

        if (barkod is not null && barkodlar.Contains(barkod))
        {
            return $"{barkod} barkodlu kart zaten var.";
        }

        return ad.Length > 0 && adlar.Contains(AktarimTablosu.Sadele(ad)) ? "Aynı adlı kart zaten var." : null;
    }

    internal static StokTuru TurCoz(string? metin, ICollection<string> hatalar)
    {
        switch (AktarimTablosu.Sadele(metin))
        {
            case "" or "urun" or "mal" or "ticarimal" or "stok" or "malzeme":
                return StokTuru.Urun;
            case "hizmet":
                return StokTuru.Hizmet;
            default:
                hatalar.Add($"Tür: \"{metin}\" anlaşılamadı (Ürün ya da Hizmet).");
                return StokTuru.Urun;
        }
    }

    // Kod ya da ad kabul edilir; eski programlardaki kısaltmalar da tanınır. Boşsa adettir.
    internal static string BirimCoz(string? metin, ICollection<string> hatalar)
    {
        var sade = AktarimTablosu.Sadele(metin);
        switch (sade)
        {
            case "" or "ad" or "adt" or "adet":
                return Birimler.Adet;
            case "kilo" or "kilogram" or "kgm":
                return "KG";
            case "litre" or "ltr" or "lt":
                return "LT";
            case "mt" or "mtr" or "metre":
                return "M";
            case "pk" or "pkt":
                return "PAKET";
        }

        if (Birimler.Tumu.FirstOrDefault(b => AktarimTablosu.Sadele(b.Kod) == sade || AktarimTablosu.Sadele(b.Ad) == sade) is { } birim)
        {
            return birim.Kod;
        }

        hatalar.Add($"Birim: \"{metin}\" tanınmıyor.");
        return Birimler.Adet;
    }

    // Excel'in yüzde biçimli hücresi 0,2 olarak okunur: 1'den küçük oran yüzdeye çevrilir ("%20", 20 ve 0,20 aynıdır).
    private static int KdvCoz(AktarimSatiri satir, ICollection<string> hatalar)
    {
        if (satir.Metin("kdv") is null)
        {
            hatalar.Add("KDV oranı boş olamaz.");
            return 0;
        }

        if (satir.Sayi("kdv", hatalar) is not { } oran)
        {
            return 0;
        }

        var yuzde = oran is > 0 and < 1 ? oran * 100 : oran;
        if (yuzde != decimal.Truncate(yuzde))
        {
            hatalar.Add("KDV oranı tam sayı olmalıdır.");
            return 0;
        }

        return (int)yuzde;
    }

    private static string ParaBirimiCoz(string? metin) => metin?.Trim().ToUpperInvariant() switch
    {
        null or "" or "TL" or "TRL" or "₺" => ParaBirimleri.Tl,
        var kod => kod
    };

    // Olmayan grup açılır: eski programdaki grup yapısı kaybolmasın.
    private static (Guid? Id, StokGrubu? Yeni) GrupCoz(string? ad, Dictionary<string, (Guid Id, StokGrubu? Yeni)> gruplar, ICollection<string> hatalar)
    {
        if (ad is null)
        {
            return (null, null);
        }

        if (ad.Length > 100)
        {
            hatalar.Add("Grup adı en çok 100 karakter olabilir.");
            return (null, null);
        }

        var anahtar = AktarimTablosu.Sadele(ad);
        if (gruplar.TryGetValue(anahtar, out var kayit))
        {
            return (kayit.Id, kayit.Yeni);
        }

        var yeni = StokGrubu.Olustur(ad);
        gruplar[anahtar] = (yeni.Id, yeni);
        return (yeni.Id, yeni);
    }
}
