namespace OnMuhasebe.Shared.Contracts.Tanimlar;

// Modüllerin sınıflandırma grupları (cari grubu, stok grubu...) için ortak sözleşme.
public sealed record GrupOzeti(Guid Id, string Ad, int KayitSayisi);

public sealed record GrupKaydetIstegi(string Ad);
