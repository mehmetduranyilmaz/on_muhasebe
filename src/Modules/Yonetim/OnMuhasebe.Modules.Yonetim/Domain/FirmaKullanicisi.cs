using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Domain.Entities;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Yonetim.Domain;

internal sealed class FirmaKullanicisi : FirmaEntity
{
    public static readonly IReadOnlyList<string> GecerliRoller = [Roller.Yonetici, Roller.Kullanici];

    private FirmaKullanicisi() { }

    private FirmaKullanicisi(Guid firmaId) : base(firmaId) { }

    public Guid KullaniciId { get; private set; }
    public string Rol { get; private set; } = null!;
    public bool AktifMi { get; private set; } = true;

    public Firma Firma { get; private set; } = null!;
    public Kullanici Kullanici { get; private set; } = null!;

    // firmaId yalnızca oturum dışı işlemlerde verilir; boş bırakılırsa oturumdaki firma atanır.
    public static FirmaKullanicisi Olustur(Guid kullaniciId, string rol, Guid? firmaId = null)
    {
        if (!GecerliRoller.Contains(rol))
        {
            throw new ArgumentException($"Geçersiz rol: {rol}", nameof(rol));
        }

        var uyelik = firmaId is null ? new FirmaKullanicisi() : new FirmaKullanicisi(firmaId.Value);
        uyelik.KullaniciId = kullaniciId;
        uyelik.Rol = rol;
        return uyelik;
    }

    // Pasifleştirilmiş üye yeniden davet edilip kabul edince üyelik davetteki rolle yeniden açılır.
    public void Aktiflestir(string rol)
    {
        AktifMi = true;
        Rol = rol;
    }

    public Sonuc Pasiflestir(Guid islemiYapanId)
    {
        if (KullaniciId == islemiYapanId)
        {
            return YonetimHatalari.KendiniPasiflestiremez;
        }

        AktifMi = false;
        return Sonuc.Basarili();
    }
}
