using OnMuhasebe.Shared.Domain.Entities;

namespace OnMuhasebe.Modules.Yonetim.Domain;

internal sealed class RefreshToken : BaseEntity
{
    private RefreshToken() { }

    public Guid KullaniciId { get; private set; }

    // Token'ın hangi firma oturumu için verildiği; firma filtresine tabi değildir.
    public Guid? FirmaId { get; private set; }

    // Token'ın kendisi değil SHA-256 özeti saklanır; veritabanı sızsa bile token kullanılamaz.
    public string TokenOzeti { get; private set; } = null!;
    public DateTime BitisTarihi { get; private set; }
    public DateTime? IptalTarihi { get; private set; }
    public Guid? YerineGecenId { get; private set; }

    public static RefreshToken Olustur(Guid kullaniciId, Guid? firmaId, string tokenOzeti, DateTime bitis) => new()
    {
        KullaniciId = kullaniciId,
        FirmaId = firmaId,
        TokenOzeti = tokenOzeti,
        BitisTarihi = bitis
    };

    public bool SuresiDolduMu(DateTime simdi) => simdi >= BitisTarihi;

    public void IptalEt(DateTime simdi, Guid? yerineGecenId = null)
    {
        IptalTarihi ??= simdi;
        YerineGecenId ??= yerineGecenId;
    }
}
