using OnMuhasebe.Modules.Muhasebe.Contracts;
using OnMuhasebe.Shared.Domain.Entities;

namespace OnMuhasebe.Modules.Muhasebe.Domain;

// Firma başına tek satır: alt hesap ayıracı ve cari alt grubu. Kayıt yoksa varsayılanlar geçerlidir.
internal sealed class MuhasebeAyari : FirmaEntity
{
    public const string VarsayilanAyirac = ".";
    public const string VarsayilanCariAltGrup = "01";

    private MuhasebeAyari() { }

    public string Ayirac { get; private set; } = VarsayilanAyirac;
    public string? CariAltGrup { get; private set; } = VarsayilanCariAltGrup;

    public static MuhasebeAyari Varsayilan() => new();

    public void Guncelle(string ayirac, string? cariAltGrup)
    {
        Ayirac = ayirac;
        CariAltGrup = string.IsNullOrWhiteSpace(cariAltGrup) ? null : cariAltGrup.Trim();
    }
}

// Firmanın varsayılandan farklı verdiği ana hesap kodu (yalnız değişenler saklanır).
internal sealed class HesapKodu : FirmaEntity
{
    private HesapKodu() { }

    public HesapRolu Rol { get; private set; }
    public string Kod { get; private set; } = null!;

    public static HesapKodu Olustur(HesapRolu rol, string kod) => new() { Rol = rol, Kod = kod.Trim() };

    public void Degistir(string kod) => Kod = kod.Trim();
}

// Kartına elle verilen hesap kodu (otomatik kodun yerine geçer).
internal sealed class KodEslemesi : FirmaEntity
{
    private KodEslemesi() { }

    public KartTuru KartTuru { get; private set; }
    public Guid KartId { get; private set; }
    public string Kod { get; private set; } = null!;

    public static KodEslemesi Olustur(KartTuru tur, Guid kartId, string kod) => new() { KartTuru = tur, KartId = kartId, Kod = kod.Trim() };

    public void Degistir(string kod) => Kod = kod.Trim();
}

// Otomatik hesap kodları. Cari: ana + alt grup + carinin numarası (C00012 -> 120.01.00012); hem müşteri hem tedarikçi olan
// cari müşteri tarafında 120'ye, tedarikçi tarafında 320'ye yazılır. Kasa/banka: ana + hesabın numarası (BN0003 -> 102.0003).
// Kategori: gelir ise diğer gelirler, gider ise genel yönetim giderleri ana hesabı. Kod rakam içermiyorsa kodun tamamı kullanılır.
internal sealed class HesapPlani(IReadOnlyDictionary<HesapRolu, string> kodlar, string ayirac, string? cariAltGrup)
{
    public string Kod(HesapRolu rol) => kodlar.TryGetValue(rol, out var kod) ? kod : HesapRolleri.Varsayilan(rol);

    public string Alt(HesapRolu rol, params string?[] parcalar) =>
        string.Join(ayirac, new[] { Kod(rol) }.Concat(parcalar.Where(p => !string.IsNullOrEmpty(p)))!);

    public string CariKodu(string kartKodu, bool tedarikciMi) =>
        Alt(tedarikciMi ? HesapRolu.Saticilar : HesapRolu.Alicilar, cariAltGrup, Numara(kartKodu));

    public string HesapKoduOtomatik(string kartKodu, string tur) => Alt(tur switch
    {
        "Kasa" => HesapRolu.Kasa,
        "Banka" => HesapRolu.Bankalar,
        "KrediKarti" => HesapRolu.KrediKartlari,
        _ => HesapRolu.PosAlacaklari
    }, Numara(kartKodu));

    public string KategoriKodu(string tur) => Kod(tur == "Gelir" ? HesapRolu.DigerGelirler : HesapRolu.GenelYonetimGiderleri);

    // C00012 -> 00012; rakamı olmayan kod olduğu gibi kalır.
    internal static string Numara(string kartKodu)
    {
        var rakamlar = new string(kartKodu.Where(char.IsAsciiDigit).ToArray());
        return rakamlar.Length > 0 ? rakamlar : kartKodu;
    }
}
