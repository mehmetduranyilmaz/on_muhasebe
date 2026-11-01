using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using OnMuhasebe.Modules.Muhasebe.Contracts;

namespace OnMuhasebe.Modules.Muhasebe.Domain;

// Yevmiye fiş listesi dosyaları: fişin her satırı bir dosya satırıdır, aynı fişin satırları aynı Fiş No'yu taşır.
internal static class FisDisaAktarici
{
    private const string SayfaAdAlani = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const int BaslikStili = 1;
    private const int TarihStili = 2;
    private const int TutarStili = 3;
    private const int ToplamStili = 4;

    private static readonly string[] _basliklar =
        ["Fiş No", "Fiş Türü", "Tarih", "Belge No", "Hesap Kodu", "Hesap Adı", "Açıklama", "Borç", "Alacak", "Kaynak", "Durum"];

    private static readonly int[] _genislikler = [8, 10, 12, 20, 18, 32, 50, 14, 14, 12, 12];

    private sealed record Satir(int FisNo, MuhasebeFisi Fis, FisSatiri Hesap);

    private static IEnumerable<Satir> Satirlar(IEnumerable<MuhasebeFisi> fisler) =>
        fisler.SelectMany((f, i) => f.Satirlar.Select(s => new Satir(i + 1, f, s)));

    private static string KaynakAdi(string kaynak) => kaynak switch
    {
        FisKaynaklari.Fatura => "Fatura",
        FisKaynaklari.KasaBanka => "Kasa/Banka",
        FisKaynaklari.CekSenet => "Çek/Senet",
        _ => "Tahakkuk"
    };

    private static string DurumAdi(FisAktarimDurumu durum) => durum switch
    {
        FisAktarimDurumu.Aktarildi => "Aktarıldı",
        FisAktarimDurumu.Degisti => "Değişti",
        _ => "Yeni"
    };

    // Türkçe Excel'in doğrudan açtığı biçim: UTF-8 BOM, noktalı virgül, virgüllü ondalık.
    public static byte[] Csv(IEnumerable<MuhasebeFisi> fisler)
    {
        var metin = new StringBuilder();
        metin.Append(string.Join(';', _basliklar.Select(CsvHucre))).Append("\r\n");
        foreach (var s in Satirlar(fisler))
        {
            metin.Append(string.Join(';',
                s.FisNo.ToString(CultureInfo.InvariantCulture), CsvHucre(s.Fis.Tur.ToString()), s.Fis.Tarih.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                CsvHucre(s.Fis.BelgeNo), CsvHucre(s.Hesap.HesapKodu), CsvHucre(s.Hesap.HesapAdi), CsvHucre(s.Hesap.Aciklama),
                CsvTutar(s.Hesap.Borc), CsvTutar(s.Hesap.Alacak), CsvHucre(KaynakAdi(s.Fis.Kaynak)), CsvHucre(DurumAdi(s.Fis.AktarimDurumu))))
                .Append("\r\n");
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(metin.ToString())];
    }

    // Sunucu kültüründen bağımsız: Docker imajı Türkçe kültür verisi taşımayabilir.
    private static string CsvTutar(decimal tutar) => tutar.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');

    // Ünvan ve açıklama kullanıcı girdisidir: formül gibi başlayan metin Excel'de çalışmasın diye tek tırnakla etkisizleştirilir.
    private static string CsvHucre(string? deger)
    {
        var metin = deger ?? "";
        if (metin.Length > 0 && metin[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            metin = "'" + metin;
        }

        return metin.IndexOfAny([';', '"', '\r', '\n']) >= 0 ? $"\"{metin.Replace("\"", "\"\"")}\"" : metin;
    }

    public static byte[] Excel(IEnumerable<MuhasebeFisi> fisler)
    {
        using var bellek = new MemoryStream();
        using (var zip = new ZipArchive(bellek, ZipArchiveMode.Create, leaveOpen: true))
        {
            Yaz(zip, "[Content_Types].xml", """<?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>""");
            Yaz(zip, "_rels/.rels", """<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            Yaz(zip, "xl/workbook.xml", """<?xml version="1.0" encoding="UTF-8"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Yevmiye" sheetId="1" r:id="rId1"/></sheets></workbook>""");
            Yaz(zip, "xl/_rels/workbook.xml.rels", """<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>""");
            Yaz(zip, "xl/styles.xml", """<?xml version="1.0" encoding="UTF-8"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><numFmts count="1"><numFmt numFmtId="164" formatCode="dd\.mm\.yyyy"/></numFmts><fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts><fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="5"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/><xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="4" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="4" fontId="1" fillId="0" borderId="0" xfId="0" applyNumberFormat="1" applyFont="1"/></cellXfs></styleSheet>""");
            CalismaSayfasi(zip, fisler);
        }

        return bellek.ToArray();
    }

    private static void Yaz(ZipArchive zip, string ad, string icerik)
    {
        using var akis = zip.CreateEntry(ad, CompressionLevel.Fastest).Open();
        akis.Write(new UTF8Encoding(false).GetBytes(icerik));
    }

    private static void CalismaSayfasi(ZipArchive zip, IEnumerable<MuhasebeFisi> fisler)
    {
        using var akis = zip.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Fastest).Open();
        using var x = XmlWriter.Create(akis, new XmlWriterSettings { Encoding = new UTF8Encoding(false) });
        x.WriteStartDocument();
        x.WriteStartElement("worksheet", SayfaAdAlani);
        BaslikSabitle(x);
        Genislikler(x);

        x.WriteStartElement("sheetData");
        var satirNo = 1;
        x.WriteStartElement("row");
        for (var i = 0; i < _basliklar.Length; i++)
        {
            Metin(x, i, satirNo, _basliklar[i], BaslikStili);
        }

        x.WriteEndElement();

        decimal toplamBorc = 0, toplamAlacak = 0;
        foreach (var s in Satirlar(fisler))
        {
            satirNo++;
            toplamBorc += s.Hesap.Borc;
            toplamAlacak += s.Hesap.Alacak;
            x.WriteStartElement("row");
            Sayi(x, 0, satirNo, s.FisNo, 0);
            Metin(x, 1, satirNo, s.Fis.Tur.ToString());
            Sayi(x, 2, satirNo, (decimal)s.Fis.Tarih.ToDateTime(TimeOnly.MinValue).ToOADate(), TarihStili);
            Metin(x, 3, satirNo, s.Fis.BelgeNo);
            Metin(x, 4, satirNo, s.Hesap.HesapKodu);
            Metin(x, 5, satirNo, s.Hesap.HesapAdi);
            Metin(x, 6, satirNo, s.Hesap.Aciklama);
            Sayi(x, 7, satirNo, s.Hesap.Borc, TutarStili);
            Sayi(x, 8, satirNo, s.Hesap.Alacak, TutarStili);
            Metin(x, 9, satirNo, KaynakAdi(s.Fis.Kaynak));
            Metin(x, 10, satirNo, DurumAdi(s.Fis.AktarimDurumu));
            x.WriteEndElement();
        }

        var sonVeriSatiri = satirNo;
        satirNo++;
        x.WriteStartElement("row");
        Metin(x, 6, satirNo, "Toplam", BaslikStili);
        Sayi(x, 7, satirNo, toplamBorc, ToplamStili);
        Sayi(x, 8, satirNo, toplamAlacak, ToplamStili);
        x.WriteEndElement();
        x.WriteEndElement();

        x.WriteStartElement("autoFilter");
        x.WriteAttributeString("ref", $"A1:K{sonVeriSatiri.ToString(CultureInfo.InvariantCulture)}");
        x.WriteEndElement();

        x.WriteEndElement();
        x.WriteEndDocument();
    }

    // Başlık satırı kaydırılırken sabit kalır.
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

    private static void Genislikler(XmlWriter x)
    {
        x.WriteStartElement("cols");
        for (var i = 0; i < _genislikler.Length; i++)
        {
            x.WriteStartElement("col");
            x.WriteAttributeString("min", (i + 1).ToString(CultureInfo.InvariantCulture));
            x.WriteAttributeString("max", (i + 1).ToString(CultureInfo.InvariantCulture));
            x.WriteAttributeString("width", _genislikler[i].ToString(CultureInfo.InvariantCulture));
            x.WriteAttributeString("customWidth", "1");
            x.WriteEndElement();
        }

        x.WriteEndElement();
    }

    private static string Hucre(int sutun, int satir) => $"{(char)('A' + sutun)}{satir.ToString(CultureInfo.InvariantCulture)}";

    private static void Metin(XmlWriter x, int sutun, int satir, string? deger, int stil = 0)
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
        x.WriteString(XmlIcinTemizle(deger ?? ""));
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
