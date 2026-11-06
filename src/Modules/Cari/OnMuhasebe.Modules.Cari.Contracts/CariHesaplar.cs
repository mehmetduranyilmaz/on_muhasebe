using System.Text.Json.Serialization;

namespace OnMuhasebe.Modules.Cari.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<CariTuru>))]
public enum CariTuru
{
    Musteri,
    Tedarikci,
    MusteriVeTedarikci
}

[JsonConverter(typeof(JsonStringEnumConverter<KisiTipi>))]
public enum KisiTipi
{
    Tuzel,
    Sahis
}

// Kod boş bırakılırsa otomatik verilir. Surum yalnızca güncellemede gönderilir (eşzamanlı düzenleme kontrolü).
// Şahıs caride Ad/Soyad boşsa ünvandan türetilir (son kelime soyad). Liste alanları null gelirse güncellemede mevcut kayıtlar
// korunur, boş liste gelirse silinir. Açılış bakiyesi yalnızca yeni kartta dikkate alınır.
public sealed record CariKaydetIstegi(
    string? Kod,
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
    string? Not,
    bool FarkliDovizdeIslem = false,
    uint Surum = 0,
    decimal? VadeFarkiOrani = null,
    string? Ad = null,
    string? Soyad = null,
    string? Ulke = null,
    string? PostaKodu = null,
    string? CepTelefonu = null,
    string? Faks = null,
    string? WebSitesi = null,
    bool EFaturaMukellefiMi = false,
    string? EFaturaEtiketi = null,
    string? VarsayilanKdvIstisnaKodu = null,
    string? VarsayilanTevkifatKodu = null,
    IReadOnlyList<CariAdresBilgisi>? Adresler = null,
    IReadOnlyList<CariIlgiliKisiBilgisi>? IlgiliKisiler = null,
    IReadOnlyList<CariBankaHesabiBilgisi>? BankaHesaplari = null,
    CariAcilisBakiyesi? AcilisBakiyesi = null);

// Fatura adresi kartın kendisindedir; bunlar teslimat/şube adresleridir.
public sealed record CariAdresBilgisi(
    string Baslik,
    string? AdSoyad,
    string? Telefon,
    string? Ulke,
    string? Il,
    string? Ilce,
    string Adres,
    string? PostaKodu);

public sealed record CariIlgiliKisiBilgisi(string AdSoyad, string? Gorev, string? Telefon, string? Eposta);

public sealed record CariBankaHesabiBilgisi(string BankaAdi, string? Sube, string? HesapNo, string? Iban, string ParaBirimi);

// Kur yalnızca dövizli caride gerekir.
public sealed record CariAcilisBakiyesi(HareketYonu Yon, decimal Tutar, DateOnly Tarih, decimal? Kur);

public sealed record CariOzeti(
    Guid Id,
    string Kod,
    string Unvan,
    CariTuru Tur,
    string? GrupAdi,
    string ParaBirimi,
    string? Telefon,
    bool AktifMi,
    decimal Bakiye,
    decimal BakiyeTl,
    decimal? RiskLimiti)
{
    public bool RiskAsildiMi => RiskLimiti is { } limit && Bakiye > limit;
}

public sealed record CariDetayi(
    Guid Id,
    string Kod,
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
    string? GrupAdi,
    string? Not,
    bool AktifMi,
    bool FarkliDovizdeIslem,
    decimal Bakiye,
    decimal BakiyeTl,
    bool HareketVarMi,
    uint Surum,
    decimal? VadeFarkiOrani,
    string? Ad,
    string? Soyad,
    string Ulke,
    string? PostaKodu,
    string? CepTelefonu,
    string? Faks,
    string? WebSitesi,
    bool EFaturaMukellefiMi,
    string? EFaturaEtiketi,
    string? VarsayilanKdvIstisnaKodu,
    string? VarsayilanTevkifatKodu,
    IReadOnlyList<CariAdresBilgisi> Adresler,
    IReadOnlyList<CariIlgiliKisiBilgisi> IlgiliKisiler,
    IReadOnlyList<CariBankaHesabiBilgisi> BankaHesaplari)
{
    // Arama/WhatsApp için ulaşılabilecek ilk numara: cep, sabit, sonra ilgili kişiler.
    public string? UlasimTelefonu => CepTelefonu ?? Telefon ?? IlgiliKisiler.Select(k => k.Telefon).FirstOrDefault(t => t is not null);
}

public static class CariVarsayilanlari
{
    public const string Ulke = "Türkiye";
}
