using System.Globalization;
using System.Text.Json.Serialization;

namespace OnMuhasebe.Modules.Stok.Contracts;

// Ürün izlenebilirliği: lot/parti (gıda, kimya, kozmetik), seri no (elektronik, beyaz eşya; garanti ve iade takibi).
// İlaçta (İTS) lot + seri + SKT birlikte izlenir; seri kartında lot isteğe bağlıdır.
[JsonConverter(typeof(JsonStringEnumConverter<IzlemeTuru>))]
public enum IzlemeTuru
{
    Yok,
    Lot,
    Seri
}

// Bir hareketin izlenebilirlik bilgisi. Seri takibinde seri sayısı temel birimdeki miktara eşit olmalıdır.
public sealed record Izlenebilirlik(string? LotNo, DateOnly? SonKullanmaTarihi, IReadOnlyList<string>? SeriNolari)
{
    public static Izlenebilirlik Bos { get; } = new(null, null, null);
}

// Depodaki lot bakiyesi; çıkışta SKT'si en yakın lot önce önerilir (FEFO).
public sealed record LotBakiyesi(
    Guid StokKartiId,
    string StokKodu,
    string StokAdi,
    string TemelBirim,
    Guid DepoId,
    string DepoAdi,
    string LotNo,
    DateOnly? SonKullanmaTarihi,
    DateOnly IlkGiris,
    decimal Miktar);

public sealed record StoktakiSeri(string SeriNo, Guid DepoId, string DepoAdi, string? LotNo, DateOnly? SonKullanmaTarihi);

// Seri numarasının geçmişi (hangi belgeyle girdi, kime satıldı, nereye transfer edildi).
public sealed record SeriHareketi(
    DateOnly Tarih,
    StokHareketTuru Tur,
    Guid StokKartiId,
    string StokKodu,
    string StokAdi,
    string DepoAdi,
    bool GirisMi,
    string? LotNo,
    string? BelgeNo,
    string? Aciklama,
    Guid? KaynakBelgeId,
    bool IptalMi);

[JsonConverter(typeof(JsonStringEnumConverter<SktDurumu>))]
public enum SktDurumu
{
    Yok,
    Gecerli,
    Yaklasiyor,
    Gecmis
}

public static class IzlenebilirlikKurallari
{
    public const int LotUzunlugu = 50;
    public const int SeriUzunlugu = 100;

    // Gıda/ilaçta raf ömrü uyarısı için yaygın eşik.
    public const int VarsayilanUyariGunu = 30;

    // Satır başına, virgül ya da noktalı virgülle ayrılmış seri listesi; boşluklar kırpılır, tekrarlar atılır.
    public static List<string> SeriListesi(string? metin) =>
        string.IsNullOrWhiteSpace(metin)
            ? []
            : metin.Split(['\n', '\r', ',', ';', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal).ToList();

    public static SktDurumu Durum(DateOnly? skt, DateOnly bugun, int uyariGunu = VarsayilanUyariGunu) => skt switch
    {
        null => SktDurumu.Yok,
        { } t when t < bugun => SktDurumu.Gecmis,
        { } t when t <= bugun.AddDays(uyariGunu) => SktDurumu.Yaklasiyor,
        _ => SktDurumu.Gecerli
    };
}

// GS1 uygulama tanımlayıcılarıyla kodlanmış barkod/karekod (GS1-128, GS1 DataMatrix; İTS ilaç karekodu):
// (01) GTIN, (17) son kullanma, (15) tavsiye edilen tüketim, (10) lot, (21) seri. Değişken uzunluklu alanlar GS (0x1D) ile biter.
public sealed record Gs1Kodu(string Gtin, DateOnly? SonKullanmaTarihi, string? LotNo, string? SeriNo);

public static class Gs1Ayrastirici
{
    private const char Ayirac = '\u001d';

    public static Gs1Kodu? Ayristir(string? deger)
    {
        if (string.IsNullOrWhiteSpace(deger))
        {
            return null;
        }

        // Okuyucular FNC1'i "]d2"/"]C1" ön ekiyle, ayıracı GS ya da insan okunur parantezli biçimde verebilir.
        var metin = deger.Trim();
        if (metin.StartsWith(']') && metin.Length > 3)
        {
            metin = metin[3..];
        }

        if (metin.StartsWith('('))
        {
            metin = ParantezliyiCevir(metin);
        }

        if (!metin.StartsWith("01", StringComparison.Ordinal) || metin.Length < 16 || !metin[2..16].All(char.IsAsciiDigit))
        {
            return null;
        }

        string gtin = metin[2..16];
        DateOnly? skt = null, tuketim = null;
        string? lot = null, seri = null;
        var i = 16;
        while (i < metin.Length)
        {
            if (metin[i] == Ayirac)
            {
                i++;
                continue;
            }

            if (i + 2 > metin.Length)
            {
                return null;
            }

            var ai = metin.Substring(i, 2);
            i += 2;
            switch (ai)
            {
                case "17" or "15" or "11" or "13":
                    if (i + 6 > metin.Length || Tarih(metin.Substring(i, 6)) is not { } tarih)
                    {
                        return null;
                    }

                    if (ai == "17")
                    {
                        skt = tarih;
                    }
                    else if (ai == "15")
                    {
                        tuketim = tarih;
                    }

                    i += 6;
                    break;
                case "10" or "21":
                    var son = metin.IndexOf(Ayirac, i);
                    var alan = son < 0 ? metin[i..] : metin[i..son];
                    if (alan.Length is 0 or > 20)
                    {
                        return null;
                    }

                    if (ai == "10")
                    {
                        lot = alan;
                    }
                    else
                    {
                        seri = alan;
                    }

                    i += alan.Length;
                    break;
                default:
                    // Desteklenmeyen tanımlayıcıdan sonrası güvenle ayrıştırılamaz; bilinenler korunur.
                    i = metin.Length;
                    break;
            }
        }

        return new Gs1Kodu(gtin, skt ?? tuketim, lot, seri);
    }

    // GTIN-14'ün başındaki sıfır atılınca EAN-13 olur; kartta genellikle EAN-13 kayıtlıdır.
    public static IReadOnlyList<string> BarkodAdaylari(string gtin) =>
        gtin.Length == 14 && gtin[0] == '0' ? [gtin, gtin[1..]] : [gtin];

    // YYAAGG; gün 00 ise ayın son günüdür (GS1 genel şartnamesi). Yıl 2000+ kabul edilir.
    private static DateOnly? Tarih(string yyaagg)
    {
        if (!int.TryParse(yyaagg[..2], NumberStyles.None, CultureInfo.InvariantCulture, out var yil)
            || !int.TryParse(yyaagg[2..4], NumberStyles.None, CultureInfo.InvariantCulture, out var ay)
            || !int.TryParse(yyaagg[4..6], NumberStyles.None, CultureInfo.InvariantCulture, out var gun)
            || ay is < 1 or > 12)
        {
            return null;
        }

        var tamYil = 2000 + yil;
        var sonGun = DateTime.DaysInMonth(tamYil, ay);
        return gun == 0 ? new DateOnly(tamYil, ay, sonGun) : gun <= sonGun ? new DateOnly(tamYil, ay, gun) : null;
    }

    private static string ParantezliyiCevir(string metin)
    {
        var parcalar = new List<string>();
        var i = 0;
        while (i < metin.Length && metin[i] == '(')
        {
            var kapanis = metin.IndexOf(')', i);
            if (kapanis < 0)
            {
                break;
            }

            var ai = metin[(i + 1)..kapanis];
            var sonraki = metin.IndexOf('(', kapanis);
            var deger = sonraki < 0 ? metin[(kapanis + 1)..] : metin[(kapanis + 1)..sonraki];
            parcalar.Add(ai + deger + (ai is "10" or "21" ? Ayirac.ToString() : ""));
            i = sonraki < 0 ? metin.Length : sonraki;
        }

        return string.Concat(parcalar).TrimEnd(Ayirac);
    }
}
