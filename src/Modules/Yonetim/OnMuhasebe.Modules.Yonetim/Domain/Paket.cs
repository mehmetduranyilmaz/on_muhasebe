using OnMuhasebe.Shared.Domain.Entities;

namespace OnMuhasebe.Modules.Yonetim.Domain;

// Satılan paket. Ücretler ve limitler sistem yöneticisinin ekranından girilir; kodda sabit değildir.
internal sealed class Paket : BaseEntity
{
    private Paket() { }

    public string Kod { get; private set; } = null!;
    public string Ad { get; private set; } = null!;
    public string? Aciklama { get; private set; }

    // Deneme paketi gibi, ileride eklenen modülleri de otomatik kapsayan paketler için.
    public bool TumModullerMi { get; private set; }
    public List<string> Moduller { get; private set; } = [];

    // Boş: sınırsız. Firmanın aktif kullanıcı sayısı bunu aşamaz.
    public int? KullaniciLimiti { get; private set; }

    // Boş: sınırsız. Firmanın aktif işyeri sayısı bunu aşamaz.
    public int? IsyeriLimiti { get; private set; }

    // Dosya eki depolama kotası (MB); boşsa sınırsız.
    public int? DepolamaKotasiMb { get; private set; }

    // KDV dahil TL; boş bırakılan süre seçeneği abonelik ekranında sunulmaz.
    public decimal? AylikUcret { get; private set; }
    public decimal? YillikUcret { get; private set; }

    // Firmaların abonelik ekranında listelenir mi (deneme gibi paketler listelenmez).
    public bool SatistaMi { get; private set; }
    public int Sira { get; private set; }

    public static Paket Olustur(string kod, PaketBilgileri bilgiler)
    {
        var paket = new Paket { Kod = kod.Trim().ToLowerInvariant() };
        paket.Guncelle(bilgiler);
        return paket;
    }

    public void Guncelle(PaketBilgileri b)
    {
        Ad = b.Ad.Trim();
        Aciklama = string.IsNullOrWhiteSpace(b.Aciklama) ? null : b.Aciklama.Trim();
        TumModullerMi = b.TumModullerMi;
        Moduller = b.TumModullerMi ? [] : b.Moduller.Distinct().ToList();
        KullaniciLimiti = b.KullaniciLimiti;
        IsyeriLimiti = b.IsyeriLimiti;
        DepolamaKotasiMb = b.DepolamaKotasiMb;
        AylikUcret = b.AylikUcret;
        YillikUcret = b.YillikUcret;
        SatistaMi = b.SatistaMi;
        Sira = b.Sira;
    }

    public decimal? Ucret(int aySayisi) => aySayisi switch
    {
        1 => AylikUcret,
        12 => YillikUcret,
        _ => null
    };
}

internal sealed record PaketBilgileri(
    string Ad, string? Aciklama, bool TumModullerMi, IReadOnlyList<string> Moduller, int? KullaniciLimiti,
    decimal? AylikUcret, decimal? YillikUcret, bool SatistaMi, int Sira, int? IsyeriLimiti = null, int? DepolamaKotasiMb = null);
