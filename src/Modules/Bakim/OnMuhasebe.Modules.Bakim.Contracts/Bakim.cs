using System.Text.Json.Serialization;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Moduller;

namespace OnMuhasebe.Modules.Bakim.Contracts;

public static class BakimYetkileri
{
    private const string Baslik = "Bakım Takibi";

    public static readonly YetkiKaynagi Ekipman = new("bakim.ekipman", ModulKodlari.Bakim, Baslik, "Ekipman kartları ve bakım planları", YetkiIslemleri.Temel, []);

    public static readonly YetkiKaynagi IsEmri = new("bakim.isemri", ModulKodlari.Bakim, Baslik, "Bakım ve arıza iş emirleri", YetkiIslemleri.Temel, ["bakim.ekipman.goruntule"]);

    public static readonly YetkiKaynagi Rapor = new("bakim.rapor", ModulKodlari.Bakim, Baslik, "Bakım maliyet ve duruş raporu", YetkiIslemleri.SadeceGoruntule, []);

    public static IReadOnlyList<YetkiKaynagi> Tumu { get; } = [Ekipman, IsEmri, Rapor];
}

[JsonConverter(typeof(JsonStringEnumConverter<EkipmanTuru>))]
public enum EkipmanTuru { Makine, Kompresor, Forklift, KaldirmaEkipmani, BasincliKap, Jenerator, Diger }

[JsonConverter(typeof(JsonStringEnumConverter<SayacBirimi>))]
public enum SayacBirimi { Yok, Saat, Adet }

[JsonConverter(typeof(JsonStringEnumConverter<EkipmanDurumu>))]
public enum EkipmanDurumu { Calisiyor, Arizali, Bakimda, HizmetDisi }

[JsonConverter(typeof(JsonStringEnumConverter<PlanDurumu>))]
public enum PlanDurumu { Zamaninda, Yaklasiyor, Gecikti }

[JsonConverter(typeof(JsonStringEnumConverter<IsEmriTuru>))]
public enum IsEmriTuru { PlanliBakim, Ariza, YasalKontrol }

[JsonConverter(typeof(JsonStringEnumConverter<IsEmriDurumu>))]
public enum IsEmriDurumu { Acik, Devam, Kapandi, Iptal }

public static class BakimMetinleri
{
    public static string Tur(EkipmanTuru t) => t switch
    {
        EkipmanTuru.Makine => "Makine",
        EkipmanTuru.Kompresor => "Kompresör",
        EkipmanTuru.Forklift => "Forklift",
        EkipmanTuru.KaldirmaEkipmani => "Kaldırma ekipmanı",
        EkipmanTuru.BasincliKap => "Basınçlı kap",
        EkipmanTuru.Jenerator => "Jeneratör",
        _ => "Diğer"
    };

    public static string Durum(EkipmanDurumu d) => d switch
    {
        EkipmanDurumu.Arizali => "Arızalı",
        EkipmanDurumu.Bakimda => "Bakımda",
        EkipmanDurumu.HizmetDisi => "Hizmet dışı",
        _ => "Çalışıyor"
    };

    public static string Plan(PlanDurumu d) => d switch
    {
        PlanDurumu.Yaklasiyor => "Yaklaşıyor",
        PlanDurumu.Gecikti => "Gecikti",
        _ => "Zamanında"
    };

    public static string IsEmri(IsEmriTuru t) => t switch
    {
        IsEmriTuru.Ariza => "Arıza",
        IsEmriTuru.YasalKontrol => "Yasal kontrol",
        _ => "Planlı bakım"
    };

    public static string IsEmriDurumu(IsEmriDurumu d) => d switch
    {
        Contracts.IsEmriDurumu.Devam => "Devam ediyor",
        Contracts.IsEmriDurumu.Kapandi => "Kapandı",
        Contracts.IsEmriDurumu.Iptal => "İptal",
        _ => "Açık"
    };
}

public sealed record EkipmanKaydetIstegi(
    string? Kod, string Ad, EkipmanTuru Tur, string? Marka, string? Model, string? SeriNo, int? UretimYili, string? Konum, Guid? IsyeriId, Guid? SorumluKullaniciId,
    Guid? DemirbasId, Guid? MasrafMerkeziId, SayacBirimi SayacBirimi, decimal? Sayac, EkipmanDurumu Durum, string? Aciklama, uint Surum = 0);

public sealed record EkipmanOzeti(
    Guid Id, string Kod, string Ad, EkipmanTuru Tur, string? Konum, EkipmanDurumu Durum, decimal? Sayac, SayacBirimi SayacBirimi, string? SorumluAdi, Guid? IsyeriId,
    string? EnYakinPlan, DateOnly? EnYakinTarih, PlanDurumu? EnYakinDurum);

public sealed record EkipmanDetayi(
    Guid Id, string Kod, string Ad, EkipmanTuru Tur, string? Marka, string? Model, string? SeriNo, int? UretimYili, string? Konum, Guid? IsyeriId, Guid? SorumluKullaniciId,
    Guid? DemirbasId, Guid? MasrafMerkeziId, SayacBirimi SayacBirimi, decimal? Sayac, EkipmanDurumu Durum, string? Aciklama, uint Surum);

// Periyot gün ve/veya sayaç; hangisi önce gelirse bakım o zaman görünür. Sonraki tarih boşsa bugünden hesaplanır; düzeltmek için elle verilebilir.
public sealed record BakimPlaniKaydetIstegi(
    string Ad, bool YasalMi, string? KontrolKurulusu, int? PeriyotGun, decimal? PeriyotSayac, DateOnly? SonrakiTarih, decimal? SonrakiSayac, int HatirlatmaGun, bool AktifMi = true);

public sealed record BakimPlaniCevabi(
    Guid Id, Guid EkipmanId, string Ad, bool YasalMi, string? KontrolKurulusu, int? PeriyotGun, decimal? PeriyotSayac, DateOnly? SonYapilmaTarihi, decimal? SonYapilmaSayaci,
    DateOnly? SonrakiTarih, decimal? SonrakiSayac, int HatirlatmaGun, bool AktifMi, PlanDurumu Durum);

public sealed record YaklasanBakim(
    Guid PlanId, Guid EkipmanId, string EkipmanKodu, string EkipmanAdi, string Plan, bool YasalMi, DateOnly? SonrakiTarih, decimal? SonrakiSayac, decimal? Sayac, PlanDurumu Durum);

// Stok kartı verilirse iş emri kapanınca sarf olarak stoktan düşer; verilmezse Ad ve BirimFiyat ile elle yazılır. Birim boşsa kartın temel birimi.
public sealed record IsEmriParcaIstegi(Guid? StokKartiId, Guid? DepoId, string? Ad, string? Birim, decimal Miktar, decimal? BirimFiyat);

public sealed record IsEmriParcasiCevabi(Guid? StokKartiId, Guid? DepoId, string Ad, string? Birim, decimal Miktar, decimal? BirimFiyat);

// İşçilik ve dış servis takip kaydıdır, muhasebe fişi üretmez. Gider muhasebeye faturayla girer; FaturaId o faturayı gösterir.
public sealed record IsEmriKaydetIstegi(
    Guid EkipmanId, IsEmriTuru Tur, Guid? PlanId, string Baslik, string? Aciklama, DateTime? BildirimZamani, bool MakineDurduMu, decimal IscilikTutari, decimal DisServisTutari,
    string? DisServisFirma, Guid? FaturaId, string? KontrolKurulusu, string? RaporNo, IReadOnlyList<IsEmriParcaIstegi>? Parcalar);

public sealed record IsEmriKapatmaIstegi(DateTime BitisZamani, decimal? SayacDegeri, string? RaporNo);

public sealed record IsEmriOzeti(
    Guid Id, string Kod, Guid EkipmanId, string EkipmanAdi, IsEmriTuru Tur, IsEmriDurumu Durum, string Baslik, DateTime BildirimZamani, DateTime? BitisZamani, decimal? DurusSaati);

public sealed record IsEmriDetayi(
    Guid Id, string Kod, Guid EkipmanId, string EkipmanAdi, IsEmriTuru Tur, Guid? PlanId, IsEmriDurumu Durum, string Baslik, string? Aciklama, DateTime BildirimZamani,
    DateTime? BaslangicZamani, DateTime? BitisZamani, bool MakineDurduMu, decimal? DurusSaati, decimal IscilikTutari, decimal DisServisTutari, string? DisServisFirma, Guid? FaturaId,
    string? KontrolKurulusu, string? RaporNo, decimal? SayacDegeri, IReadOnlyList<IsEmriParcasiCevabi> Parcalar, string? StokUyarisi = null);

public sealed record BakimSorumlusu(Guid Id, string AdSoyad);

// StokMaliyetiDahilMi: kullanıcının stok maliyeti görme yetkisi yoksa stoktan düşen parçanın maliyeti ParcaMaliyeti'ne girmez.
public sealed record BakimMaliyeti(
    Guid EkipmanId, string Kod, string Ad, decimal ParcaMaliyeti, decimal Iscilik, decimal DisServis, decimal Toplam, int ArizaSayisi, int BakimSayisi, decimal DurusSaati, bool StokMaliyetiDahilMi);
