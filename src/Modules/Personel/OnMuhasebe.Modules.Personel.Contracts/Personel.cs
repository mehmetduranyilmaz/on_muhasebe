using System.Text.Json.Serialization;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Moduller;

namespace OnMuhasebe.Modules.Personel.Contracts;

public static class PersonelYetkileri
{
    private const string Baslik = "Personel";

    public static readonly YetkiKaynagi Kart = new("personel.kart", ModulKodlari.Personel, Baslik, "Personel kartları (maaş ve kimlik hariç)", YetkiIslemleri.Temel, []);

    // Maaş tutarı, TC kimlik ve IBAN bu yetkiyle görünür.
    public static readonly YetkiKaynagi Maas = new("personel.maas", ModulKodlari.Personel, Baslik, "Maaş, ödeme ve personel hesabı",
        [YetkiIslemleri.Goruntule, YetkiIslemleri.Ekle, YetkiIslemleri.Duzenle, YetkiIslemleri.Sil, YetkiIslemleri.Onayla, YetkiIslemleri.Iptal], ["personel.kart.goruntule", "kasabanka.hesap.goruntule"]);

    public static readonly YetkiKaynagi Avans = new("personel.avans", ModulKodlari.Personel, Baslik, "Personel avansları",
        [YetkiIslemleri.Goruntule, YetkiIslemleri.Ekle, YetkiIslemleri.Iptal], ["personel.kart.goruntule", "kasabanka.hesap.goruntule"]);

    public static readonly YetkiKaynagi Masraf = new("personel.masraf", ModulKodlari.Personel, Baslik, "Masraf beyanları",
        [YetkiIslemleri.Goruntule, YetkiIslemleri.Ekle, YetkiIslemleri.Duzenle, YetkiIslemleri.Sil, YetkiIslemleri.Onayla], ["personel.kart.goruntule", "kasabanka.hesap.goruntule"]);

    public static readonly YetkiKaynagi Zimmet = new("personel.zimmet", ModulKodlari.Personel, Baslik, "Zimmetler", YetkiIslemleri.Temel, ["personel.kart.goruntule"]);

    // Kullanıcıya bağlı personel kendi masrafını girer ve görür; başkasının bilgisini görmez.
    public static readonly YetkiKaynagi Kendi = new("personel.kendi", ModulKodlari.Personel, Baslik, "Kendi masraflarım", [YetkiIslemleri.Goruntule, YetkiIslemleri.Ekle], []);

    public static IReadOnlyList<YetkiKaynagi> Tumu { get; } = [Kart, Maas, Avans, Masraf, Zimmet, Kendi];
}

[JsonConverter(typeof(JsonStringEnumConverter<MaasDonemiDurumu>))]
public enum MaasDonemiDurumu { Taslak, Onayli }

[JsonConverter(typeof(JsonStringEnumConverter<MasrafDurumu>))]
public enum MasrafDurumu { Beyan, Onaylandi, Reddedildi, Odendi }

[JsonConverter(typeof(JsonStringEnumConverter<MasrafKategorisi>))]
public enum MasrafKategorisi { Yemek, Yol, Konaklama, Akaryakit, Temsil, Kirtasiye, Diger }

[JsonConverter(typeof(JsonStringEnumConverter<ZimmetDurumu>))]
public enum ZimmetDurumu { Zimmette, IadeEdildi, Kayip }

[JsonConverter(typeof(JsonStringEnumConverter<PersonelOdemeTuru>))]
public enum PersonelOdemeTuru { Maas, Avans, Masraf }

public static class PersonelMetinleri
{
    public static string Masraf(MasrafDurumu d) => d switch
    {
        MasrafDurumu.Onaylandi => "Onaylandı",
        MasrafDurumu.Reddedildi => "Reddedildi",
        MasrafDurumu.Odendi => "Ödendi",
        _ => "Onay bekliyor"
    };

    public static string Kategori(MasrafKategorisi k) => k switch
    {
        MasrafKategorisi.Yemek => "Yemek",
        MasrafKategorisi.Yol => "Yol",
        MasrafKategorisi.Konaklama => "Konaklama",
        MasrafKategorisi.Akaryakit => "Akaryakıt",
        MasrafKategorisi.Temsil => "Temsil ağırlama",
        MasrafKategorisi.Kirtasiye => "Kırtasiye",
        _ => "Diğer"
    };

    public static string Zimmet(ZimmetDurumu d) => d switch
    {
        ZimmetDurumu.IadeEdildi => "İade edildi",
        ZimmetDurumu.Kayip => "Kayıp",
        _ => "Zimmette"
    };

    public static string Odeme(PersonelOdemeTuru t) => t switch
    {
        PersonelOdemeTuru.Avans => "Avans",
        PersonelOdemeTuru.Masraf => "Masraf",
        _ => "Maaş"
    };
}

// Maaş yetkisi olmayan kullanıcı TcKimlik, Iban ve NetMaas göndermez; null gelen değer korunur.
public sealed record PersonelKaydetIstegi(
    string Ad, string Soyad, string? TcKimlik, DateOnly IseGiris, string? Gorev, string? Departman, Guid? IsyeriId, Guid? MasrafMerkeziId, string? Iban, decimal? NetMaas, string? Telefon, string? Eposta, Guid? KullaniciId, uint Surum = 0);

public sealed record PersonelOzeti(Guid Id, string Kod, string AdSoyad, string? Gorev, string? Departman, DateOnly IseGiris, DateOnly? IstenCikis, bool AktifMi, int ZimmetSayisi, Guid? IsyeriId);

// TcKimlik ve Iban maaş yetkisi yoksa maskelidir, NetMaas boştur.
public sealed record PersonelDetayi(
    Guid Id, string Kod, string Ad, string Soyad, string? TcKimlik, DateOnly IseGiris, DateOnly? IstenCikis, string? Gorev, string? Departman, Guid? IsyeriId, Guid? MasrafMerkeziId, string? Iban, decimal? NetMaas, string? Telefon, string? Eposta, Guid? KullaniciId, int ZimmetSayisi, uint Surum);

public sealed record IstenCikisIstegi(DateOnly Tarih);

public sealed record IstenCikisSonucu(PersonelDetayi Personel, int AcikZimmet, decimal KalanBakiye);

public sealed record KullaniciSecenegi(Guid Id, string Ad);

public sealed record DonemAcIstegi(int Yil, int Ay);

public sealed record MaasSatirIstegi(decimal NetMaas, decimal Prim, decimal Kesinti, decimal AvansKesintisi, string? Not);

public sealed record MaasSatirCevabi(Guid Id, Guid PersonelId, string AdSoyad, string? Iban, decimal NetMaas, decimal Prim, decimal Kesinti, decimal AvansKesintisi, decimal AcikAvans, decimal Odenecek, decimal Odenen, decimal Kalan, string? Not);

public sealed record MaasDonemiOzeti(Guid Id, int Yil, int Ay, MaasDonemiDurumu Durum, int KisiSayisi, decimal ToplamOdenecek, decimal ToplamOdenen);

public sealed record MaasDonemiDetayi(Guid Id, int Yil, int Ay, MaasDonemiDurumu Durum, IReadOnlyList<MaasSatirCevabi> Satirlar, uint Surum);

// SatirIdleri boşsa kalanı olan tüm satırlar ödenir. Tutar yalnız tek satırda verilebilir (kısmi ödeme). Kasa/Banka lisansı yoksa hesap verilmez, ödeme kayıt olarak tutulur.
public sealed record MaasOdemeIstegi(DateOnly Tarih, Guid? HesapId, IReadOnlyList<Guid>? SatirIdleri, decimal? Tutar);

public sealed record AvansKaydetIstegi(Guid PersonelId, DateOnly Tarih, decimal Tutar, int TaksitSayisi, Guid? HesapId, string? Aciklama);

public sealed record AvansCevabi(Guid Id, Guid PersonelId, string AdSoyad, DateOnly Tarih, decimal Tutar, int TaksitSayisi, decimal Kalan, bool IptalMi, string? Aciklama);

public sealed record MasrafKaydetIstegi(Guid PersonelId, DateOnly Tarih, MasrafKategorisi Kategori, decimal Tutar, string Aciklama, Guid? MasrafMerkeziId, uint Surum = 0);

public sealed record KendiMasrafIstegi(DateOnly Tarih, MasrafKategorisi Kategori, decimal Tutar, string Aciklama, uint Surum = 0);

public sealed record MasrafKararIstegi(string? Not);

public sealed record MasrafOdemeIstegi(DateOnly Tarih, Guid? HesapId);

public sealed record MasrafCevabi(
    Guid Id, Guid PersonelId, string AdSoyad, DateOnly Tarih, MasrafKategorisi Kategori, decimal Tutar, string Aciklama, Guid? MasrafMerkeziId, MasrafDurumu Durum, string? KararVeren, DateTime? KararTarihi, string? Not, bool OnaylayabilirMi, uint Surum);

public sealed record BenimBilgim(Guid? PersonelId, string? AdSoyad, IReadOnlyList<MasrafCevabi> Masraflar);

public sealed record ZimmetKaydetIstegi(Guid PersonelId, string Ad, string? SeriNo, DateOnly VerilisTarihi, decimal? Bedel, string? Not, uint Surum = 0);

public sealed record ZimmetIadeIstegi(DateOnly Tarih, bool KayipMi, string? Not);

public sealed record ZimmetCevabi(Guid Id, Guid PersonelId, string AdSoyad, string Ad, string? SeriNo, DateOnly VerilisTarihi, DateOnly? IadeTarihi, decimal? Bedel, ZimmetDurumu Durum, string? Not, uint Surum);

public sealed record PersonelHesapSatiri(DateOnly Tarih, string Islem, string? Aciklama, decimal Alacak, decimal Borc, decimal Bakiye);

// Bakiye artıysa firmanın personele borcu, eksiyse personelin firmaya borcu (avans).
public sealed record PersonelHesabi(Guid PersonelId, string AdSoyad, IReadOnlyList<PersonelHesapSatiri> Satirlar, decimal Bakiye);

public sealed record PersonelOdemeCevabi(Guid Id, PersonelOdemeTuru Tur, DateOnly Tarih, decimal Tutar, bool KasaYazildiMi);

// Muhasebe aktarımı: yalnız kasadan geçmeyen kayıtlar. Ödemeler kasa hareketinden gelir.
public sealed record MuhasebePersonelKaydi(Guid Id, string Tur, DateOnly Tarih, string? BelgeNo, string Aciklama, decimal Tutar, Guid? IsyeriId);

public static class MuhasebePersonelTurleri
{
    public const string Masraf = "Masraf";
    public const string AvansMahsubu = "AvansMahsubu";
}

public interface IPersonelMuhasebeSorgusu
{
    Task<IReadOnlyList<MuhasebePersonelKaydi>> KayitlarAsync(DateOnly baslangic, DateOnly bitis, Guid? isyeriId, CancellationToken cancellationToken = default);
}
