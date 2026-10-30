namespace OnMuhasebe.Modules.KasaBanka.Contracts;

// Bankaya bağlı şube kartı. Şube kodu bankanın verdiği numaradır (ör. 1234). Portföy yöneticisi (müşteri temsilcisi) ve
// telefon/faks gerçek hayatta şube bazında tanımlanır.
public sealed record SubeOzeti(Guid Id, string BankaAdi, string? Kod, string Ad, string? PortfoyYoneticisi, string? Telefon, string? Faks);

public sealed record SubeDetayi(Guid Id, string BankaAdi, string? Kod, string Ad, string? PortfoyYoneticisi, string? Telefon, string? Faks, uint Surum);

public sealed record SubeKaydetIstegi(string BankaAdi, string? Kod, string Ad, string? PortfoyYoneticisi, string? Telefon, string? Faks, uint Surum = 0);
