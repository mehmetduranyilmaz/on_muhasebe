using OnMuhasebe.Shared.Domain.Entities;

namespace OnMuhasebe.Modules.Yonetim.Domain;

internal sealed class Kullanici : BaseEntity
{
    public const int AzamiHataliGiris = 5;
    public static readonly TimeSpan KilitSuresi = TimeSpan.FromMinutes(15);

    private Kullanici() { }

    public string Ad { get; private set; } = null!;
    public string Soyad { get; private set; } = null!;
    public string Eposta { get; private set; } = null!;
    public string SifreOzeti { get; private set; } = null!;
    public bool AktifMi { get; private set; } = true;
    public bool SistemYoneticisiMi { get; private set; }
    public int HataliGirisSayisi { get; private set; }
    public DateTime? KilitBitisTarihi { get; private set; }
    public Guid? SonFirmaId { get; private set; }

    // Kayıtla açılan hesap, e-postadaki bağlantıyla doğrulanana kadar giriş yapamaz. Davetle ya da şifre sıfırlamayla
    // açılan/kurtarılan hesap da doğrulanmış sayılır (e-postanın sahibi olduğu kanıtlanmıştır).
    public bool EpostaDogrulandiMi { get; private set; }

    public string AdSoyad => $"{Ad} {Soyad}";

    public static Kullanici Olustur(string ad, string soyad, string eposta, bool sistemYoneticisiMi = false) => new()
    {
        Ad = ad.Trim(),
        Soyad = soyad.Trim(),
        Eposta = EpostaNormalize(eposta),
        SistemYoneticisiMi = sistemYoneticisiMi
    };

    public void EpostaDogrula() => EpostaDogrulandiMi = true;

    // Doğrulanmamış hesap henüz kimsenin değildir: aynı e-postayla yeniden kayıt ya da davet, bilgileri günceller.
    public void BekleyenHesabiGuncelle(string ad, string soyad)
    {
        if (EpostaDogrulandiMi)
        {
            throw new InvalidOperationException("Doğrulanmış hesabın bilgileri bu yolla değiştirilemez.");
        }

        Ad = ad.Trim();
        Soyad = soyad.Trim();
    }

    public static string EpostaNormalize(string eposta) => eposta.Trim().ToLowerInvariant();

    public void SifreBelirle(string sifreOzeti) => SifreOzeti = sifreOzeti;

    // Şifre değişince (kullanıcı ya da sıfırlama bağlantısıyla) hatalı giriş sayacı ve kilit de sıfırlanır.
    public void SifreDegistir(string sifreOzeti)
    {
        SifreOzeti = sifreOzeti;
        HataliGirisSayisi = 0;
        KilitBitisTarihi = null;
    }

    public bool KilitliMi(DateTime simdi) => KilitBitisTarihi > simdi;

    public void HataliGirisYapildi(DateTime simdi)
    {
        HataliGirisSayisi++;
        if (HataliGirisSayisi >= AzamiHataliGiris)
        {
            KilitBitisTarihi = simdi + KilitSuresi;
            HataliGirisSayisi = 0;
        }
    }

    public void OturumAcildi(Guid? firmaId)
    {
        HataliGirisSayisi = 0;
        KilitBitisTarihi = null;
        SonFirmaId = firmaId;
    }
}
