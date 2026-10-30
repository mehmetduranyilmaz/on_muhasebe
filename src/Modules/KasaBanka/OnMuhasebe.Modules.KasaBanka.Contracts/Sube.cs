namespace OnMuhasebe.Modules.KasaBanka.Contracts;

// Bankaya bağlı şube kartı. Portföy yöneticisi (müşteri temsilcisi) gerçek hayatta şube bazında tanımlanır; telefon/faks da şubenindir.
public sealed record SubeOzeti(Guid Id, string BankaAdi, string Ad, string? PortfoyYoneticisi, string? Telefon, string? Faks);

public sealed record SubeDetayi(Guid Id, string BankaAdi, string Ad, string? PortfoyYoneticisi, string? Telefon, string? Faks, uint Surum);

public sealed record SubeKaydetIstegi(string BankaAdi, string Ad, string? PortfoyYoneticisi, string? Telefon, string? Faks, uint Surum = 0);
