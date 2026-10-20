using OnMuhasebe.Shared.Domain.Entities;

namespace OnMuhasebe.Modules.Yonetim.Domain;

internal sealed class Paket : BaseEntity
{
    private Paket() { }

    public string Kod { get; private set; } = null!;
    public string Ad { get; private set; } = null!;

    // Deneme paketi gibi, ileride eklenen modülleri de otomatik kapsayan paketler için.
    public bool TumModullerMi { get; private set; }
    public List<string> Moduller { get; private set; } = [];
}
