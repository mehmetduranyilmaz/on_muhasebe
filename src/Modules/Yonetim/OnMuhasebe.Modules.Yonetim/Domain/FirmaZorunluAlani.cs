using OnMuhasebe.Shared.Domain.Entities;

namespace OnMuhasebe.Modules.Yonetim.Domain;

// Firmanın zorunlu yaptığı isteğe bağlı alan (kod modüllerin tanımlarından gelir, ör. "cari.kart.telefon").
internal sealed class FirmaZorunluAlani : FirmaEntity
{
    private FirmaZorunluAlani() { }

    public string Kod { get; private set; } = null!;

    public static FirmaZorunluAlani Olustur(string kod) => new() { Kod = kod };
}
