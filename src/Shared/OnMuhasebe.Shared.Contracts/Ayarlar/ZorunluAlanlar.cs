namespace OnMuhasebe.Shared.Contracts.Ayarlar;

// Firmanın zorunlu yapabileceği isteğe bağlı alan. Kod "modul.form.alan" biçimindedir; Modul lisans kodudur (lisanssız modülün alanları listelenmez).
// Sistemin her durumda zorunlu tuttuğu alanlar (ünvan, tarih, KDV oranı...) bu listede yer almaz; böylece hiçbir zaman gevşetilemez.
public sealed record ZorunluAlanTanimi(string Kod, string Modul, string Form, string Ad);

public sealed record ZorunluAlanlarCevabi(IReadOnlyList<ZorunluAlanTanimi> Tanimlar, IReadOnlyList<string> Secili);

public sealed record ZorunluAlanlarIstegi(IReadOnlyList<string> Secili);
