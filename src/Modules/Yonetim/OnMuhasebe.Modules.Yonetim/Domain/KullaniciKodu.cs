using OnMuhasebe.Shared.Domain.Abstractions;
using OnMuhasebe.Shared.Domain.Entities;

namespace OnMuhasebe.Modules.Yonetim.Domain;

internal enum KodAmaci
{
    SifreSifirlama,
    EpostaDogrulama
}

// E-postayla gönderilen tek kullanımlık kod (şifre sıfırlama, e-posta doğrulama). Kodun kendisi değil SHA-256 özeti saklanır.
[DenetimDisi]
internal sealed class KullaniciKodu : BaseEntity
{
    // Aynı amaçla aynı hesaba bu süre dolmadan yeni e-posta gönderilmez (e-posta bombardımanına karşı).
    public static readonly TimeSpan YenidenGondermeAraligi = TimeSpan.FromMinutes(2);

    private KullaniciKodu() { }

    public Guid KullaniciId { get; private set; }
    public KodAmaci Amac { get; private set; }
    public string KodOzeti { get; private set; } = null!;
    public DateTime BitisTarihi { get; private set; }
    public DateTime? KullanimTarihi { get; private set; }

    public static TimeSpan GecerlilikSuresi(KodAmaci amac) =>
        amac == KodAmaci.EpostaDogrulama ? TimeSpan.FromHours(24) : TimeSpan.FromHours(1);

    public static KullaniciKodu Olustur(Guid kullaniciId, KodAmaci amac, string kodOzeti, DateTime simdi) => new()
    {
        KullaniciId = kullaniciId,
        Amac = amac,
        KodOzeti = kodOzeti,
        BitisTarihi = simdi + GecerlilikSuresi(amac)
    };

    public bool GecerliMi(KodAmaci amac, DateTime simdi) => Amac == amac && KullanimTarihi is null && simdi < BitisTarihi;

    public void Kullan(DateTime simdi) => KullanimTarihi ??= simdi;
}
