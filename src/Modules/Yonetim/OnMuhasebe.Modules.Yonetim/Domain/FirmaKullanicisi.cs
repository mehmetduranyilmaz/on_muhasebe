using OnMuhasebe.Shared.Domain.Entities;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Yonetim.Domain;

internal sealed class FirmaKullanicisi : FirmaEntity
{
    private FirmaKullanicisi() { }

    private FirmaKullanicisi(Guid firmaId) : base(firmaId) { }

    public Guid KullaniciId { get; private set; }
    public Guid RolId { get; private set; }
    public bool AktifMi { get; private set; } = true;

    // Rolün üzerine bu kullanıcıya özel verilen ve alınan yetkiler.
    public List<string> EkYetkiler { get; private set; } = [];
    public List<string> YasakYetkiler { get; private set; } = [];

    // Girebildiği işyerleri (boş: tümü) ve yeni belgelerin varsayılan işyeri.
    public List<Guid> Isyerleri { get; private set; } = [];
    public Guid? VarsayilanIsyeriId { get; private set; }

    public Firma Firma { get; private set; } = null!;
    public Kullanici Kullanici { get; private set; } = null!;
    public FirmaRolu Rol { get; private set; } = null!;

    // firmaId yalnızca oturum dışı işlemlerde verilir; boş bırakılırsa oturumdaki firma atanır.
    public static FirmaKullanicisi Olustur(Guid kullaniciId, Guid rolId, Guid? firmaId = null)
    {
        var uyelik = firmaId is null ? new FirmaKullanicisi() : new FirmaKullanicisi(firmaId.Value);
        uyelik.KullaniciId = kullaniciId;
        uyelik.RolId = rolId;
        return uyelik;
    }

    // Pasifleştirilmiş üye yeniden davet edilip kabul edince üyelik davetteki rolle, özel yetkiler sıfırlanarak açılır.
    public void Aktiflestir(Guid rolId)
    {
        AktifMi = true;
        YetkileriAta(rolId, [], []);
        IsyerleriniAta([], null);
    }

    public void YetkileriAta(Guid rolId, IEnumerable<string> ek, IEnumerable<string> yasak)
    {
        RolId = rolId;
        EkYetkiler = ek.Distinct().Order().ToList();
        YasakYetkiler = yasak.Distinct().Except(EkYetkiler).Order().ToList();
    }

    public void IsyerleriniAta(IEnumerable<Guid> isyerleri, Guid? varsayilan)
    {
        Isyerleri = isyerleri.Distinct().ToList();
        VarsayilanIsyeriId = varsayilan is { } v && (Isyerleri.Count == 0 || Isyerleri.Contains(v)) ? v : null;
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
