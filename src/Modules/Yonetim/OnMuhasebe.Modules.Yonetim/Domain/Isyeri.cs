using OnMuhasebe.Shared.Domain.Entities;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Yonetim.Domain;

// Firmanın (tek VKN) faaliyet gösterdiği yer: merkez, şube, mağaza, depo/fabrika. Belge serisi, kasa ve depo işyerine bağlanır;
// cari ve stok kartları firma genelidir. SGK işyeri sicil numarası işyeri başına ayrıdır (26 hane).
internal sealed class Isyeri : FirmaEntity
{
    public const string MerkezAdi = "Merkez";
    public const int SgkSicilUzunlugu = 26;

    private Isyeri() { }

    private Isyeri(Guid firmaId) : base(firmaId) { }

    public string Kod { get; private set; } = null!;
    public string Ad { get; private set; } = null!;
    public string? Adres { get; private set; }
    public string? Il { get; private set; }
    public string? Ilce { get; private set; }
    public string? Telefon { get; private set; }
    public string? SgkSicilNo { get; private set; }
    public bool MerkezMi { get; private set; }
    public bool AktifMi { get; private set; } = true;

    // firmaId yalnızca oturum dışı işlemlerde (kayıt, sistem yöneticisinin firma açması) verilir.
    public static Isyeri Olustur(IsyeriBilgileri b, Guid? firmaId = null)
    {
        var isyeri = firmaId is null ? new Isyeri() : new Isyeri(firmaId.Value);
        isyeri.Guncelle(b);
        return isyeri;
    }

    public static Isyeri MerkezOlustur(Guid? firmaId = null)
    {
        var isyeri = Olustur(new IsyeriBilgileri("MRK", MerkezAdi, null, null, null, null, null), firmaId);
        isyeri.MerkezMi = true;
        return isyeri;
    }

    public void Guncelle(IsyeriBilgileri b)
    {
        Kod = b.Kod.Trim().ToUpperInvariant();
        Ad = b.Ad.Trim();
        Adres = Temiz(b.Adres);
        Il = Temiz(b.Il);
        Ilce = Temiz(b.Ilce);
        Telefon = Temiz(b.Telefon);
        SgkSicilNo = Temiz(b.SgkSicilNo);
    }

    // Merkez kapatılamaz: varsayılan işyeri ve "işyeri seçilmedi" kayıtlarının yeri odur.
    public Sonuc Pasiflestir()
    {
        if (MerkezMi)
        {
            return YonetimHatalari.MerkezKapatilamaz;
        }

        AktifMi = false;
        return Sonuc.Basarili();
    }

    public void Aktiflestir() => AktifMi = true;

    // Merkez başka bir işyerine devredilir (taşınma): eski merkez sıradan işyeri olur.
    public void MerkezYap(bool merkezMi) => MerkezMi = merkezMi;

    private static string? Temiz(string? deger) => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();
}

internal sealed record IsyeriBilgileri(string Kod, string Ad, string? Adres, string? Il, string? Ilce, string? Telefon, string? SgkSicilNo);
