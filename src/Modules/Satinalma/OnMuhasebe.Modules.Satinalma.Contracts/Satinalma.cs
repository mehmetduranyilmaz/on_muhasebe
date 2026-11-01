using System.Text.Json.Serialization;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Moduller;

namespace OnMuhasebe.Modules.Satinalma.Contracts;

public static class SatinalmaYetkileri
{
    private const string Baslik = "Satın Alma";

    public static readonly YetkiKaynagi Talep = new("satinalma.talep", ModulKodlari.Satinalma, Baslik, "Satın alma talepleri",
        [YetkiIslemleri.Goruntule, YetkiIslemleri.Ekle, YetkiIslemleri.Duzenle, YetkiIslemleri.Sil, YetkiIslemleri.Iptal], []);

    public static readonly YetkiKaynagi Onay = new("satinalma.onay", ModulKodlari.Satinalma, Baslik, "Talep onaylama",
        [YetkiIslemleri.Goruntule, YetkiIslemleri.Onayla], ["satinalma.talep.goruntule"]);

    public static readonly YetkiKaynagi Kural = new("satinalma.kural", ModulKodlari.Satinalma, Baslik, "Onay kuralları",
        [YetkiIslemleri.Goruntule, YetkiIslemleri.Duzenle], []);

    public static IReadOnlyList<YetkiKaynagi> Tumu { get; } = [Talep, Onay, Kural];
}

[JsonConverter(typeof(JsonStringEnumConverter<TalepDurumu>))]
public enum TalepDurumu { Taslak, Onayda, Onaylandi, Reddedildi, SipariseDonustu, Iptal }

[JsonConverter(typeof(JsonStringEnumConverter<OnayDurumu>))]
public enum OnayDurumu { Bekliyor, Onaylandi, Reddedildi }

public static class SatinalmaMetinleri
{
    public static string Durum(TalepDurumu d) => d switch
    {
        TalepDurumu.Onayda => "Onayda",
        TalepDurumu.Onaylandi => "Onaylandı",
        TalepDurumu.Reddedildi => "Reddedildi",
        TalepDurumu.SipariseDonustu => "Siparişe dönüştü",
        TalepDurumu.Iptal => "İptal",
        _ => "Taslak"
    };

    public static string Onay(OnayDurumu d) => d switch
    {
        OnayDurumu.Onaylandi => "Onayladı",
        OnayDurumu.Reddedildi => "Reddetti",
        _ => "Bekliyor"
    };
}

// Stok kartı verilmezse Ad zorunludur (serbest kalem). Birim boşsa kartın temel birimi. Tahmini fiyat KDV hariç TL.
public sealed record TalepKalemiIstegi(Guid? StokKartiId, string? Ad, string? Birim, decimal Miktar, decimal TahminiBirimFiyat, int KdvOrani);

public sealed record TalepKalemiCevabi(Guid? StokKartiId, string Ad, string Birim, decimal Miktar, decimal TahminiBirimFiyat, int KdvOrani, decimal Tutar);

public sealed record TalepKaydetIstegi(
    DateOnly Tarih, DateOnly? GerekliTarih, string Gerekce, Guid? IsyeriId, Guid? MasrafMerkeziId, IReadOnlyList<TalepKalemiIstegi> Kalemler, uint Surum = 0);

public sealed record TalepOnayCevabi(int GonderimNo, int Sira, string Onaylayan, OnayDurumu Durum, string? KararVeren, DateTime? KararTarihi, string? Not);

// SiparisUyarisi: sipariş tutarı onaylanan tutarı aşıyorsa.
public sealed record TalepDetayi(
    Guid Id, string Kod, Guid TalepEdenId, string TalepEden, DateOnly Tarih, DateOnly? GerekliTarih, string Gerekce, Guid? IsyeriId, Guid? MasrafMerkeziId, TalepDurumu Durum, decimal Toplam,
    IReadOnlyList<TalepKalemiCevabi> Kalemler, IReadOnlyList<TalepOnayCevabi> Onaylar, bool OnaylayabilirMi, bool SipariseDonusebilirMi, Guid? SiparisId, string? SiparisNo, decimal? SiparisTutari, string? SiparisUyarisi, uint Surum);

public sealed record TalepOzeti(
    Guid Id, string Kod, string TalepEden, DateOnly Tarih, DateOnly? GerekliTarih, string Gerekce, TalepDurumu Durum, decimal Toplam, string? BekleyenOnay, Guid? IsyeriId);

public sealed record TalepKararIstegi(string? Not);

public sealed record SipariseDonusturIstegi(Guid TedarikciId, DateOnly? TerminTarihi);

// Toplam tutar EnAzTutar ve üstündeyse bu basamak uygulanır; basamaklar Sira ile sırayla ilerler. Rol ya da kişiden yalnız biri verilir.
public sealed record OnayBasamagiIstegi(decimal EnAzTutar, Guid? RolId, Guid? KullaniciId);

public sealed record OnayKuraliIstegi(IReadOnlyList<OnayBasamagiIstegi> Basamaklar);

public sealed record OnayBasamagiCevabi(int Sira, decimal EnAzTutar, Guid? RolId, Guid? KullaniciId, string Ad);

public sealed record OnaySecenegi(Guid Id, string Ad);

public sealed record OnayKuraliCevabi(IReadOnlyList<OnayBasamagiCevabi> Basamaklar, IReadOnlyList<OnaySecenegi> Roller, IReadOnlyList<OnaySecenegi> Kullanicilar);
