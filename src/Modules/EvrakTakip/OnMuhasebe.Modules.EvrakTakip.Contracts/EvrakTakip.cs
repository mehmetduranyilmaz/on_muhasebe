using System.Text.Json.Serialization;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Moduller;

namespace OnMuhasebe.Modules.EvrakTakip.Contracts;

public static class EvrakTakipYetkileri
{
    public static readonly YetkiKaynagi Kayit = new("evraktakip.kayit", ModulKodlari.EvrakTakip, "Evrak Takibi", "Süreli evraklar",
        YetkiIslemleri.Temel, []);

    public static IReadOnlyList<YetkiKaynagi> Tumu { get; } = [Kayit];
}

[JsonConverter(typeof(JsonStringEnumConverter<TakipEvrakTuru>))]
public enum TakipEvrakTuru
{
    Ruhsat,
    Sigorta,
    VergiLevhasi,
    ImzaSirkuleri,
    Sozlesme,
    Sertifika,
    Izin,
    Diger
}

// Pasif: takibi bırakılmış evrak (sözleşme bitti, araç satıldı); hatırlatılmaz.
[JsonConverter(typeof(JsonStringEnumConverter<TakipEvrakDurumu>))]
public enum TakipEvrakDurumu
{
    Gecerli,
    Yaklasiyor,
    SuresiDoldu,
    Pasif
}

public static class TakipEvrakTurleri
{
    public static string Ad(TakipEvrakTuru tur) => tur switch
    {
        TakipEvrakTuru.Ruhsat => "Ruhsat",
        TakipEvrakTuru.Sigorta => "Sigorta poliçesi",
        TakipEvrakTuru.VergiLevhasi => "Vergi levhası",
        TakipEvrakTuru.ImzaSirkuleri => "İmza sirküleri",
        TakipEvrakTuru.Sozlesme => "Sözleşme",
        TakipEvrakTuru.Sertifika => "Sertifika / belge",
        TakipEvrakTuru.Izin => "İzin / lisans",
        _ => "Diğer"
    };

    public static string DurumAdi(TakipEvrakDurumu durum) => durum switch
    {
        TakipEvrakDurumu.Yaklasiyor => "Süresi yaklaşıyor",
        TakipEvrakDurumu.SuresiDoldu => "Süresi doldu",
        TakipEvrakDurumu.Pasif => "Pasif",
        _ => "Geçerli"
    };

    public static TakipEvrakDurumu Durum(bool aktifMi, DateOnly bitis, int hatirlatmaGun, DateOnly bugun) =>
        !aktifMi ? TakipEvrakDurumu.Pasif
        : bitis < bugun ? TakipEvrakDurumu.SuresiDoldu
        : bitis.DayNumber - bugun.DayNumber <= hatirlatmaGun ? TakipEvrakDurumu.Yaklasiyor
        : TakipEvrakDurumu.Gecerli;
}

// IsyeriId boşsa firma genelidir. SorumluKullaniciId'ye hatırlatma, bildirim tercihinden bağımsız gider.
public sealed record TakipEvrakiKaydetIstegi(
    string Ad, TakipEvrakTuru Tur, string? BelgeNo, string? Kurum, DateOnly? BaslangicTarihi, DateOnly BitisTarihi, int HatirlatmaGun,
    Guid? SorumluKullaniciId, Guid? IsyeriId, string? Aciklama, uint Surum = 0);

public sealed record TakipEvrakiOzeti(
    Guid Id, string Ad, TakipEvrakTuru Tur, string? BelgeNo, string? Kurum, DateOnly BitisTarihi, int KalanGun, TakipEvrakDurumu Durum,
    string? SorumluAdi, Guid? IsyeriId);

public sealed record TakipYenilemesi(DateOnly EskiBitisTarihi, DateOnly YeniBitisTarihi, string? EskiBelgeNo, string? Not, string? Yapan, DateTime Tarih);

public sealed record TakipEvrakiDetayi(
    Guid Id, string Ad, TakipEvrakTuru Tur, string? BelgeNo, string? Kurum, DateOnly? BaslangicTarihi, DateOnly BitisTarihi, int HatirlatmaGun,
    Guid? SorumluKullaniciId, Guid? IsyeriId, string? Aciklama, bool AktifMi, TakipEvrakDurumu Durum, uint Surum, IReadOnlyList<TakipYenilemesi> Yenilemeler);

// Evrak yenilenince eski bitiş tarihi ve belge no geçmişe yazılır; YeniBelgeNo boşsa belge no değişmez.
public sealed record TakipYenilemeIstegi(DateOnly YeniBitisTarihi, DateOnly? YeniBaslangicTarihi, string? YeniBelgeNo, string? Not);

public sealed record TakipEvrakSayilari(int Gecerli, int Yaklasiyor, int SuresiDoldu, int Pasif);

public sealed record TakipSorumlusu(Guid Id, string AdSoyad);
