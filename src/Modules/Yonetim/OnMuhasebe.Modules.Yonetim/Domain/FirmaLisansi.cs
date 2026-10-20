using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Domain.Entities;

namespace OnMuhasebe.Modules.Yonetim.Domain;

internal sealed class FirmaLisansi : FirmaEntity
{
    private FirmaLisansi() { }

    private FirmaLisansi(Guid firmaId) : base(firmaId) { }

    public Guid PaketId { get; private set; }
    public DateTime BaslangicTarihi { get; private set; }
    public DateTime BitisTarihi { get; private set; }
    public List<string> EkModuller { get; private set; } = [];

    public Paket Paket { get; private set; } = null!;

    public static FirmaLisansi Olustur(Guid firmaId, Paket paket, DateTime baslangic, DateTime bitis, IEnumerable<string>? ekModuller = null)
    {
        var lisans = new FirmaLisansi(firmaId) { BaslangicTarihi = baslangic };
        lisans.Guncelle(paket, bitis, ekModuller);
        return lisans;
    }

    public void Guncelle(Paket paket, DateTime bitis, IEnumerable<string>? ekModuller)
    {
        Paket = paket;
        PaketId = paket.Id;
        BitisTarihi = bitis;
        EkModuller = ekModuller?.Distinct().ToList() ?? [];
    }

    public IReadOnlyList<string> AcikModuller() =>
        Paket.TumModullerMi
            ? ModulKodlari.Tumu
            : Paket.Moduller.Union(EkModuller).Where(ModulKodlari.Tumu.Contains).ToList();
}
