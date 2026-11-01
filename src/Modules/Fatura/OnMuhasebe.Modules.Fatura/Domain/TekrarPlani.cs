using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Text.Json;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Domain.Abstractions;
using OnMuhasebe.Shared.Domain.Entities;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Fatura.Domain;

internal static class TekrarHatalari
{
    public static readonly Hata Bulunamadi = Hata.Bulunamadi("tekrar_bulunamadi", "Tekrar planı bulunamadı.");
    public static readonly Hata Bitti = Hata.IsKurali("tekrar_bitti", "Bitmiş plan değiştirilemez; başlatmak için bitiş tarihini ileri alın.");
    public static readonly Hata HenuzDegil = Hata.IsKurali("tekrar_tarih_gelmedi", "Planın sıradaki fatura tarihi henüz gelmedi.");
    public static readonly Hata Pasif = Hata.IsKurali("tekrar_pasif", "Plan aktif değil.");
    public static readonly Hata BitisOnce = Hata.Dogrulama("tekrar_bitis", "Bitiş tarihi başlangıçtan önce olamaz.");
    public static readonly Hata FaturaYetkisi = Hata.Yetki("yetki_yok", "Fatura kesme yetkiniz yok.");
    public static readonly Hata ArtisEksik = Hata.Dogrulama("tekrar_artis", "Yıllık artış için oran ve ay birlikte girilmelidir.");
}

internal static class TekrarTakvimi
{
    public static int AyAdimi(TekrarSikligi s) => s switch
    {
        TekrarSikligi.UcAylik => 3,
        TekrarSikligi.AltiAylik => 6,
        TekrarSikligi.Yillik => 12,
        _ => 1
    };

    // Ayın gününe oturtur: ayın 31'i kısa ayda son gün olur, sonraki ayda yine 31'e döner.
    public static DateOnly Gunle(int yil, int ay, int gun) => new(yil, ay, Math.Min(gun, DateTime.DaysInMonth(yil, ay)));

    public static DateOnly Ilk(DateOnly baslangic, TekrarSikligi siklik, int gun)
    {
        var ayinda = Gunle(baslangic.Year, baslangic.Month, gun);
        return ayinda >= baslangic ? ayinda : Sonraki(ayinda, siklik, gun);
    }

    public static DateOnly Sonraki(DateOnly donem, TekrarSikligi siklik, int gun)
    {
        var ay = new DateOnly(donem.Year, donem.Month, 1).AddMonths(AyAdimi(siklik));
        return Gunle(ay.Year, ay.Month, gun);
    }

    // Yılda bir kez, belirlenen aydan itibaren ilk dönemde. Üç aylık planda ay dönem ayına denk gelmese de atlanmaz.
    public static bool ArtisVarMi(DateOnly donem, decimal? oran, int? ay, int sonArtisYili) =>
        oran is > 0 && ay is { } a && donem.Year > sonArtisYili && donem.Month >= a;

    public static string Aciklama(string? sablon, DateOnly donem)
    {
        if (string.IsNullOrWhiteSpace(sablon))
        {
            return "";
        }

        var kultur = CultureInfo.GetCultureInfo("tr-TR");
        return sablon.Replace("{ay}", kultur.DateTimeFormat.GetMonthName(donem.Month), StringComparison.OrdinalIgnoreCase)
            .Replace("{yil}", donem.Year.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
    }
}

// Düzenli kesilen satış faturasının kartı. Satırlar fatura isteği olarak saklanır; her dönem elle kesilen faturayla aynı servisten geçer.
internal sealed class TekrarPlani : FirmaEntity, ISilinebilir, IIsyeriVarligi
{
    private TekrarPlani() { }

    public string Kod { get; private set; } = null!;
    public string Ad { get; private set; } = null!;
    public Guid CariHesapId { get; private set; }
    public TekrarSikligi Siklik { get; private set; }
    public int Gun { get; private set; }
    public DateOnly Baslangic { get; private set; }
    public DateOnly? Bitis { get; private set; }
    public int? VadeGun { get; private set; }
    public string? Aciklama { get; private set; }
    public bool OtomatikKesinlestir { get; private set; }
    public decimal StopajOrani { get; private set; }
    public Guid? MasrafMerkeziId { get; private set; }
    public Guid? IsyeriId { get; private set; }
    public decimal? ArtisOrani { get; private set; }
    public int? ArtisAyi { get; private set; }
    public int SonArtisYili { get; private set; }
    public DateOnly? SonrakiTarih { get; private set; }
    public TekrarDurumu Durum { get; private set; }
    public string? SonHata { get; private set; }
    public DateTime? SonHataTarihi { get; private set; }
    public string SatirlarJson { get; private set; } = "[]";

    [NotMapped]
    public List<FaturaSatiriIstegi> Satirlar
    {
        get => JsonSerializer.Deserialize<List<FaturaSatiriIstegi>>(SatirlarJson) ?? [];
        private set => SatirlarJson = JsonSerializer.Serialize(value);
    }

    public bool SilindiMi { get; private set; }
    public DateTime? SilinmeTarihi { get; private set; }

    public static TekrarPlani Olustur(string kod, TekrarPlaniKaydetIstegi i, Guid isyeriId)
    {
        var plan = new TekrarPlani { Kod = kod, Durum = TekrarDurumu.Aktif, SonArtisYili = i.Baslangic.Year };
        plan.Ayarla(i, isyeriId);
        plan.SonrakiTarih = plan.Sinirla(TekrarTakvimi.Ilk(i.Baslangic, i.Siklik, i.Gun));
        return plan;
    }

    // Üretim başladıktan sonra başlangıç geri alınmaz: kesilmiş dönem yeniden üretilmesin. Sıklık ya da gün değişirse sıradaki tarih yeniden hesaplanır.
    public void Guncelle(TekrarPlaniKaydetIstegi i, Guid isyeriId, DateOnly? sonDonem)
    {
        var takvimDegisti = i.Siklik != Siklik || i.Gun != Gun || i.Baslangic != Baslangic || i.Bitis != Bitis;
        Ayarla(i, isyeriId);
        if (takvimDegisti)
        {
            var ilk = TekrarTakvimi.Ilk(i.Baslangic, i.Siklik, i.Gun);
            var sonraki = sonDonem is { } son ? (ilk > son ? ilk : TekrarTakvimi.Sonraki(son, i.Siklik, i.Gun)) : ilk;
            SonrakiTarih = Sinirla(sonraki);
            Durum = SonrakiTarih is null ? TekrarDurumu.Bitti : Durum == TekrarDurumu.Bitti ? TekrarDurumu.Aktif : Durum;
        }

        (SonHata, SonHataTarihi) = (null, null);
    }

    private void Ayarla(TekrarPlaniKaydetIstegi i, Guid isyeriId)
    {
        Ad = i.Ad.Trim();
        CariHesapId = i.CariHesapId;
        Siklik = i.Siklik;
        Gun = i.Gun;
        Baslangic = i.Baslangic;
        Bitis = i.Bitis;
        VadeGun = i.VadeGun;
        Aciklama = string.IsNullOrWhiteSpace(i.Aciklama) ? null : i.Aciklama.Trim();
        OtomatikKesinlestir = i.OtomatikKesinlestir;
        StopajOrani = i.StopajOrani;
        MasrafMerkeziId = i.MasrafMerkeziId;
        IsyeriId = isyeriId;
        ArtisOrani = i.ArtisOrani;
        ArtisAyi = i.ArtisAyi;
        Satirlar = [.. i.Satirlar];
    }

    private DateOnly? Sinirla(DateOnly tarih)
    {
        if (Bitis is { } bitis && tarih > bitis)
        {
            Durum = TekrarDurumu.Bitti;
            return null;
        }

        return tarih;
    }

    public FaturaKaydetIstegi FaturaIstegi(DateOnly donem) =>
        new(FaturaTuru.Satis, null, CariHesapId, null, donem, VadeGun is { } g ? donem.AddDays(g) : null, null, null, GenelIskontoTuru.Yuzde, 0,
            TekrarTakvimi.Aciklama(Aciklama, donem) is { Length: > 0 } metin ? metin : null, Satirlar, 0, null, StopajOrani, MasrafMerkeziId, IsyeriId);

    // Artış sıradaki faturadan önce satır fiyatlarına kalıcı yazılır; dönem başarısız olursa işlemle birlikte geri alınır.
    public decimal? ArtisUygula(DateOnly donem)
    {
        if (!TekrarTakvimi.ArtisVarMi(donem, ArtisOrani, ArtisAyi, SonArtisYili))
        {
            return null;
        }

        var carpan = 1 + ArtisOrani!.Value / 100;
        Satirlar = Satirlar.Select(s => s with { BirimFiyat = Para.Yuvarla(s.BirimFiyat * carpan) }).ToList();
        SonArtisYili = donem.Year;
        return ArtisOrani;
    }

    public void Uretildi(DateOnly donem)
    {
        (SonHata, SonHataTarihi) = (null, null);
        var sonraki = Sinirla(TekrarTakvimi.Sonraki(donem, Siklik, Gun));
        SonrakiTarih = sonraki;
    }

    public void HataYaz(string hata, DateTime simdi) => (SonHata, SonHataTarihi) = (hata.Length > 500 ? hata[..500] : hata, simdi);

    public Sonuc Duraklat()
    {
        if (Durum != TekrarDurumu.Aktif)
        {
            return TekrarHatalari.Pasif;
        }

        Durum = TekrarDurumu.Duraklatildi;
        return Sonuc.Basarili();
    }

    public Sonuc DevamEt()
    {
        if (Durum != TekrarDurumu.Duraklatildi)
        {
            return TekrarHatalari.Pasif;
        }

        Durum = TekrarDurumu.Aktif;
        return Sonuc.Basarili();
    }
}

// Kesilen dönem. Plan ve dönem benzersizdir: aynı dönem iki kez kesilemez.
internal sealed class TekrarUretimi : FirmaEntity
{
    private TekrarUretimi() { }

    public Guid PlanId { get; private set; }
    public DateOnly Donem { get; private set; }
    public Guid FaturaId { get; private set; }
    public decimal? UygulananArtis { get; private set; }

    public static TekrarUretimi Olustur(Guid planId, DateOnly donem, Guid faturaId, decimal? artis) =>
        new() { PlanId = planId, Donem = donem, FaturaId = faturaId, UygulananArtis = artis };
}
