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

    public static Kullanici Olustur(string ad, string soyad, string eposta, bool sistemYoneticisiMi = false) => new()
    {
        Ad = ad.Trim(),
        Soyad = soyad.Trim(),
        Eposta = EpostaNormalize(eposta),
        SistemYoneticisiMi = sistemYoneticisiMi
    };

    public static string EpostaNormalize(string eposta) => eposta.Trim().ToLowerInvariant();

    public void SifreBelirle(string sifreOzeti) => SifreOzeti = sifreOzeti;

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
