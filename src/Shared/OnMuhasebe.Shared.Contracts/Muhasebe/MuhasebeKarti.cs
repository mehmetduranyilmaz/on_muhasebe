namespace OnMuhasebe.Shared.Contracts.Muhasebe;

// Kaynak modüllerin muhasebe aktarımına sunduğu kart bilgisi (cari, kasa/banka hesabı, gelir-gider kategorisi).
// Tur: cari için Musteri/Tedarikci/HerIkisi, hesap için Kasa/Banka/KrediKarti/Pos, kategori için Gelir/Gider.
public sealed record MuhasebeKarti(Guid Id, string Kod, string Ad, string Tur);
