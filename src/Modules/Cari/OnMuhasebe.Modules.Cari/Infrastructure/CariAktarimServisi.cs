using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.Cari.Domain;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.TopluAktarim;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.TopluAktarim;

namespace OnMuhasebe.Modules.Cari.Infrastructure;

// Excel'den cari kartları ve açılış bakiyeleri. Elle kayıtla aynı doğrulamadan geçer; hatalı ve mükerrer satırlar atlanır, tek işlemde kaydedilir.
internal sealed class CariAktarimServisi(
    CariDbContext db, IValidator<CariKaydetIstegi> dogrulayici, CariKodUretici kodUretici, IYetkiDenetimi yetki)
{
    private const string KodOnEki = "C";

    public static IReadOnlyList<AktarimSutunu> Sutunlar { get; } =
    [
        new("kod", "Cari Kodu", Esanlamlilar: ["Kod", "Hesap Kodu", "Müşteri Kodu"]),
        new("unvan", "Ünvan", Zorunlu: true, Esanlamlilar: ["Ünvanı", "Cari Adı", "Cari Ünvanı", "Firma Adı", "Firma Ünvanı", "Müşteri Adı", "Hesap Adı", "Adı Soyadı"], Genislik: 40),
        new("tur", "Tür", Esanlamlilar: ["Cari Türü", "Cari Tipi", "Tipi"]),
        new("kisiTipi", "Kişi Tipi", Esanlamlilar: ["Şahıs/Tüzel", "Mükellef Tipi"]),
        new("vergiNo", "Vergi No", Esanlamlilar: ["VKN", "TCKN", "VKN/TCKN", "TC Kimlik No", "Vergi Numarası", "Vergi Kimlik No", "T.C. No"]),
        new("vergiDairesi", "Vergi Dairesi", Esanlamlilar: ["V.D.", "VD"]),
        new("il", "İl", Esanlamlilar: ["Şehir"]),
        new("ilce", "İlçe"),
        new("adres", "Adres", Genislik: 40),
        new("telefon", "Telefon", Esanlamlilar: ["Tel", "Telefon No", "GSM", "Cep Telefonu"]),
        new("eposta", "E-posta", Esanlamlilar: ["Mail", "E-mail", "Email", "E-Posta Adresi"], Genislik: 28),
        new("paraBirimi", "Para Birimi", Esanlamlilar: ["Döviz", "Döviz Cinsi", "PB"]),
        new("vadeGun", "Vade Gün", Esanlamlilar: ["Vade", "Vade Günü", "Ödeme Vadesi"]),
        new("riskLimiti", "Risk Limiti", Esanlamlilar: ["Kredi Limiti", "Limit"]),
        new("grup", "Grup", Esanlamlilar: ["Cari Grubu", "Grubu"]),
        new("yetkiliAd", "Yetkili", Esanlamlilar: ["Yetkili Adı", "Yetkili Kişi", "İlgili Kişi"]),
        new("yetkiliTelefon", "Yetkili Telefon", Esanlamlilar: ["Yetkili Tel"]),
        new("iban", "IBAN", Genislik: 30),
        new("not", "Not", Esanlamlilar: ["Açıklama"], Genislik: 30),
        new("borc", "Borç Bakiye", Esanlamlilar: ["Borç", "Borç Bakiyesi", "Açılış Borç"]),
        new("alacak", "Alacak Bakiye", Esanlamlilar: ["Alacak", "Alacak Bakiyesi", "Açılış Alacak"]),
        new("kur", "Kur", Esanlamlilar: ["Döviz Kuru"])
    ];

    public async Task<Sonuc<TopluAktarimSonucu>> IsleAsync(byte[] icerik, DateOnly? acilisTarihi, bool kaydet, CancellationToken ct)
    {
        var okuma = AktarimTablosu.Oku(icerik, Sutunlar);
        if (!okuma.BasariliMi)
        {
            return okuma.Hata;
        }

        var mevcut = await db.CariHesaplar.IgnoreQueryFilters([ModulDbContext.SilinmeFiltresi]).AsNoTracking()
            .Select(c => new { c.Kod, c.VergiNo, c.Unvan, c.SilindiMi }).ToListAsync(ct);
        var silinmisKodlar = mevcut.Where(c => c.SilindiMi).Select(c => c.Kod).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var kodlar = mevcut.Where(c => !c.SilindiMi).Select(c => c.Kod).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var vergiNolari = mevcut.Where(c => !c.SilindiMi && c.VergiNo != null).Select(c => c.VergiNo!).ToHashSet();
        var unvanlar = mevcut.Where(c => !c.SilindiMi).Select(c => AktarimTablosu.Sadele(c.Unvan)).ToHashSet();
        var gruplar = (await db.CariGruplari.ToListAsync(ct)).ToDictionary(g => AktarimTablosu.Sadele(g.Ad), g => (g.Id, Yeni: (CariGrup?)null));
        var acilisYetkisi = await yetki.VarMiAsync(CariYetkileri.Hareket.Yetki(YetkiIslemleri.Ekle), ct);

        var ozetler = new List<AktarimSatiriOzeti>();
        var eklenecekler = new List<(CariKaydetIstegi Istek, CariGrup? YeniGrup, decimal Net, decimal Kur)>();
        foreach (var satir in okuma.Deger.Satirlar)
        {
            var hatalar = new List<string>();
            var unvan = satir.Metin("unvan") ?? "";
            var kod = satir.Metin("kod");
            var vergiNo = VergiNoTemizle(satir.Metin("vergiNo"));
            var net = (satir.Sayi("borc", hatalar) ?? 0) - (satir.Sayi("alacak", hatalar) ?? 0);

            if (MukerrerNedeni(kod, vergiNo, unvan, kodlar, vergiNolari, unvanlar) is { } neden)
            {
                ozetler.Add(new(satir.SatirNo, kod, unvan, AktarimSatirDurumu.Mukerrer, [neden], net == 0 ? null : net));
                continue;
            }

            if (kod is not null && silinmisKodlar.Contains(kod))
            {
                hatalar.Add($"\"{kod}\" kodu silinmiş bir carinin; başka kod verin ya da boş bırakın.");
            }

            var tur = TurCoz(satir.Metin("tur"), hatalar);
            var kisiTipi = KisiTipiCoz(satir.Metin("kisiTipi"), vergiNo, hatalar);
            var paraBirimi = ParaBirimiCoz(satir.Metin("paraBirimi"));
            var (grupId, yeniGrup) = GrupCoz(satir.Metin("grup"), gruplar, hatalar);
            var kur = paraBirimi == ParaBirimleri.Tl ? 1m : satir.Sayi("kur", hatalar) ?? 0;
            if (net != 0)
            {
                if (acilisTarihi is null)
                {
                    hatalar.Add("Açılış bakiyesi için açılış tarihi seçin.");
                }

                if (!acilisYetkisi)
                {
                    hatalar.Add("Açılış bakiyesi girmek için borç/alacak dekontu ekleme yetkiniz yok.");
                }

                if (kur <= 0)
                {
                    hatalar.Add("Dövizli carinin açılış bakiyesi için Kur sütunu gerekli.");
                }
            }

            var istek = new CariKaydetIstegi(kod, unvan, tur, kisiTipi, vergiNo, satir.Metin("vergiDairesi"), satir.Metin("il"), satir.Metin("ilce"),
                satir.Metin("adres"), satir.Metin("telefon"), satir.Metin("eposta"), paraBirimi, satir.TamSayi("vadeGun", hatalar),
                satir.Sayi("riskLimiti", hatalar), grupId, satir.Metin("yetkiliAd"), satir.Metin("yetkiliTelefon"), satir.Metin("not"), Iban: satir.Metin("iban"));
            var dogrulama = await dogrulayici.ValidateAsync(istek, ct);
            hatalar.AddRange(dogrulama.Errors.Select(e => e.ErrorMessage));
            if (hatalar.Count > 0)
            {
                ozetler.Add(new(satir.SatirNo, kod, unvan, AktarimSatirDurumu.Hatali, hatalar.Distinct().ToList(), net == 0 ? null : net));
                continue;
            }

            // Dosyanın sonraki satırları bu satıra göre mükerrer sayılır.
            if (kod is not null)
            {
                kodlar.Add(kod);
            }

            if (vergiNo is not null)
            {
                vergiNolari.Add(vergiNo);
            }

            unvanlar.Add(AktarimTablosu.Sadele(unvan));
            ozetler.Add(new(satir.SatirNo, kod, unvan, AktarimSatirDurumu.Eklenecek, [], net == 0 ? null : net));
            eklenecekler.Add((istek, yeniGrup, net, kur));
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
        List<(CariKaydetIstegi Istek, CariGrup? YeniGrup, decimal Net, decimal Kur)> satirlar, DateOnly? acilisTarihi,
        HashSet<string> kodlar, HashSet<string> silinmisKodlar, CancellationToken ct)
    {
        foreach (var grup in satirlar.Where(s => s.YeniGrup is not null).Select(s => s.YeniGrup!).Distinct())
        {
            db.CariGruplari.Add(grup);
        }

        string? sonKod = null;
        foreach (var (istek, _, net, kur) in satirlar)
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

            var cari = CariHesap.Olustur(kod.Trim(), CariBilgileri.Istekten(istek));
            db.CariHesaplar.Add(cari);
            if (net != 0)
            {
                var hareket = CariHareket.Olustur(cari, CariHareketTuru.Acilis, net > 0 ? HareketYonu.Borc : HareketYonu.Alacak, acilisTarihi!.Value, null,
                    Math.Abs(net), kur, null, "Excel aktarımı açılış bakiyesi");
                if (!hareket.BasariliMi)
                {
                    return hareket.Hata;
                }

                db.CariHareketler.Add(hareket.Deger);
            }
        }

        await db.SaveChangesAsync(ct);
        return Sonuc.Basarili();
    }

    private static string? MukerrerNedeni(string? kod, string? vergiNo, string unvan, HashSet<string> kodlar, HashSet<string> vergiNolari, HashSet<string> unvanlar)
    {
        if (kod is not null && kodlar.Contains(kod))
        {
            return $"\"{kod}\" kodlu cari zaten var.";
        }

        if (vergiNo is not null && vergiNolari.Contains(vergiNo))
        {
            return $"{vergiNo} vergi numaralı cari zaten var.";
        }

        return unvan.Length > 0 && unvanlar.Contains(AktarimTablosu.Sadele(unvan)) ? "Aynı ünvanlı cari zaten var." : null;
    }

    // Sayı olarak kaydedilen VKN'nin baştaki sıfırı Excel'de düşer (0123456789 -> 123456789).
    internal static string? VergiNoTemizle(string? metin)
    {
        if (metin is null)
        {
            return null;
        }

        var sade = metin.Replace(" ", "");
        return sade.Length == 9 && sade.All(char.IsAsciiDigit) ? "0" + sade : sade;
    }

    internal static CariTuru TurCoz(string? metin, ICollection<string> hatalar)
    {
        switch (AktarimTablosu.Sadele(metin))
        {
            case "" or "musteri" or "alici":
                return CariTuru.Musteri;
            case "tedarikci" or "satici":
                return CariTuru.Tedarikci;
            case "musteritedarikci" or "musterivetedarikci" or "alicisatici" or "alicivesatici" or "herikisi" or "ikisi" or "her2si":
                return CariTuru.MusteriVeTedarikci;
            default:
                hatalar.Add($"Tür: \"{metin}\" anlaşılamadı (Müşteri, Tedarikçi ya da Her ikisi).");
                return CariTuru.Musteri;
        }
    }

    // Boşsa vergi numarasından anlaşılır: 11 hane T.C. kimlik, şahıstır.
    internal static KisiTipi KisiTipiCoz(string? metin, string? vergiNo, ICollection<string> hatalar)
    {
        switch (AktarimTablosu.Sadele(metin))
        {
            case "":
                return vergiNo?.Length == 11 ? KisiTipi.Sahis : KisiTipi.Tuzel;
            case "tuzel" or "tuzelkisi" or "sirket" or "firma" or "kurumsal":
                return KisiTipi.Tuzel;
            case "sahis" or "gercek" or "gercekkisi" or "bireysel" or "sahisfirmasi":
                return KisiTipi.Sahis;
            default:
                hatalar.Add($"Kişi tipi: \"{metin}\" anlaşılamadı (Tüzel ya da Şahıs).");
                return KisiTipi.Tuzel;
        }
    }

    internal static string ParaBirimiCoz(string? metin) => metin?.Trim().ToUpperInvariant() switch
    {
        null or "" or "TL" or "TRL" or "₺" => ParaBirimleri.Tl,
        var kod => kod
    };

    // Olmayan grup açılır: eski programdaki grup yapısı kaybolmasın.
    private static (Guid? Id, CariGrup? Yeni) GrupCoz(string? ad, Dictionary<string, (Guid Id, CariGrup? Yeni)> gruplar, ICollection<string> hatalar)
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

        var yeni = CariGrup.Olustur(ad);
        gruplar[anahtar] = (yeni.Id, yeni);
        return (yeni.Id, yeni);
    }
}
