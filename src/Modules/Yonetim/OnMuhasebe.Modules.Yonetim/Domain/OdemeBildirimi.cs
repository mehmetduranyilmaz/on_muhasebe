using OnMuhasebe.Modules.Yonetim.Contracts;
using OnMuhasebe.Shared.Domain.Entities;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Yonetim.Domain;

// Firmanın "havale/EFT yaptım" bildirimi. Ödeme sağlayıcısı yok: sistem yöneticisi hesabına geçen parayı görünce onaylar,
// onayla birlikte firmanın lisansı istenen paketle ve süreyle uzar.
internal sealed class OdemeBildirimi : FirmaEntity
{
    public const int GonderenUzunlugu = 200;
    public const int AciklamaUzunlugu = 500;

    private OdemeBildirimi() { }

    public Guid PaketId { get; private set; }
    public int AySayisi { get; private set; }
    public decimal Tutar { get; private set; }
    public DateOnly OdemeTarihi { get; private set; }
    public string Gonderen { get; private set; } = null!;
    public string? Aciklama { get; private set; }
    public OdemeBildirimiDurumu Durum { get; private set; }
    public string? SonucNotu { get; private set; }
    public DateTime? SonucTarihi { get; private set; }
    public DateTime? YeniBitisTarihi { get; private set; }

    public Paket Paket { get; private set; } = null!;

    public static OdemeBildirimi Olustur(Paket paket, int aySayisi, decimal tutar, DateOnly odemeTarihi, string gonderen, string? aciklama) => new()
    {
        Paket = paket,
        PaketId = paket.Id,
        AySayisi = aySayisi,
        Tutar = tutar,
        OdemeTarihi = odemeTarihi,
        Gonderen = gonderen.Trim(),
        Aciklama = string.IsNullOrWhiteSpace(aciklama) ? null : aciklama.Trim(),
        Durum = OdemeBildirimiDurumu.Bekliyor
    };

    public Sonuc Onayla(DateTime yeniBitis, DateTime simdi, string? not)
    {
        if (Durum != OdemeBildirimiDurumu.Bekliyor)
        {
            return YonetimHatalari.BildirimSonuclanmis;
        }

        Durum = OdemeBildirimiDurumu.Onaylandi;
        YeniBitisTarihi = yeniBitis;
        SonucTarihi = simdi;
        SonucNotu = string.IsNullOrWhiteSpace(not) ? null : not.Trim();
        return Sonuc.Basarili();
    }

    public Sonuc Reddet(string not, DateTime simdi)
    {
        if (Durum != OdemeBildirimiDurumu.Bekliyor)
        {
            return YonetimHatalari.BildirimSonuclanmis;
        }

        Durum = OdemeBildirimiDurumu.Reddedildi;
        SonucTarihi = simdi;
        SonucNotu = not.Trim();
        return Sonuc.Basarili();
    }
}
