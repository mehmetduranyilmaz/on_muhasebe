using System.IO.Compression;
using System.Text;
using OnMuhasebe.Shared.Domain.Entities;

namespace OnMuhasebe.Modules.Dosya.Domain;

// Bir kayda eklenmiş dosya. İçerik depoda, burada yalnız bilgileri tutulur; silinen dosya SaklamaGunu boyunca geri alınabilir.
internal sealed class DosyaEki : FirmaEntity
{
    public const int SaklamaGunu = 30;

    private DosyaEki() { }

    public string KayitTuru { get; private set; } = null!;
    public Guid KayitId { get; private set; }
    public string Ad { get; private set; } = null!;
    public long Boyut { get; private set; }
    public string Ozet { get; private set; } = null!;
    public string DepoYolu { get; private set; } = null!;
    public string? Yukleyen { get; private set; }
    public DateTime? SilinmeTarihi { get; private set; }

    public static DosyaEki Olustur(string kayitTuru, Guid kayitId, string ad, long boyut, string ozet, string depoYolu, string? yukleyen) => new()
    {
        KayitTuru = kayitTuru,
        KayitId = kayitId,
        Ad = ad,
        Boyut = boyut,
        Ozet = ozet,
        DepoYolu = depoYolu,
        Yukleyen = yukleyen
    };

    public void Sil(DateTime simdi) => SilinmeTarihi ??= simdi;

    public void GeriAl() => SilinmeTarihi = null;
}

// Yüklenebilen türler: uzantı ve içeriğin ilk baytları birlikte denetlenir. Çalışabilen türler (html, svg, exe) kabul edilmez;
// dosya her zaman indirme olarak verilir.
internal static class DosyaTurleri
{
    private static readonly byte[] _pdf = "%PDF"u8.ToArray();
    private static readonly byte[] _jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] _png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] _eskiOffice = [0xD0, 0xCF, 0x11, 0xE0];

    public static string IcerikTuru(string ad) => Path.GetExtension(ad).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".xml" => "application/xml",
        _ => "application/octet-stream"
    };

    public static bool GecerliMi(string ad, Stream icerik)
    {
        var bas = new byte[64];
        icerik.Position = 0;
        var okunan = icerik.Read(bas);
        var ilk = bas.AsSpan(0, okunan);
        var gecerli = Path.GetExtension(ad).ToLowerInvariant() switch
        {
            ".pdf" => ilk.StartsWith(_pdf),
            ".jpg" or ".jpeg" => ilk.StartsWith(_jpeg),
            ".png" => ilk.StartsWith(_png),
            ".doc" or ".xls" => ilk.StartsWith(_eskiOffice),
            ".docx" or ".xlsx" => OfficeMi(icerik),
            ".xml" => XmlMi(ilk),
            _ => false
        };
        icerik.Position = 0;
        return gecerli;
    }

    // Kullanıcının verdiği ad yalnız görüntülenir: klasör parçaları ve denetim karakterleri atılır.
    public static string AdTemizle(string ad)
    {
        var yalin = ad.Replace('\\', '/').Split('/')[^1];
        var temiz = new StringBuilder(yalin.Length);
        foreach (var c in yalin)
        {
            if (!char.IsControl(c))
            {
                temiz.Append(c);
            }
        }

        var sonuc = temiz.ToString().Trim();
        if (sonuc.Length <= 200)
        {
            return sonuc;
        }

        var uzanti = Path.GetExtension(sonuc);
        return sonuc[..(200 - uzanti.Length)] + uzanti;
    }

    private static bool OfficeMi(Stream icerik)
    {
        try
        {
            icerik.Position = 0;
            using var zip = new ZipArchive(icerik, ZipArchiveMode.Read, leaveOpen: true);
            return zip.GetEntry("[Content_Types].xml") is not null;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static bool XmlMi(ReadOnlySpan<byte> ilk)
    {
        if (ilk.StartsWith(Encoding.UTF8.Preamble))
        {
            ilk = ilk[3..];
        }

        ilk = ilk.TrimStart(" \t\r\n"u8);
        return ilk.Length > 0 && ilk[0] == (byte)'<';
    }
}
