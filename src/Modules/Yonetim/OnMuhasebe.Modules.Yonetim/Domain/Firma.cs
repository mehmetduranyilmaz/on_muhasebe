using OnMuhasebe.Shared.Domain.Entities;

namespace OnMuhasebe.Modules.Yonetim.Domain;

internal sealed class Firma : BaseEntity
{
    private Firma() { }

    public string Unvan { get; private set; } = null!;
    public string? VergiNo { get; private set; }
    public string? VergiDairesi { get; private set; }
    public bool AktifMi { get; private set; } = true;

    public static Firma Olustur(string unvan, string? vergiNo, string? vergiDairesi) => new()
    {
        Unvan = unvan.Trim(),
        VergiNo = BosIseNull(vergiNo),
        VergiDairesi = BosIseNull(vergiDairesi)
    };

    private static string? BosIseNull(string? deger) => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();
}
