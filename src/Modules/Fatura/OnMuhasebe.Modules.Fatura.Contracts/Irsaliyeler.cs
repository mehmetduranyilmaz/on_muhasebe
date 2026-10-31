using System.Text.Json.Serialization;

namespace OnMuhasebe.Modules.Fatura.Contracts;

// İrsaliye malın sevkini belgeler (VUK 230): stoğu etkiler, cariyi etkilemez; cari, irsaliye faturalanınca oluşur.
[JsonConverter(typeof(JsonStringEnumConverter<IrsaliyeTuru>))]
public enum IrsaliyeTuru
{
    Satis,
    Alis,
    SatistanIade,
    AlistanIade,
    // Firmanın farklı adresteki depoları arasında mal taşınırken düzenlenir; carisi yoktur, faturalanmaz.
    DepoSevk
}

[JsonConverter(typeof(JsonStringEnumConverter<IrsaliyeDurumu>))]
public enum IrsaliyeDurumu
{
    Taslak,
    Onayli,
    Iptal
}

[JsonConverter(typeof(JsonStringEnumConverter<FaturalamaDurumu>))]
public enum FaturalamaDurumu
{
    Faturalanmadi,
    KismenFaturalandi,
    Faturalandi
}

public static class IrsaliyeTurleri
{
    // Numarasını firmanın verdiği irsaliyeler; alış ve satıştan iadede karşı tarafın irsaliye numarası girilir.
    public static bool GidenMi(IrsaliyeTuru tur) => tur is IrsaliyeTuru.Satis or IrsaliyeTuru.AlistanIade or IrsaliyeTuru.DepoSevk;

    public static bool FaturalanirMi(IrsaliyeTuru tur) => tur != IrsaliyeTuru.DepoSevk;

    public static bool StokGirisiMi(IrsaliyeTuru tur) => tur is IrsaliyeTuru.Alis or IrsaliyeTuru.SatistanIade;

    public static FaturaTuru? FaturaTuru(IrsaliyeTuru tur) => tur switch
    {
        IrsaliyeTuru.Satis => Contracts.FaturaTuru.Satis,
        IrsaliyeTuru.Alis => Contracts.FaturaTuru.Alis,
        IrsaliyeTuru.SatistanIade => Contracts.FaturaTuru.SatistanIade,
        IrsaliyeTuru.AlistanIade => Contracts.FaturaTuru.AlistanIade,
        _ => null
    };

    public static IrsaliyeTuru? Tur(FaturaTuru tur) => tur switch
    {
        Contracts.FaturaTuru.Satis => IrsaliyeTuru.Satis,
        Contracts.FaturaTuru.Alis => IrsaliyeTuru.Alis,
        Contracts.FaturaTuru.SatistanIade => IrsaliyeTuru.SatistanIade,
        Contracts.FaturaTuru.AlistanIade => IrsaliyeTuru.AlistanIade,
        _ => null
    };
}

// Satır yalnızca ürün kartıdır (hizmet sevk edilmez). Fiyat opsiyoneldir (fiyatsız irsaliye yaygındır), KDV hariç ve
// irsaliyenin dövizindedir; alış irsaliyesinde stoğa giriş maliyeti olduğu için zorunludur (fatura gelince güncellenir).
// Depo boşsa varsayılan depo kullanılır; depo sevkinde bu, malın çıktığı depodur.
public sealed record IrsaliyeSatiriIstegi(
    Guid StokKartiId,
    string? Ad,
    Guid? DepoId,
    string Birim,
    decimal Miktar,
    decimal? BirimFiyat);

// Sevk saati e-İrsaliye'de zorunludur; kâğıt irsaliyede de fiili sevk anı olarak tutulur.
// Araç/şoför/taşıyıcı alanları kâğıt irsaliyede opsiyonel, e-İrsaliye'de (S5) zorunlu olacaktır.
public sealed record IrsaliyeKaydetIstegi(
    IrsaliyeTuru Tur,
    Guid? CariHesapId,
    DateOnly Tarih,
    DateOnly SevkTarihi,
    TimeOnly SevkSaati,
    string? Numara,
    Guid? HedefDepoId,
    string? TeslimAdresi,
    string? AracPlakasi,
    string? SoforAdi,
    string? SoforTckn,
    string? TasiyiciUnvani,
    string? TasiyiciVkn,
    string? Aciklama,
    IReadOnlyList<IrsaliyeSatiriIstegi> Satirlar,
    uint Surum = 0);

public sealed record IrsaliyeOzeti(
    Guid Id,
    IrsaliyeTuru Tur,
    IrsaliyeDurumu Durum,
    FaturalamaDurumu? FaturalamaDurumu,
    string? Numara,
    DateOnly Tarih,
    DateOnly SevkTarihi,
    Guid? CariHesapId,
    string CariUnvan,
    int SatirSayisi,
    DateOnly? FaturaSonGunu);

public sealed record IrsaliyeSatiriDetayi(
    Guid Id,
    int Sira,
    Guid StokKartiId,
    string StokKodu,
    string Ad,
    Guid DepoId,
    string Birim,
    decimal Miktar,
    decimal? BirimFiyat,
    int KdvOrani,
    decimal FaturalananMiktar,
    decimal KalanMiktar);

public sealed record IrsaliyeDetayi(
    Guid Id,
    IrsaliyeTuru Tur,
    IrsaliyeDurumu Durum,
    FaturalamaDurumu? FaturalamaDurumu,
    string? Numara,
    DateOnly Tarih,
    DateOnly SevkTarihi,
    TimeOnly SevkSaati,
    Guid? CariHesapId,
    string? CariKodu,
    string CariUnvan,
    string? VergiNo,
    string? VergiDairesi,
    string? Il,
    string? Ilce,
    string? Adres,
    string ParaBirimi,
    Guid? HedefDepoId,
    string? TeslimAdresi,
    string? AracPlakasi,
    string? SoforAdi,
    string? SoforTckn,
    string? TasiyiciUnvani,
    string? TasiyiciVkn,
    string? Aciklama,
    IReadOnlyList<IrsaliyeSatiriDetayi> Satirlar,
    IReadOnlyList<BagliBelge> Faturalar,
    DateOnly? FaturaSonGunu,
    DateTime? OnayTarihi,
    DateTime? IptalTarihi,
    uint Surum);

// İrsaliye ile fatura arasındaki bağ (iki yönde de gösterilir).
public sealed record BagliBelge(Guid Id, string? Numara, DateOnly Tarih);

public sealed record IrsaliyeCevabi(IrsaliyeDetayi Irsaliye, string? Uyari);

// VUK 231/5: fatura, malın teslim (sevk) tarihinden itibaren en geç 7 gün içinde düzenlenir. Süre aşılırsa fatura
// hiç düzenlenmemiş sayılır (özel usulsüzlük) ama yine de kesilmek zorundadır; bu yüzden engellenmez, uyarılır.
public static class IrsaliyeHesaplari
{
    public const int FaturaSuresiGun = 7;
    public const int MaliyetOndalik = 4;

    public static DateOnly FaturaSonGunu(DateOnly sevkTarihi) => sevkTarihi.AddDays(FaturaSuresiGun);

    public static bool FaturaSuresiGectiMi(DateOnly sevkTarihi, DateOnly faturaTarihi) => faturaTarihi > FaturaSonGunu(sevkTarihi);

    public static decimal Kalan(decimal miktar, decimal faturalanan) => Math.Max(0m, miktar - faturalanan);

    public static FaturalamaDurumu Durum(IEnumerable<(decimal Miktar, decimal Faturalanan)> satirlar)
    {
        var liste = satirlar.ToList();
        if (liste.All(s => s.Faturalanan <= 0m))
        {
            return FaturalamaDurumu.Faturalanmadi;
        }

        return liste.All(s => s.Faturalanan >= s.Miktar) ? FaturalamaDurumu.Faturalandi : FaturalamaDurumu.KismenFaturalandi;
    }

    // Alış irsaliyesi satırının stoktaki birim maliyeti: faturalanan kısımlar fatura maliyetiyle, kalanı irsaliyedeki
    // tahmini maliyetle ağırlıklandırılır. Böylece lotun toplam değeri, gelen faturaların gerçek tutarına eşit olur.
    public static decimal AgirlikliMaliyet(decimal miktar, decimal tahminiBirimMaliyet, IEnumerable<(decimal Miktar, decimal BirimMaliyet)> faturalar)
    {
        if (miktar <= 0m)
        {
            return tahminiBirimMaliyet;
        }

        var parcalar = faturalar.ToList();
        var faturalanan = Math.Min(miktar, parcalar.Sum(p => p.Miktar));
        var toplam = parcalar.Sum(p => p.Miktar * p.BirimMaliyet) + (miktar - faturalanan) * tahminiBirimMaliyet;
        return Math.Round(toplam / miktar, MaliyetOndalik, MidpointRounding.AwayFromZero);
    }
}
