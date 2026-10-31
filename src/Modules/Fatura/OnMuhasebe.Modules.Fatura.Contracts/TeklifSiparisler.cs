using System.Text.Json.Serialization;

namespace OnMuhasebe.Modules.Fatura.Contracts;

// Teklif ve sipariş ticari taahhüttür: stoğu ve cariyi etkilemez. Stok irsaliyeyle, cari faturayla oluşur.
[JsonConverter(typeof(JsonStringEnumConverter<TeklifSiparisTuru>))]
public enum TeklifSiparisTuru
{
    SatisTeklifi,
    SatisSiparisi,
    SatinalmaSiparisi
}

// Teklif: Taslak → Acik (müşteriye verildi) → Kabul / Red. Sipariş: Taslak → Acik → (karşılanma satırlardan) → Kapatildi.
[JsonConverter(typeof(JsonStringEnumConverter<TeklifSiparisDurumu>))]
public enum TeklifSiparisDurumu
{
    Taslak,
    Acik,
    Kabul,
    Red,
    Kapatildi,
    Iptal
}

[JsonConverter(typeof(JsonStringEnumConverter<KarsilamaDurumu>))]
public enum KarsilamaDurumu
{
    Bekliyor,
    Kismen,
    Tamamlandi
}

public static class TeklifSiparisTurleri
{
    public static bool TeklifMi(TeklifSiparisTuru tur) => tur == TeklifSiparisTuru.SatisTeklifi;

    public static bool SatisMi(TeklifSiparisTuru tur) => tur != TeklifSiparisTuru.SatinalmaSiparisi;

    // Siparişin karşılandığı irsaliye/fatura türü.
    public static FaturaTuru FaturaTuru(TeklifSiparisTuru tur) => SatisMi(tur) ? Contracts.FaturaTuru.Satis : Contracts.FaturaTuru.Alis;

    public static IrsaliyeTuru IrsaliyeTuru(TeklifSiparisTuru tur) => SatisMi(tur) ? Contracts.IrsaliyeTuru.Satis : Contracts.IrsaliyeTuru.Alis;

    public static TeklifSiparisTuru? SiparisTuru(FaturaTuru tur) => tur switch
    {
        Contracts.FaturaTuru.Satis => TeklifSiparisTuru.SatisSiparisi,
        Contracts.FaturaTuru.Alis => TeklifSiparisTuru.SatinalmaSiparisi,
        _ => null
    };

    // Geçerlilik süresi dolmuş, yanıtlanmamış teklif.
    public static bool SuresiDolduMu(TeklifSiparisTuru tur, TeklifSiparisDurumu durum, DateOnly? gecerlilik, DateOnly bugun) =>
        TeklifMi(tur) && durum == TeklifSiparisDurumu.Acik && gecerlilik < bugun;
}

// Satırlar fatura satırıyla aynı hesaplanır (iskonto, KDV dahil/hariç, döviz). Cari boşsa (yalnızca teklifte) AdayUnvan zorunludur.
public sealed record TeklifSiparisKaydetIstegi(
    TeklifSiparisTuru Tur,
    Guid? CariHesapId,
    string? AdayUnvan,
    DateOnly Tarih,
    DateOnly? GecerlilikTarihi,
    DateOnly? TerminTarihi,
    string? ParaBirimi,
    decimal? Kur,
    GenelIskontoTuru GenelIskontoTuru,
    decimal GenelIskonto,
    string? OdemeKosulu,
    string? TeslimKosulu,
    string? Aciklama,
    IReadOnlyList<FaturaSatiriIstegi> Satirlar,
    uint Surum = 0);

public sealed record TeklifSiparisOzeti(
    Guid Id,
    TeklifSiparisTuru Tur,
    TeklifSiparisDurumu Durum,
    KarsilamaDurumu? Karsilama,
    string? Numara,
    DateOnly Tarih,
    DateOnly? GecerlilikTarihi,
    DateOnly? TerminTarihi,
    Guid? CariHesapId,
    string CariUnvan,
    string ParaBirimi,
    decimal GenelToplam,
    bool SuresiDolduMu);

public sealed record TeklifSiparisSatiriDetayi(
    Guid Id,
    int Sira,
    Guid? StokKartiId,
    string? StokKodu,
    string Ad,
    bool StokluMu,
    Guid? DepoId,
    string Birim,
    decimal Miktar,
    decimal BirimFiyat,
    bool KdvDahilMi,
    int KdvOrani,
    decimal Iskonto1,
    decimal Iskonto2,
    decimal Iskonto3,
    decimal Matrah,
    decimal KdvTutari,
    decimal Toplam,
    decimal KarsilananMiktar,
    decimal KalanMiktar);

public sealed record TeklifSiparisDetayi(
    Guid Id,
    TeklifSiparisTuru Tur,
    TeklifSiparisDurumu Durum,
    KarsilamaDurumu? Karsilama,
    string? Numara,
    DateOnly Tarih,
    DateOnly? GecerlilikTarihi,
    DateOnly? TerminTarihi,
    Guid? CariHesapId,
    string? CariKodu,
    string CariUnvan,
    string? VergiNo,
    string? VergiDairesi,
    string? Il,
    string? Ilce,
    string? Adres,
    string ParaBirimi,
    decimal Kur,
    GenelIskontoTuru GenelIskontoTuru,
    decimal GenelIskonto,
    string? OdemeKosulu,
    string? TeslimKosulu,
    string? Aciklama,
    decimal BrutToplam,
    decimal SatirIskontoToplami,
    decimal GenelIskontoTutari,
    decimal Matrah,
    decimal KdvToplami,
    decimal GenelToplam,
    IReadOnlyList<KdvDagilimi> KdvDagilimi,
    IReadOnlyList<TeklifSiparisSatiriDetayi> Satirlar,
    Guid? KaynakTeklifId,
    IReadOnlyList<BagliBelge> Siparisler,
    bool SuresiDolduMu,
    DateTime? OnayTarihi,
    uint Surum);

// Teklif yanıtı: kabul ya da red (red nedeni açıklamaya eklenir).
public sealed record TeklifYanitiIstegi(bool KabulMu, string? Neden);

public static class TeklifSiparisHesaplari
{
    public static KarsilamaDurumu Karsilama(IEnumerable<(decimal Miktar, decimal Karsilanan)> satirlar)
    {
        var liste = satirlar.ToList();
        if (liste.All(s => s.Karsilanan <= 0m))
        {
            return KarsilamaDurumu.Bekliyor;
        }

        return liste.All(s => s.Karsilanan >= s.Miktar) ? KarsilamaDurumu.Tamamlandi : KarsilamaDurumu.Kismen;
    }
}
