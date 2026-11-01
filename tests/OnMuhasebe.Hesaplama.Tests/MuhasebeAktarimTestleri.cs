using System.IO.Compression;
using System.Text;
using ExcelDataReader;
using OnMuhasebe.Modules.Muhasebe.Contracts;
using OnMuhasebe.Modules.Muhasebe.Domain;

namespace OnMuhasebe.Hesaplama.Tests;

public class MuhasebeAktarimTestleri
{
    private static readonly DateOnly Tarih = new(2026, 10, 15);

    public MuhasebeAktarimTestleri() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private static MuhasebeFisi Fis(
        string aciklama = "Alıcı AŞ - Satış", decimal tutar = 1200.5m, string belgeNo = "A1", string kod = "600", Guid? id = null,
        FisAktarimDurumu durum = FisAktarimDurumu.Aktarilmadi) =>
        new(id ?? Guid.Empty, FisKaynaklari.Fatura, "Satis", FisTuru.Mahsup, Tarih, belgeNo, aciklama, null,
            [new("120.01.1", "Alıcı AŞ", tutar, 0, aciklama), new(kod, "Satışlar", 0, tutar, aciklama)], durum);

    [Fact]
    public void Ozet_aciklama_ve_hesap_adindan_etkilenmez() =>
        Assert.Equal(FisOzeti.Hesapla(Fis()), FisOzeti.Hesapla(Fis(aciklama: "Yeni Ünvan AŞ - Satış")));

    [Fact]
    public void Ozet_tutar_hesap_kodu_ve_belge_no_degisince_degisir()
    {
        Assert.NotEqual(FisOzeti.Hesapla(Fis()), FisOzeti.Hesapla(Fis(tutar: 1200.51m)));
        Assert.NotEqual(FisOzeti.Hesapla(Fis()), FisOzeti.Hesapla(Fis(kod: "601")));
        Assert.NotEqual(FisOzeti.Hesapla(Fis()), FisOzeti.Hesapla(Fis(belgeNo: "A2")));
    }

    [Fact]
    public void Ozet_ondalik_olcegine_bakmaz() =>
        Assert.Equal(FisOzeti.Hesapla(Fis(tutar: 100m)), FisOzeti.Hesapla(Fis(tutar: 100.00m)));

    [Fact]
    public void Parmak_siraya_bakmaz_icerige_bakar()
    {
        var a = Fis(id: Guid.NewGuid());
        var b = Fis(id: Guid.NewGuid(), tutar: 5);

        Assert.Equal(FisOzeti.Parmak([a, b]), FisOzeti.Parmak([b, a]));
        Assert.NotEqual(FisOzeti.Parmak([a, b]), FisOzeti.Parmak([a]));
    }

    [Fact]
    public void Csv_turkce_excel_bicimindedir()
    {
        var baytlar = FisDisaAktarici.Csv([Fis()]);
        var metin = Encoding.UTF8.GetString(baytlar);
        var satirlar = metin.TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(Encoding.UTF8.GetPreamble(), baytlar[..3]);
        Assert.Equal(3, satirlar.Length);
        Assert.StartsWith("Fiş No;Fiş Türü;Tarih", satirlar[0]);
        Assert.Equal("1;Mahsup;15.10.2026;A1;120.01.1;Alıcı AŞ;Alıcı AŞ - Satış;1200,50;0,00;Fatura;Yeni", satirlar[1]);
    }

    [Theory]
    [InlineData("=HYPERLINK(\"x\")")]
    [InlineData("+1+1")]
    [InlineData("-1")]
    [InlineData("@SUM(A1)")]
    public void Csv_formul_gibi_baslayan_metni_etkisizlestirir(string aciklama)
    {
        var satir = Encoding.UTF8.GetString(FisDisaAktarici.Csv([Fis(aciklama)])).Split("\r\n")[1];

        Assert.DoesNotContain(";" + aciklama.Replace("\"", "\"\"") + ";", satir);
        Assert.Contains("'" + aciklama[0], satir);
    }

    [Fact]
    public void Csv_ayiraci_ve_tirnagi_kacirir()
    {
        var satir = Encoding.UTF8.GetString(FisDisaAktarici.Csv([Fis("A;B \"C\"")])).Split("\r\n")[1];

        Assert.Contains("\"A;B \"\"C\"\"\"", satir);
    }

    [Fact]
    public void Excel_gercek_okuyucuyla_acilir_ve_degerler_tiplidir()
    {
        var fis = Fis(durum: FisAktarimDurumu.Degisti);
        using var akis = new MemoryStream(FisDisaAktarici.Excel([fis, fis]));
        using var okuyucu = ExcelReaderFactory.CreateOpenXmlReader(akis);

        Assert.True(okuyucu.Read());
        Assert.Equal("Fiş No", okuyucu.GetValue(0));
        Assert.Equal("Durum", okuyucu.GetValue(10));

        Assert.True(okuyucu.Read());
        Assert.Equal(1d, okuyucu.GetValue(0));
        Assert.Equal(new DateTime(2026, 10, 15), okuyucu.GetValue(2));
        Assert.Equal("120.01.1", okuyucu.GetValue(4));
        Assert.Equal(1200.5d, okuyucu.GetValue(7));
        Assert.Equal("Değişti", okuyucu.GetValue(10));

        Assert.True(okuyucu.Read());
        Assert.True(okuyucu.Read());
        Assert.Equal(2d, okuyucu.GetValue(0));

        Assert.True(okuyucu.Read());
        Assert.True(okuyucu.Read());
        Assert.Equal("Toplam", okuyucu.GetValue(6));
        Assert.Equal(2401d, okuyucu.GetValue(7));
        Assert.Equal(2401d, okuyucu.GetValue(8));
        Assert.False(okuyucu.Read());
    }

    [Fact]
    public void Excel_gecersiz_karakteri_ayiklar_gecerli_kalir()
    {
        using var akis = new MemoryStream(FisDisaAktarici.Excel([Fis("Kötü\u0001metin 😀 <&>")]));
        using var okuyucu = ExcelReaderFactory.CreateOpenXmlReader(akis);

        okuyucu.Read();
        okuyucu.Read();
        Assert.Equal("Kötümetin 😀 <&>", okuyucu.GetValue(6));
    }

    [Fact]
    public void Excel_bos_listede_de_gecerlidir()
    {
        var baytlar = FisDisaAktarici.Excel([]);
        using var zip = new ZipArchive(new MemoryStream(baytlar));

        Assert.NotNull(zip.GetEntry("xl/worksheets/sheet1.xml"));
        using var okuyucu = ExcelReaderFactory.CreateOpenXmlReader(new MemoryStream(baytlar));
        Assert.True(okuyucu.Read());
    }
}
