namespace OnMuhasebe.Shared.Contracts.Moduller;

public static class ModulKodlari
{
    public const string Cari = "cari";
    public const string Stok = "stok";
    public const string Fatura = "fatura";
    public const string KasaBanka = "kasabanka";

    // Mali müşavir köprüsü: belgelerin Tek Düzen Hesap Planı fişlerine çevrilip paket programlara aktarılması.
    public const string Muhasebe = "muhasebe";

    // Süreli evrak takibi (ruhsat, poliçe, sözleşme). Dosya eki bundan bağımsız, temel lisanstadır.
    public const string EvrakTakip = "evraktakip";

    // Araç kartı, sigorta/muayene/MTV süreleri, yakıt ve masraf takibi.
    public const string Arac = "arac";

    // Ekipman kartı, periyodik bakım ve yasal kontrol, arıza iş emirleri.
    public const string Bakim = "bakim";

    // Satın alma talebi ve tutara bağlı onay akışı.
    public const string Satinalma = "satinalma";

    // Personel kartı, net maaş, avans, masraf ve zimmet. Bordro hesabı yapmaz.
    public const string Personel = "personel";

    public static IReadOnlyList<string> Tumu { get; } = [Cari, Stok, Fatura, KasaBanka, Muhasebe, EvrakTakip, Arac, Bakim, Satinalma, Personel];
}
