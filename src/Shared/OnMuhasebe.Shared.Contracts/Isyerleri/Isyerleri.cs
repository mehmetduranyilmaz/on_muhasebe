namespace OnMuhasebe.Shared.Contracts.Isyerleri;

// Firmanın işyeri (merkez/şube). Modüller belge, depo ve kasa için bunu kullanır; tanımı Yönetim modülündedir.
public sealed record IsyeriOzeti(
    Guid Id, string Kod, string Ad, bool MerkezMi, bool AktifMi, string? Adres, string? Il, string? Ilce, string? Telefon, string? SgkSicilNo)
{
    public string? TamAdres => string.Join(", ", new[] { Adres, string.Join("/", new[] { Ilce, Il }.Where(x => !string.IsNullOrEmpty(x))) }
        .Where(x => !string.IsNullOrEmpty(x))) is { Length: > 0 } a ? a : null;
}

public sealed record IsyeriKaydetIstegi(string Kod, string Ad, string? Adres, string? Il, string? Ilce, string? Telefon, string? SgkSicilNo);
