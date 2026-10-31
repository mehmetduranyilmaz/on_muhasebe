namespace OnMuhasebe.Shared.Infrastructure.Kimlik;

public interface IMevcutKullanici
{
    Guid? KullaniciId { get; }
    Guid? FirmaId { get; }
    string? KullaniciAdi { get; }
    bool SistemYoneticisiMi { get; }

    // İşyeri kısıtlı kullanıcının izinli işyerleri; null: kısıt yok. Yetki filtresinden geçen isteklerde dolar.
    List<Guid>? IzinliIsyerleri { get; }
}
