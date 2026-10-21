using OnMuhasebe.Modules.Bildirim.Contracts;
using OnMuhasebe.Shared.Contracts.Bildirimler;
using OnMuhasebe.Shared.Domain.Abstractions;
using OnMuhasebe.Shared.Domain.Entities;

namespace OnMuhasebe.Modules.Bildirim.Domain;

internal enum EpostaDurumu
{
    Yok,
    Bekliyor,
    Gonderildi,
    Hata
}

// Bir kullanıcıya düşen bildirim. Aynı anahtar aynı kullanıcıya bir kez yazılır (tekilleştirme).
[DenetimDisi]
internal sealed class Bildirim : FirmaEntity
{
    public const int AnahtarUzunlugu = 200;
    public const int BaslikUzunlugu = 200;
    public const int MetinUzunlugu = 1000;
    public const int BaglantiUzunlugu = 300;

    private Bildirim() { }

    public Guid KullaniciId { get; private set; }
    public BildirimTuru Tur { get; private set; }
    public string Anahtar { get; private set; } = null!;
    public string Baslik { get; private set; } = null!;
    public string Metin { get; private set; } = null!;
    public string? Baglanti { get; private set; }
    public BildirimOnemi Onem { get; private set; }
    public DateTime? OkunmaTarihi { get; private set; }
    public EpostaDurumu Eposta { get; private set; }

    public bool OkunduMu => OkunmaTarihi is not null;

    public static Bildirim Olustur(Guid kullaniciId, BildirimAdayi a, bool epostaGidecek) => new()
    {
        KullaniciId = kullaniciId,
        Tur = a.Tur,
        Anahtar = Kisalt(a.Anahtar, AnahtarUzunlugu),
        Baslik = Kisalt(a.Baslik, BaslikUzunlugu),
        Metin = Kisalt(a.Metin, MetinUzunlugu),
        Baglanti = a.Baglanti is null ? null : Kisalt(a.Baglanti, BaglantiUzunlugu),
        Onem = a.Onem,
        Eposta = epostaGidecek ? EpostaDurumu.Bekliyor : EpostaDurumu.Yok
    };

    public void Okundu(DateTime zaman) => OkunmaTarihi ??= zaman;

    public void EpostaSonucu(bool basarili) => Eposta = basarili ? EpostaDurumu.Gonderildi : EpostaDurumu.Hata;

    private static string Kisalt(string s, int uzunluk) => s.Length <= uzunluk ? s : s[..(uzunluk - 1)] + "…";
}

// Firma ayarı (firma başına tek satır).
internal sealed class BildirimAyari : FirmaEntity
{
    public const int VarsayilanVadeOncesiGun = 3;

    private BildirimAyari() { }

    public int VadeOncesiGun { get; private set; } = VarsayilanVadeOncesiGun;
    public List<string> KapaliTurler { get; private set; } = [];

    public static BildirimAyari Olustur() => new();

    public void Guncelle(int vadeOncesiGun, IEnumerable<BildirimTuru> kapaliTurler)
    {
        VadeOncesiGun = vadeOncesiGun;
        KapaliTurler = kapaliTurler.Distinct().Select(t => t.ToString()).ToList();
    }

    public BildirimAyarlari Sozlesme() => new(VadeOncesiGun, Turler(KapaliTurler));

    public static IReadOnlyList<BildirimTuru> Turler(IEnumerable<string> adlar) =>
        adlar.Select(a => Enum.TryParse<BildirimTuru>(a, out var t) ? t : (BildirimTuru?)null).OfType<BildirimTuru>().ToList();
}

// Kullanıcının bu firmadaki bildirim tercihi.
internal sealed class KullaniciTercihi : FirmaEntity
{
    private KullaniciTercihi() { }

    public Guid KullaniciId { get; private set; }
    public bool BildirimAlir { get; private set; }
    public List<string> KapaliTurler { get; private set; } = [];
    public EpostaSikligi Eposta { get; private set; }
    public DateOnly? SonOzetTarihi { get; private set; }

    public static KullaniciTercihi Olustur(Guid kullaniciId, BildirimTercihleri t)
    {
        var tercih = new KullaniciTercihi { KullaniciId = kullaniciId };
        tercih.Guncelle(t);
        return tercih;
    }

    public void Guncelle(BildirimTercihleri t)
    {
        BildirimAlir = t.BildirimAlir;
        KapaliTurler = t.KapaliTurler.Distinct().Select(x => x.ToString()).ToList();
        Eposta = t.Eposta;
    }

    public void OzetGonderildi(DateOnly gun) => SonOzetTarihi = gun;

    public BildirimTercihleri Sozlesme() => new(BildirimAlir, BildirimAyari.Turler(KapaliTurler), Eposta);
}

// Sistem geneli SMTP ayarı (tek satır). Şifre AES-GCM ile şifrelenmiş saklanır.
[DenetimDisi]
internal sealed class EpostaAyari : BaseEntity
{
    private EpostaAyari() { }

    public bool AktifMi { get; private set; }
    public string? Sunucu { get; private set; }
    public int Port { get; private set; } = 587;
    public SmtpGuvenligi Guvenlik { get; private set; } = SmtpGuvenligi.StartTls;
    public string? KullaniciAdi { get; private set; }
    public string? SifreSifreli { get; private set; }
    public string? GonderenAdres { get; private set; }
    public string? GonderenAd { get; private set; }

    public static EpostaAyari Olustur() => new();

    public void Guncelle(EpostaAyarlari a, string? sifreSifreli)
    {
        AktifMi = a.AktifMi;
        Sunucu = Bos(a.Sunucu);
        Port = a.Port;
        Guvenlik = a.Guvenlik;
        KullaniciAdi = Bos(a.KullaniciAdi);
        SifreSifreli = KullaniciAdi is null ? null : sifreSifreli ?? SifreSifreli;
        GonderenAdres = Bos(a.GonderenAdres);
        GonderenAd = Bos(a.GonderenAd);
    }

    public EpostaAyarlari Sozlesme() =>
        new(AktifMi, Sunucu, Port, Guvenlik, KullaniciAdi, null, SifreSifreli is not null, GonderenAdres, GonderenAd);

    private static string? Bos(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
