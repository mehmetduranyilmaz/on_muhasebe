using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace OnMuhasebe.Shared.Infrastructure.Excel;

public sealed record ExcelSutunu(string Baslik, int Genislik = 18);

// Bağımlılıksız .xlsx yazıcı: tek sayfa, kalın ve sabit başlık, filtre, isteğe bağlı kalın alt satır.
// Hücre değeri string, int, decimal, DateOnly ya da null olabilir.
public static class ExcelYazici
{
    private const string SayfaAdAlani = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const int BaslikStili = 1;
    private const int TarihStili = 2;
    private const int TutarStili = 3;
    private const int ToplamStili = 4;

    public static byte[] Yaz(string sayfaAdi, IReadOnlyList<ExcelSutunu> sutunlar, IEnumerable<object?[]> satirlar, object?[]? altSatir = null)
    {
        using var bellek = new MemoryStream();
        using (var zip = new ZipArchive(bellek, ZipArchiveMode.Create, leaveOpen: true))
        {
            ParcaYaz(zip, "[Content_Types].xml", """<?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>""");
            ParcaYaz(zip, "_rels/.rels", """<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            ParcaYaz(zip, "xl/workbook.xml", $"""<?xml version="1.0" encoding="UTF-8"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="{sayfaAdi}" sheetId="1" r:id="rId1"/></sheets></workbook>""");
            ParcaYaz(zip, "xl/_rels/workbook.xml.rels", """<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>""");
            ParcaYaz(zip, "xl/styles.xml", """<?xml version="1.0" encoding="UTF-8"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><numFmts count="1"><numFmt numFmtId="164" formatCode="dd\.mm\.yyyy"/></numFmts><fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts><fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="5"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/><xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="4" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="4" fontId="1" fillId="0" borderId="0" xfId="0" applyNumberFormat="1" applyFont="1"/></cellXfs></styleSheet>""");
            CalismaSayfasi(zip, sutunlar, satirlar, altSatir);
        }

        return bellek.ToArray();
    }

    private static void ParcaYaz(ZipArchive zip, string ad, string icerik)
    {
        using var akis = zip.CreateEntry(ad, CompressionLevel.Fastest).Open();
        akis.Write(new UTF8Encoding(false).GetBytes(icerik));
    }

    private static void CalismaSayfasi(ZipArchive zip, IReadOnlyList<ExcelSutunu> sutunlar, IEnumerable<object?[]> satirlar, object?[]? altSatir)
    {
        using var akis = zip.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Fastest).Open();
        using var x = XmlWriter.Create(akis, new XmlWriterSettings { Encoding = new UTF8Encoding(false) });
        x.WriteStartDocument();
        x.WriteStartElement("worksheet", SayfaAdAlani);
        BaslikSabitle(x);
        Genislikler(x, sutunlar);

        x.WriteStartElement("sheetData");
        var satirNo = 1;
        SatirYaz(x, satirNo, sutunlar.Select(s => (object?)s.Baslik).ToArray(), kalin: true);
        foreach (var satir in satirlar)
        {
            SatirYaz(x, ++satirNo, satir, kalin: false);
        }

        var sonVeriSatiri = satirNo;
        if (altSatir is not null)
        {
            SatirYaz(x, ++satirNo, altSatir, kalin: true);
        }

        x.WriteEndElement();

        x.WriteStartElement("autoFilter");
        x.WriteAttributeString("ref", $"A1:{SutunAdi(sutunlar.Count - 1)}{sonVeriSatiri.ToString(CultureInfo.InvariantCulture)}");
        x.WriteEndElement();

        x.WriteEndElement();
        x.WriteEndDocument();
    }

    private static void BaslikSabitle(XmlWriter x)
    {
        x.WriteStartElement("sheetViews");
        x.WriteStartElement("sheetView");
        x.WriteAttributeString("workbookViewId", "0");
        x.WriteStartElement("pane");
        x.WriteAttributeString("ySplit", "1");
        x.WriteAttributeString("topLeftCell", "A2");
        x.WriteAttributeString("activePane", "bottomLeft");
        x.WriteAttributeString("state", "frozen");
        x.WriteEndElement();
        x.WriteEndElement();
        x.WriteEndElement();
    }

    private static void Genislikler(XmlWriter x, IReadOnlyList<ExcelSutunu> sutunlar)
    {
        x.WriteStartElement("cols");
        for (var i = 0; i < sutunlar.Count; i++)
        {
            x.WriteStartElement("col");
            x.WriteAttributeString("min", (i + 1).ToString(CultureInfo.InvariantCulture));
            x.WriteAttributeString("max", (i + 1).ToString(CultureInfo.InvariantCulture));
            x.WriteAttributeString("width", sutunlar[i].Genislik.ToString(CultureInfo.InvariantCulture));
            x.WriteAttributeString("customWidth", "1");
            x.WriteEndElement();
        }

        x.WriteEndElement();
    }

    private static void SatirYaz(XmlWriter x, int satirNo, object?[] hucreler, bool kalin)
    {
        x.WriteStartElement("row");
        for (var i = 0; i < hucreler.Length; i++)
        {
            switch (hucreler[i])
            {
                case null:
                    break;
                case string metin:
                    Metin(x, i, satirNo, metin, kalin ? BaslikStili : 0);
                    break;
                case DateOnly tarih:
                    Sayi(x, i, satirNo, (decimal)tarih.ToDateTime(TimeOnly.MinValue).ToOADate(), TarihStili);
                    break;
                case int tam:
                    Sayi(x, i, satirNo, tam, 0);
                    break;
                case decimal tutar:
                    Sayi(x, i, satirNo, tutar, kalin ? ToplamStili : TutarStili);
                    break;
                default:
                    throw new ArgumentException($"Desteklenmeyen hücre türü: {hucreler[i]!.GetType().Name}");
            }
        }

        x.WriteEndElement();
    }

    // 26'dan fazla sütunda AA, AB...
    private static string SutunAdi(int sutun) =>
        sutun < 26 ? ((char)('A' + sutun)).ToString() : $"{(char)('A' + sutun / 26 - 1)}{(char)('A' + sutun % 26)}";

    private static string Hucre(int sutun, int satir) => $"{SutunAdi(sutun)}{satir.ToString(CultureInfo.InvariantCulture)}";

    private static void Metin(XmlWriter x, int sutun, int satir, string deger, int stil)
    {
        x.WriteStartElement("c");
        x.WriteAttributeString("r", Hucre(sutun, satir));
        if (stil != 0)
        {
            x.WriteAttributeString("s", stil.ToString(CultureInfo.InvariantCulture));
        }

        x.WriteAttributeString("t", "inlineStr");
        x.WriteStartElement("is");
        x.WriteStartElement("t");
        x.WriteAttributeString("xml", "space", null, "preserve");
        x.WriteString(XmlIcinTemizle(deger));
        x.WriteEndElement();
        x.WriteEndElement();
        x.WriteEndElement();
    }

    private static void Sayi(XmlWriter x, int sutun, int satir, decimal deger, int stil)
    {
        x.WriteStartElement("c");
        x.WriteAttributeString("r", Hucre(sutun, satir));
        if (stil != 0)
        {
            x.WriteAttributeString("s", stil.ToString(CultureInfo.InvariantCulture));
        }

        x.WriteElementString("v", SayfaAdAlani, deger.ToString(CultureInfo.InvariantCulture));
        x.WriteEndElement();
    }

    // XML'de bulunamayan denetim karakterleri (yapıştırılmış metinlerden gelebilir) dosyayı açılmaz hale getirir.
    private static string XmlIcinTemizle(string metin)
    {
        var temiz = new StringBuilder(metin.Length);
        for (var i = 0; i < metin.Length; i++)
        {
            if (char.IsHighSurrogate(metin[i]) && i + 1 < metin.Length && char.IsLowSurrogate(metin[i + 1]))
            {
                temiz.Append(metin[i]).Append(metin[++i]);
            }
            else if (XmlConvert.IsXmlChar(metin[i]))
            {
                temiz.Append(metin[i]);
            }
        }

        return temiz.ToString();
    }
}
