using OnMuhasebe.Shared.Domain.Dogrulama;
using OnMuhasebe.Shared.Domain.Entities;

namespace OnMuhasebe.Modules.Yonetim.Domain;

// Ürünü satan tarafın tek satırlık ayarları: abonelik ödemesinin yapılacağı banka hesabı.
internal sealed class SistemAyari : BaseEntity
{
    private SistemAyari() { }

    public string? OdemeAlicisi { get; private set; }
    public string? OdemeBankasi { get; private set; }
    public string? OdemeIban { get; private set; }
    public string? OdemeNotu { get; private set; }

    public static SistemAyari Olustur() => new();

    public void OdemeBilgileriniGuncelle(string? alici, string? banka, string? iban, string? not)
    {
        OdemeAlicisi = Bos(alici);
        OdemeBankasi = Bos(banka);
        OdemeIban = Bos(iban) is { } i ? Iban.Normalize(i) : null;
        OdemeNotu = Bos(not);
    }

    private static string? Bos(string? deger) => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();
}
