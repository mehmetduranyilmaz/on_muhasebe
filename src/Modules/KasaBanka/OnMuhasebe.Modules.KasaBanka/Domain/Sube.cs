using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Shared.Domain.Entities;

namespace OnMuhasebe.Modules.KasaBanka.Domain;

// Bankaya bağlı şube kartı. Banka hesabı açarken seçilir; portföy yöneticisi ve iletişim bilgileri şube bazında tutulur.
internal sealed class Sube : FirmaEntity
{
    private Sube() { }

    public string BankaAdi { get; private set; } = null!;
    public string? Kod { get; private set; }
    public string Ad { get; private set; } = null!;
    public string? PortfoyYoneticisi { get; private set; }
    public string? Telefon { get; private set; }
    public string? Faks { get; private set; }

    public static Sube Olustur(SubeKaydetIstegi i) => new()
    {
        BankaAdi = i.BankaAdi.Trim(),
        Kod = Temiz(i.Kod),
        Ad = i.Ad.Trim(),
        PortfoyYoneticisi = Temiz(i.PortfoyYoneticisi),
        Telefon = Temiz(i.Telefon),
        Faks = Temiz(i.Faks)
    };

    public void Guncelle(SubeKaydetIstegi i)
    {
        BankaAdi = i.BankaAdi.Trim();
        Kod = Temiz(i.Kod);
        Ad = i.Ad.Trim();
        PortfoyYoneticisi = Temiz(i.PortfoyYoneticisi);
        Telefon = Temiz(i.Telefon);
        Faks = Temiz(i.Faks);
    }

    private static string? Temiz(string? deger) => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();
}
