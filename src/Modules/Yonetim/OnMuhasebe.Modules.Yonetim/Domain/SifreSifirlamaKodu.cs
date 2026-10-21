using OnMuhasebe.Shared.Domain.Abstractions;
using OnMuhasebe.Shared.Domain.Entities;

namespace OnMuhasebe.Modules.Yonetim.Domain;

// "Şifremi unuttum" bağlantısındaki tek kullanımlık kod. Kodun kendisi değil SHA-256 özeti saklanır.
[DenetimDisi]
internal sealed class SifreSifirlamaKodu : BaseEntity
{
    public static readonly TimeSpan GecerlilikSuresi = TimeSpan.FromHours(1);

    // Aynı hesaba bu süre dolmadan yeni e-posta gönderilmez (e-posta bombardımanına karşı).
    public static readonly TimeSpan YenidenGondermeAraligi = TimeSpan.FromMinutes(2);

    private SifreSifirlamaKodu() { }

    public Guid KullaniciId { get; private set; }
    public string KodOzeti { get; private set; } = null!;
    public DateTime BitisTarihi { get; private set; }
    public DateTime? KullanimTarihi { get; private set; }

    public static SifreSifirlamaKodu Olustur(Guid kullaniciId, string kodOzeti, DateTime simdi) => new()
    {
        KullaniciId = kullaniciId,
        KodOzeti = kodOzeti,
        BitisTarihi = simdi + GecerlilikSuresi
    };

    public bool GecerliMi(DateTime simdi) => KullanimTarihi is null && simdi < BitisTarihi;

    public void Kullan(DateTime simdi) => KullanimTarihi ??= simdi;
}
