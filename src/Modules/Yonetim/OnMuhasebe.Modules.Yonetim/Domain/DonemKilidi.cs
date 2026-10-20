using OnMuhasebe.Shared.Domain.Entities;

namespace OnMuhasebe.Modules.Yonetim.Domain;

// Firmanın kilitli dönemi (firma başına tek satır): kilit tarihi ve öncesindeki hareket ve faturalar değiştirilemez.
internal sealed class DonemKilidi : FirmaEntity
{
    public const int AciklamaUzunlugu = 300;

    private DonemKilidi() { }

    public DateOnly? KilitTarihi { get; private set; }
    public string? Aciklama { get; private set; }

    public static DonemKilidi Olustur() => new();

    public void Guncelle(DateOnly? kilitTarihi, string? aciklama)
    {
        KilitTarihi = kilitTarihi;
        Aciklama = string.IsNullOrWhiteSpace(aciklama) ? null : aciklama.Trim();
    }
}
