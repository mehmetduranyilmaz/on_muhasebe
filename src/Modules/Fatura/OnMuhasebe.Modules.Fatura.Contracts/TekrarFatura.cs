using System.Text.Json.Serialization;

namespace OnMuhasebe.Modules.Fatura.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<TekrarSikligi>))]
public enum TekrarSikligi { Aylik, UcAylik, AltiAylik, Yillik }

[JsonConverter(typeof(JsonStringEnumConverter<TekrarDurumu>))]
public enum TekrarDurumu { Aktif, Duraklatildi, Bitti }

public static class TekrarMetinleri
{
    public static string Siklik(TekrarSikligi s) => s switch
    {
        TekrarSikligi.UcAylik => "3 ayda bir",
        TekrarSikligi.AltiAylik => "6 ayda bir",
        TekrarSikligi.Yillik => "Yılda bir",
        _ => "Her ay"
    };

    public static string Durum(TekrarDurumu d) => d switch
    {
        TekrarDurumu.Duraklatildi => "Duraklatıldı",
        TekrarDurumu.Bitti => "Bitti",
        _ => "Aktif"
    };
}

// Yalnız satış faturası: alış faturasının numarasını karşı taraf verir. Tutarlar carinin para birimindedir; kur fatura tarihinde bulunur.
// Aciklama içinde {ay} ve {yil} fatura döneminin ayı ve yılıyla değişir. ArtisOrani ve ArtisAyi: her yıl bu aydan itibaren ilk faturada % artış.
public sealed record TekrarPlaniKaydetIstegi(
    string Ad,
    Guid CariHesapId,
    TekrarSikligi Siklik,
    int Gun,
    DateOnly Baslangic,
    DateOnly? Bitis,
    int? VadeGun,
    string? Aciklama,
    bool OtomatikKesinlestir,
    decimal StopajOrani,
    Guid? MasrafMerkeziId,
    Guid? IsyeriId,
    decimal? ArtisOrani,
    int? ArtisAyi,
    IReadOnlyList<FaturaSatiriIstegi> Satirlar,
    uint Surum = 0);

public sealed record TekrarPlaniOzeti(
    Guid Id,
    string Kod,
    string Ad,
    Guid CariHesapId,
    string CariUnvan,
    TekrarSikligi Siklik,
    DateOnly? SonrakiTarih,
    TekrarDurumu Durum,
    bool OtomatikKesinlestir,
    string? SonHata,
    Guid? IsyeriId);

public sealed record TekrarUretimCevabi(DateOnly Donem, Guid FaturaId, string? FaturaNo, decimal? UygulananArtis, DateTime OlusturmaTarihi);

public sealed record TekrarPlaniDetayi(
    Guid Id,
    string Kod,
    string Ad,
    Guid CariHesapId,
    string CariUnvan,
    string? CariKodu,
    string ParaBirimi,
    TekrarSikligi Siklik,
    int Gun,
    DateOnly Baslangic,
    DateOnly? Bitis,
    int? VadeGun,
    string? Aciklama,
    bool OtomatikKesinlestir,
    decimal StopajOrani,
    Guid? MasrafMerkeziId,
    Guid? IsyeriId,
    decimal? ArtisOrani,
    int? ArtisAyi,
    DateOnly? SonrakiTarih,
    TekrarDurumu Durum,
    string? SonHata,
    DateTime? SonHataTarihi,
    IReadOnlyList<FaturaSatiriIstegi> Satirlar,
    IReadOnlyList<TekrarUretimCevabi> Uretimler,
    uint Surum);

// Uretilen: bu çağrıda kesilen dönem sayısı. Hata: plan bir dönemde durduysa nedeni.
public sealed record TekrarUretSonucu(int Uretilen, string? Hata, TekrarPlaniDetayi Plan);
