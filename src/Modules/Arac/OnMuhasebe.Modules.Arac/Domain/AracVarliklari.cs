using System.Text;
using System.Text.RegularExpressions;
using OnMuhasebe.Modules.Arac.Contracts;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Domain.Abstractions;
using OnMuhasebe.Shared.Domain.Entities;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Arac.Domain;

internal static class AracHatalari
{
    public static readonly Hata Bulunamadi = Hata.Bulunamadi("arac_bulunamadi", "Araç bulunamadı.");
    public static readonly Hata KayitBulunamadi = Hata.Bulunamadi("arac_kaydi_bulunamadi", "Kayıt bulunamadı.");
    public static readonly Hata PlakaKayitli = Hata.Cakisma("plaka_kayitli", "Bu plaka başka bir araçta kayıtlı.");
    public static readonly Hata PlakaGecersiz = Hata.Dogrulama("plaka_gecersiz", "Plaka geçersiz (ör. 34 ABC 123). İş makinesinde tescil no girilebilir.");
    public static readonly Hata BitisBaslangictanOnce = Hata.Dogrulama("bitis_tarihi", "Bitiş tarihi başlangıç tarihinden önce olamaz.");
    public static readonly Hata YenilemeTarihi = Hata.IsKurali("yenileme_tarihi", "Yeni bitiş tarihi mevcut bitiş tarihinden sonra olmalıdır.");
    public static readonly Hata ZatenYenilendi = Hata.IsKurali("zaten_yenilendi", "Bu kayıt daha önce yenilenmiş; güncel kayıt üzerinden işlem yapın.");
    public static readonly Hata MtvKayitli = Hata.Cakisma("mtv_kayitli", "Bu dönemin MTV taksiti zaten kayıtlı.");
    public static readonly Hata MasrafMerkeziGecersiz = Hata.Dogrulama("masraf_merkezi", "Masraf merkezi bulunamadı ya da pasif.");
    public static readonly Hata DemirbasGecersiz = Hata.Dogrulama("demirbas_gecersiz", "Demirbaş kartı bulunamadı.");
    public static readonly Hata FaturaGecersiz = Hata.Dogrulama("fatura_gecersiz", "Bağlanan fatura bulunamadı.");
}

// Plaka boşluksuz büyük harf saklanır: "34 abc 123" ile "34ABC123" aynı araçtır.
internal static partial class Plakalar
{
    public static string Normalize(string plaka)
    {
        var sade = new StringBuilder(plaka.Length);
        foreach (var c in plaka)
        {
            if (char.IsLetterOrDigit(c))
            {
                sade.Append(c switch { 'i' or 'ı' or 'İ' => 'I', 'ş' => 'Ş', 'ğ' => 'Ğ', 'ü' => 'Ü', 'ö' => 'Ö', 'ç' => 'Ç', _ => char.ToUpperInvariant(c) });
            }
        }

        return sade.ToString();
    }

    // İl kodu 01-81, 1-3 harf, 2-4 rakam. İş makinesinde tescil numarası serbesttir.
    public static bool GecerliMi(string normal, AracTuru tur) =>
        normal.Length is > 0 and <= 20 && (tur == AracTuru.IsMakinesi || TurkPlakasi().IsMatch(normal));

    [GeneratedRegex("^(0[1-9]|[1-7][0-9]|8[01])[A-Z]{1,3}[0-9]{2,4}$")]
    private static partial Regex TurkPlakasi();
}

internal sealed class AracKarti : FirmaEntity, ISilinebilir, IIsyeriVarligi
{
    private AracKarti() { }

    public string Plaka { get; private set; } = null!;
    public string Marka { get; private set; } = null!;
    public string Model { get; private set; } = null!;
    public int? ModelYili { get; private set; }
    public AracTuru Tur { get; private set; }
    public YakitTuru Yakit { get; private set; }
    public string? SasiNo { get; private set; }
    public string? MotorNo { get; private set; }
    public string? RuhsatSahibi { get; private set; }
    public AracMulkiyeti Mulkiyet { get; private set; }
    public Guid? IsyeriId { get; private set; }
    public string? Surucu { get; private set; }
    public string? SurucuTelefon { get; private set; }
    public int? GuncelKm { get; private set; }
    public AracDurumu Durum { get; private set; }
    public Guid? DemirbasId { get; private set; }
    public Guid? MasrafMerkeziId { get; private set; }
    public string? Aciklama { get; private set; }
    public bool SilindiMi { get; private set; }
    public DateTime? SilinmeTarihi { get; private set; }

    public static AracKarti Olustur(AracKaydetIstegi i)
    {
        var arac = new AracKarti();
        arac.Guncelle(i);
        return arac;
    }

    public void Guncelle(AracKaydetIstegi i)
    {
        Plaka = Plakalar.Normalize(i.Plaka);
        Marka = i.Marka.Trim();
        Model = i.Model.Trim();
        ModelYili = i.ModelYili;
        Tur = i.Tur;
        Yakit = i.Yakit;
        SasiNo = Metin.Temiz(i.SasiNo);
        MotorNo = Metin.Temiz(i.MotorNo);
        RuhsatSahibi = Metin.Temiz(i.RuhsatSahibi);
        Mulkiyet = i.Mulkiyet;
        IsyeriId = i.IsyeriId;
        Surucu = Metin.Temiz(i.Surucu);
        SurucuTelefon = Metin.Temiz(i.SurucuTelefon);
        GuncelKm = i.GuncelKm;
        Durum = i.Durum;
        DemirbasId = i.DemirbasId;
        MasrafMerkeziId = i.MasrafMerkeziId;
        Aciklama = Metin.Temiz(i.Aciklama);
    }

    // Masraf kaydındaki km aracın sayacını ileri alır; eski tarihli kayıt geri almaz.
    public void KmIlerlet(int? km)
    {
        if (km is { } deger && deger > (GuncelKm ?? 0))
        {
            GuncelKm = deger;
        }
    }
}

internal sealed class AracYukumlulugu : FirmaEntity
{
    private AracYukumlulugu() { }

    public Guid AracId { get; private set; }
    public YukumlulukTuru Tur { get; private set; }
    public string? Ad { get; private set; }
    public DateOnly? BaslangicTarihi { get; private set; }
    public DateOnly BitisTarihi { get; private set; }
    public string? BelgeNo { get; private set; }
    public string? Sirket { get; private set; }
    public decimal? Tutar { get; private set; }
    public int HatirlatmaGun { get; private set; }
    public bool YenilendiMi { get; private set; }

    public static Sonuc<AracYukumlulugu> Olustur(Guid aracId, YukumlulukKaydetIstegi i)
    {
        var y = new AracYukumlulugu { AracId = aracId };
        var sonuc = y.Guncelle(i);
        return sonuc.BasariliMi ? y : sonuc.Hata;
    }

    public Sonuc Guncelle(YukumlulukKaydetIstegi i)
    {
        if (i.BaslangicTarihi is { } baslangic && i.BitisTarihi < baslangic)
        {
            return AracHatalari.BitisBaslangictanOnce;
        }

        Tur = i.Tur;
        Ad = Metin.Temiz(i.Ad);
        BaslangicTarihi = i.BaslangicTarihi;
        BitisTarihi = i.BitisTarihi;
        BelgeNo = Metin.Temiz(i.BelgeNo);
        Sirket = Metin.Temiz(i.Sirket);
        Tutar = i.Tutar is { } t ? Para.Yuvarla(t) : null;
        HatirlatmaGun = i.HatirlatmaGun;
        return Sonuc.Basarili();
    }

    // Yeni dönem ayrı kayıttır: eski poliçenin bilgisi ve dosyası geçmişte kalır.
    public Sonuc<AracYukumlulugu> Yenile(YukumlulukKaydetIstegi i)
    {
        if (YenilendiMi)
        {
            return AracHatalari.ZatenYenilendi;
        }

        if (i.BitisTarihi <= BitisTarihi)
        {
            return AracHatalari.YenilemeTarihi;
        }

        var yeni = Olustur(AracId, i with { Tur = Tur, Ad = i.Ad ?? Ad });
        if (yeni.BasariliMi)
        {
            YenilendiMi = true;
        }

        return yeni;
    }
}

internal sealed class AracMtv : FirmaEntity
{
    private AracMtv() { }

    public Guid AracId { get; private set; }
    public int Yil { get; private set; }
    public int Taksit { get; private set; }
    public decimal Tutar { get; private set; }
    public DateOnly SonOdemeTarihi { get; private set; }
    public DateOnly? OdemeTarihi { get; private set; }

    public static AracMtv Olustur(Guid aracId, MtvKaydetIstegi i) => new()
    {
        AracId = aracId,
        Yil = i.Yil,
        Taksit = i.Taksit,
        Tutar = Para.Yuvarla(i.Tutar),
        SonOdemeTarihi = i.SonOdemeTarihi ?? VarsayilanSonOdeme(i.Yil, i.Taksit)
    };

    public static DateOnly VarsayilanSonOdeme(int yil, int taksit) => taksit == 1 ? new DateOnly(yil, 1, 31) : new DateOnly(yil, 7, 31);

    public void Ode(DateOnly? tarih) => OdemeTarihi = tarih;
}

internal sealed class AracMasrafi : FirmaEntity
{
    private AracMasrafi() { }

    public Guid AracId { get; private set; }
    public AracMasrafTuru Tur { get; private set; }
    public DateOnly Tarih { get; private set; }
    public decimal Tutar { get; private set; }
    public int? Km { get; private set; }
    public decimal? Litre { get; private set; }
    public string? Tedarikci { get; private set; }
    public string? BelgeNo { get; private set; }
    public Guid? FaturaId { get; private set; }
    public string? Aciklama { get; private set; }

    public static AracMasrafi Olustur(Guid aracId, MasrafKaydetIstegi i)
    {
        var m = new AracMasrafi { AracId = aracId };
        m.Guncelle(i);
        return m;
    }

    public void Guncelle(MasrafKaydetIstegi i)
    {
        Tur = i.Tur;
        Tarih = i.Tarih;
        Tutar = Para.Yuvarla(i.Tutar);
        Km = i.Km;
        Litre = i.Tur == AracMasrafTuru.Yakit ? i.Litre : null;
        Tedarikci = Metin.Temiz(i.Tedarikci);
        BelgeNo = Metin.Temiz(i.BelgeNo);
        FaturaId = i.FaturaId;
        Aciklama = Metin.Temiz(i.Aciklama);
    }
}

internal sealed class AracHasari : FirmaEntity
{
    private AracHasari() { }

    public Guid AracId { get; private set; }
    public DateOnly Tarih { get; private set; }
    public HasarKusuru Kusur { get; private set; }
    public string? Aciklama { get; private set; }
    public decimal OnarimTutari { get; private set; }
    public decimal SigortaOdemesi { get; private set; }
    public string? DosyaNo { get; private set; }

    public static AracHasari Olustur(Guid aracId, HasarKaydetIstegi i)
    {
        var h = new AracHasari { AracId = aracId };
        h.Guncelle(i);
        return h;
    }

    public void Guncelle(HasarKaydetIstegi i)
    {
        Tarih = i.Tarih;
        Kusur = i.Kusur;
        Aciklama = Metin.Temiz(i.Aciklama);
        OnarimTutari = Para.Yuvarla(i.OnarimTutari);
        SigortaOdemesi = Para.Yuvarla(i.SigortaOdemesi);
        DosyaNo = Metin.Temiz(i.DosyaNo);
    }
}

internal static class Metin
{
    public static string? Temiz(string? deger) => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();
}

internal static class AracHesaplari
{
    public static SureDurumu Durum(DateOnly bitis, int hatirlatmaGun, DateOnly bugun) =>
        bitis < bugun ? SureDurumu.SuresiDoldu
        : bitis.DayNumber - bugun.DayNumber <= hatirlatmaGun ? SureDurumu.Yaklasiyor
        : SureDurumu.Gecerli;

    // Dönemde yapılan km: masraf kayıtlarındaki en büyük ve en küçük km farkı. Tek kayıttan km çıkmaz.
    public static int? DonemKm(IReadOnlyCollection<int> kmler) =>
        kmler.Count >= 2 && kmler.Max() > kmler.Min() ? kmler.Max() - kmler.Min() : null;

    // Depo dolumları arası tüketim: ilk dolumun yakıtı önceki yol içindir, hesaba girmez. En az iki km'li yakıt kaydı gerekir.
    public static decimal? Tuketim(IReadOnlyList<(int Km, decimal Litre)> dolumlar)
    {
        if (dolumlar.Count < 2)
        {
            return null;
        }

        var sirali = dolumlar.OrderBy(d => d.Km).ToList();
        var km = sirali[^1].Km - sirali[0].Km;
        return km <= 0 ? null : Math.Round(sirali.Skip(1).Sum(d => d.Litre) * 100m / km, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal? KmBasina(decimal toplam, int? km) =>
        km is > 0 ? Math.Round(toplam / km.Value, 2, MidpointRounding.AwayFromZero) : null;
}
