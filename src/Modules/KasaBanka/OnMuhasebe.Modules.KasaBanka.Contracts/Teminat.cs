using System.Text.Json.Serialization;

namespace OnMuhasebe.Modules.KasaBanka.Contracts;

// Teminat türü. Türüne göre ek alanlar (ipotekte ada/pafta/parsel, araç rehninde plaka/marka/model, mektupta komisyon...) TeminatSemasi'nda tanımlıdır.
[JsonConverter(typeof(JsonStringEnumConverter<TeminatTuru>))]
public enum TeminatTuru
{
    TeminatMektubu,
    Ipotek,
    Rehin,
    Kefalet,
    NakitTeminat,
    TeminatCeki,
    TeminatSenedi,
    Diger
}

// Alinan: lehimize alınan teminat; Verilen: karşı tarafa verdiğimiz teminat.
[JsonConverter(typeof(JsonStringEnumConverter<TeminatYonu>))]
public enum TeminatYonu
{
    Alinan,
    Verilen
}

// Aktif: yürürlükte; IadeEdildi: serbest bırakıldı/geri alındı; Irat: teminat nakde çevrildi/irat kaydedildi.
[JsonConverter(typeof(JsonStringEnumConverter<TeminatDurumu>))]
public enum TeminatDurumu
{
    Aktif,
    IadeEdildi,
    Irat
}

// Teminat mektubu komisyon tahsil dönemi. Gerçek hayatta genelde peşin ya da 3 aylık dönemlerde tahsil edilir.
[JsonConverter(typeof(JsonStringEnumConverter<KomisyonDonemi>))]
public enum KomisyonDonemi
{
    Pesin,
    Aylik,
    UcAylik,
    AltiAylik,
    Yillik
}

public enum TeminatAlanTipi
{
    Metin,
    Sayi
}

// Türe özgü bir detay alanının tanımı (kod = Detaylar sözlüğünün anahtarı).
public sealed record TeminatDetayAlani(string Kod, string Etiket, TeminatAlanTipi Tipi = TeminatAlanTipi.Metin);

// Türe göre hangi ek alanların gösterileceği; UI ve doğrulama bunu kullanır. Değerler Teminat.Detaylar sözlüğünde tutulur.
public static class TeminatSemasi
{
    public static IReadOnlyList<TeminatDetayAlani> Alanlar(TeminatTuru tur) => tur switch
    {
        TeminatTuru.TeminatMektubu => [new("MektupTuru", "Mektup türü (geçici/kesin/avans)")],
        TeminatTuru.Ipotek =>
        [
            new("TasinmazTuru", "Taşınmaz türü (arsa/arazi/konut/işyeri)"),
            new("Il", "İl"), new("Ilce", "İlçe"),
            new("Ada", "Ada"), new("Pafta", "Pafta"), new("Parsel", "Parsel"),
            new("BagimsizBolumNo", "Bağımsız bölüm no"),
            new("IpotekDerecesi", "İpotek derecesi", TeminatAlanTipi.Sayi)
        ],
        TeminatTuru.Rehin =>
        [
            new("RehinTuru", "Rehin türü (araç/makine/menkul)"),
            new("Plaka", "Plaka"), new("Marka", "Marka"), new("Model", "Model"),
            new("ModelYili", "Model yılı", TeminatAlanTipi.Sayi), new("SasiNo", "Şasi / seri no")
        ],
        TeminatTuru.Kefalet =>
        [
            new("KefilAdi", "Kefil adı/unvanı"), new("KefilKimlikNo", "Kefil VKN/TCKN"),
            new("KefaletTuru", "Kefalet türü (müteselsil/adi)")
        ],
        TeminatTuru.NakitTeminat => [new("BlokeHesap", "Bloke hesap / dekont")],
        _ => []
    };

    // Mektup, çek ve nakit teminat bir bankayla ilişkilidir.
    public static bool Bankali(TeminatTuru tur) => tur is TeminatTuru.TeminatMektubu or TeminatTuru.TeminatCeki or TeminatTuru.NakitTeminat;

    // Komisyon yalnızca teminat mektubunda vardır.
    public static bool Komisyonlu(TeminatTuru tur) => tur is TeminatTuru.TeminatMektubu;

    public static string ReferansEtiketi(TeminatTuru tur) => tur switch
    {
        TeminatTuru.TeminatMektubu => "Mektup no",
        TeminatTuru.Ipotek => "Tapu yevmiye no",
        TeminatTuru.Rehin => "Sözleşme / seri no",
        TeminatTuru.Kefalet => "Sözleşme no",
        TeminatTuru.NakitTeminat => "Dekont no",
        TeminatTuru.TeminatCeki => "Çek no",
        TeminatTuru.TeminatSenedi => "Senet no",
        _ => "Referans no"
    };
}

public sealed record TeminatOzeti(
    Guid Id,
    TeminatTuru Tur,
    TeminatYonu Yon,
    Guid CariHesapId,
    string? CariUnvan,
    string? BankaAdi,
    string? ReferansNo,
    decimal Tutar,
    string ParaBirimi,
    DateOnly VerilisTarihi,
    DateOnly? VadeTarihi,
    TeminatDurumu Durum);

public sealed record TeminatDetayi(
    Guid Id,
    TeminatTuru Tur,
    TeminatYonu Yon,
    Guid CariHesapId,
    string? CariUnvan,
    string? BankaAdi,
    string? ReferansNo,
    decimal Tutar,
    string ParaBirimi,
    DateOnly VerilisTarihi,
    DateOnly? VadeTarihi,
    DateOnly? IadeTarihi,
    TeminatDurumu Durum,
    decimal? KomisyonOrani,
    decimal? KomisyonTutari,
    KomisyonDonemi? KomisyonDonemi,
    IReadOnlyDictionary<string, string> Detaylar,
    string? Aciklama,
    uint Surum);

public sealed record TeminatKaydetIstegi(
    TeminatTuru Tur,
    TeminatYonu Yon,
    Guid CariHesapId,
    string? BankaAdi,
    string? ReferansNo,
    decimal Tutar,
    string ParaBirimi,
    DateOnly VerilisTarihi,
    DateOnly? VadeTarihi,
    decimal? KomisyonOrani,
    decimal? KomisyonTutari,
    KomisyonDonemi? KomisyonDonemi,
    IReadOnlyDictionary<string, string>? Detaylar,
    string? Aciklama,
    uint Surum = 0);

public sealed record TeminatDurumIstegi(TeminatDurumu Durum, DateOnly? IadeTarihi);
