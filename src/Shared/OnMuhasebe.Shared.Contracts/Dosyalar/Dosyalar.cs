using OnMuhasebe.Shared.Contracts.Kimlik;

namespace OnMuhasebe.Shared.Contracts.Dosyalar;

// Dosya eki temel lisanstadır; sınırı paketin depolama kotasıdır. Ayrıca ekin bağlı olduğu kaydı görme yetkisi aranır.
public static class DosyaYetkileri
{
    public static readonly YetkiKaynagi Ek = new("dosya.ek", "", "Dosyalar", "Kayıtlara eklenen dosyalar",
        [YetkiIslemleri.Goruntule, YetkiIslemleri.Ekle, YetkiIslemleri.Sil], []);

    public static IReadOnlyList<YetkiKaynagi> Tumu { get; } = [Ek];
}

// Dosya eklenebilen kayıt türleri. Kaydın sahibi modül, kaydı görme kuralını kendisi bildirir.
public static class DosyaKayitTurleri
{
    public const string Cari = "cari";
    public const string Fatura = "fatura";
    public const string Stok = "stok";
    public const string CekSenet = "cek-senet";
    public const string Kredi = "kredi";
    public const string Demirbas = "demirbas";
    public const string Teminat = "teminat";
}

public static class DosyaSinirlari
{
    public const long AzamiBoyut = 20 * 1024 * 1024;
    public const string KabulEdilenler = ".pdf,.jpg,.jpeg,.png,.doc,.docx,.xls,.xlsx,.xml";
}

// SilinmeTarihi doluysa dosya çöp kutusundadır: SaklamaGunu sonunda kalıcı silinir.
public sealed record DosyaEkiOzeti(Guid Id, string Ad, long Boyut, DateTime YuklemeTarihi, string? Yukleyen, DateTime? SilinmeTarihi);

// KotaBayt boşsa paket sınırsızdır.
public sealed record DepolamaKullanimi(long KullanilanBayt, long? KotaBayt);
