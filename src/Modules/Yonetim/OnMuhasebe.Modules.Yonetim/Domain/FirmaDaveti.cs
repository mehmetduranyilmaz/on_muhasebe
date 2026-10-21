using OnMuhasebe.Shared.Domain.Entities;

namespace OnMuhasebe.Modules.Yonetim.Domain;

// Yöneticinin firmaya kullanıcı daveti. Üyelik, davet e-postasındaki bağlantıyla (e-postanın sahibi) kabul edilince oluşur;
// böylece kimse başkasının e-postasıyla açtığı hesap üzerinden firmaya sızamaz ve kimse rızası olmadan firmaya eklenmez.
internal sealed class FirmaDaveti : FirmaEntity
{
    public static readonly TimeSpan GecerlilikSuresi = TimeSpan.FromDays(7);

    private FirmaDaveti() { }

    private FirmaDaveti(Guid firmaId) : base(firmaId) { }

    public string Eposta { get; private set; } = null!;
    public string Rol { get; private set; } = null!;
    public string KodOzeti { get; private set; } = null!;
    public DateTime BitisTarihi { get; private set; }
    public DateTime SonGonderimTarihi { get; private set; }
    public DateTime? KabulTarihi { get; private set; }
    public DateTime? IptalTarihi { get; private set; }

    public Firma Firma { get; private set; } = null!;

    // firmaId yalnızca oturum dışı işlemlerde (sistem yöneticisinin firma açması) verilir; boşsa oturumdaki firma atanır.
    public static FirmaDaveti Olustur(string eposta, string rol, string kodOzeti, DateTime simdi, Guid? firmaId = null)
    {
        if (!FirmaKullanicisi.GecerliRoller.Contains(rol))
        {
            throw new ArgumentException($"Geçersiz rol: {rol}", nameof(rol));
        }

        var davet = firmaId is null ? new FirmaDaveti() : new FirmaDaveti(firmaId.Value);
        davet.Eposta = Kullanici.EpostaNormalize(eposta);
        davet.Rol = rol;
        davet.KodOzeti = kodOzeti;
        davet.BitisTarihi = simdi + GecerlilikSuresi;
        davet.SonGonderimTarihi = simdi;
        return davet;
    }

    public bool BekliyorMu(DateTime simdi) => KabulTarihi is null && IptalTarihi is null && simdi < BitisTarihi;

    public void YenidenGonder(string kodOzeti, DateTime simdi)
    {
        KodOzeti = kodOzeti;
        BitisTarihi = simdi + GecerlilikSuresi;
        SonGonderimTarihi = simdi;
    }

    public void KabulEt(DateTime simdi) => KabulTarihi ??= simdi;

    public void IptalEt(DateTime simdi) => IptalTarihi ??= simdi;
}
