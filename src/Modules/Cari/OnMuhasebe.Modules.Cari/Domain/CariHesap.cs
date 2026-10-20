using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Shared.Domain.Abstractions;
using OnMuhasebe.Shared.Domain.Entities;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Cari.Domain;

internal sealed class CariHesap : FirmaEntity, ISilinebilir
{
    private CariHesap() { }

    public string Kod { get; private set; } = null!;
    public string Unvan { get; private set; } = null!;
    public CariTuru Tur { get; private set; }
    public KisiTipi KisiTipi { get; private set; }
    public string? VergiNo { get; private set; }
    public string? VergiDairesi { get; private set; }
    public string? Il { get; private set; }
    public string? Ilce { get; private set; }
    public string? Adres { get; private set; }
    public string? Telefon { get; private set; }
    public string? Eposta { get; private set; }
    public string ParaBirimi { get; private set; } = null!;
    public int? VadeGun { get; private set; }
    public decimal? RiskLimiti { get; private set; }
    public Guid? GrupId { get; private set; }
    public string? YetkiliAd { get; private set; }
    public string? YetkiliTelefon { get; private set; }
    public string? Not { get; private set; }
    public bool AktifMi { get; private set; } = true;
    public bool SilindiMi { get; private set; }
    public DateTime? SilinmeTarihi { get; private set; }

    public CariGrup? Grup { get; private set; }

    public static CariHesap Olustur(string kod, CariBilgileri bilgiler)
    {
        var cari = new CariHesap { Kod = kod, ParaBirimi = bilgiler.ParaBirimi };
        cari.BilgileriAyarla(bilgiler);
        return cari;
    }

    // Hareketi olan carinin para birimi değişirse geçmiş tutarlar anlamını yitirir.
    public Sonuc Guncelle(string kod, CariBilgileri bilgiler, bool hareketVarMi)
    {
        if (bilgiler.ParaBirimi != ParaBirimi && hareketVarMi)
        {
            return CariHatalari.ParaBirimiDegistirilemez;
        }

        Kod = kod;
        ParaBirimi = bilgiler.ParaBirimi;
        BilgileriAyarla(bilgiler);
        return Sonuc.Basarili();
    }

    public void Pasiflestir() => AktifMi = false;

    public void Aktiflestir() => AktifMi = true;

    public static Sonuc SilinebilirMi(bool hareketVarMi) =>
        hareketVarMi ? CariHatalari.HareketliCariSilinemez : Sonuc.Basarili();

    private void BilgileriAyarla(CariBilgileri b)
    {
        Unvan = b.Unvan;
        Tur = b.Tur;
        KisiTipi = b.KisiTipi;
        VergiNo = b.VergiNo;
        VergiDairesi = b.VergiDairesi;
        Il = b.Il;
        Ilce = b.Ilce;
        Adres = b.Adres;
        Telefon = b.Telefon;
        Eposta = b.Eposta;
        VadeGun = b.VadeGun;
        RiskLimiti = b.RiskLimiti;
        GrupId = b.GrupId;
        YetkiliAd = b.YetkiliAd;
        YetkiliTelefon = b.YetkiliTelefon;
        Not = b.Not;
    }
}

internal sealed record CariBilgileri(
    string Unvan,
    CariTuru Tur,
    KisiTipi KisiTipi,
    string? VergiNo,
    string? VergiDairesi,
    string? Il,
    string? Ilce,
    string? Adres,
    string? Telefon,
    string? Eposta,
    string ParaBirimi,
    int? VadeGun,
    decimal? RiskLimiti,
    Guid? GrupId,
    string? YetkiliAd,
    string? YetkiliTelefon,
    string? Not)
{
    public static CariBilgileri Istekten(CariKaydetIstegi i) => new(
        i.Unvan.Trim(), i.Tur, i.KisiTipi, Temiz(i.VergiNo), Temiz(i.VergiDairesi), Temiz(i.Il), Temiz(i.Ilce),
        Temiz(i.Adres), Temiz(i.Telefon), Temiz(i.Eposta)?.ToLowerInvariant(), i.ParaBirimi, i.VadeGun, i.RiskLimiti,
        i.GrupId, Temiz(i.YetkiliAd), Temiz(i.YetkiliTelefon), Temiz(i.Not));

    private static string? Temiz(string? deger) => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();
}

internal sealed class CariGrup : FirmaEntity
{
    private CariGrup() { }

    public string Ad { get; private set; } = null!;

    public static CariGrup Olustur(string ad) => new() { Ad = ad.Trim() };

    public void AdDegistir(string ad) => Ad = ad.Trim();
}
