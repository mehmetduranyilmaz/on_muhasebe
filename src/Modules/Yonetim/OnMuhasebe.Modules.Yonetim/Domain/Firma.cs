using OnMuhasebe.Shared.Domain.Entities;

namespace OnMuhasebe.Modules.Yonetim.Domain;

internal sealed class Firma : BaseEntity
{
    private Firma() { }

    public string Unvan { get; private set; } = null!;
    public string? VergiNo { get; private set; }
    public string? VergiDairesi { get; private set; }

    // Fatura gibi yazdırılan belgelerin üst bilgisi.
    public string? Il { get; private set; }
    public string? Ilce { get; private set; }
    public string? Adres { get; private set; }
    public string? Telefon { get; private set; }
    public string? Eposta { get; private set; }
    public bool AktifMi { get; private set; } = true;

    public static Firma Olustur(string unvan, string? vergiNo, string? vergiDairesi) => new()
    {
        Unvan = unvan.Trim(),
        VergiNo = BosIseNull(vergiNo),
        VergiDairesi = BosIseNull(vergiDairesi)
    };

    public void BilgileriGuncelle(string unvan, string? vergiNo, string? vergiDairesi, string? il, string? ilce, string? adres,
        string? telefon, string? eposta)
    {
        Unvan = unvan.Trim();
        VergiNo = BosIseNull(vergiNo);
        VergiDairesi = BosIseNull(vergiDairesi);
        Il = BosIseNull(il);
        Ilce = BosIseNull(ilce);
        Adres = BosIseNull(adres);
        Telefon = BosIseNull(telefon);
        Eposta = BosIseNull(eposta);
    }

    private static string? BosIseNull(string? deger) => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();
}
