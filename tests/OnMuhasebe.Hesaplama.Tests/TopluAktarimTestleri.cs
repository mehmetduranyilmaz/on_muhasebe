using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.Cari.Infrastructure;
using OnMuhasebe.Modules.Stok.Infrastructure;
using OnMuhasebe.Shared.Contracts.TopluAktarim;
using OnMuhasebe.Shared.Infrastructure.Excel;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.TopluAktarim;

namespace OnMuhasebe.Hesaplama.Tests;

public class TopluAktarimTestleri
{
    private static readonly AktarimSutunu[] Sutunlar =
    [
        new("ad", "Ünvan", Zorunlu: true, Esanlamlilar: ["Cari Adı"]),
        new("vergi", "Vergi No", Esanlamlilar: ["VKN"]),
        new("tutar", "Tutar")
    ];

    private static byte[] Dosya(string[] basliklar, params object?[][] satirlar) =>
        ExcelYazici.Yaz("Sayfa", basliklar.Select(b => new ExcelSutunu(b)).ToList(), satirlar);

    [Theory]
    [InlineData("1.234,56", "1234.56")]
    [InlineData("1,234.56", "1234.56")]
    [InlineData("1234,5", "1234.5")]
    [InlineData("1234.5", "1234.5")]
    [InlineData("1.234", "1234")]
    [InlineData("1.234.567", "1234567")]
    [InlineData("1,234,567", "1234567")]
    [InlineData("12.50", "12.50")]
    [InlineData("1.500 TL", "1500")]
    [InlineData("₺1.500,75", "1500.75")]
    [InlineData("-250,50", "-250.50")]
    [InlineData("0", "0")]
    public void Metin_tutar_cozulur(string metin, string beklenen) =>
        Assert.Equal(decimal.Parse(beklenen, System.Globalization.CultureInfo.InvariantCulture), AktarimTablosu.SayiCoz(metin));

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("12 adet")]
    public void Sayi_olmayan_metin_cozulmez(string metin) => Assert.Null(AktarimTablosu.SayiCoz(metin));

    [Theory]
    [InlineData("VERGİ NO", "Vergi No")]
    [InlineData("Şti.", "STI")]
    [InlineData("ÇAĞRI ÖZÜ", "çağrı özü")]
    [InlineData("A.Ş.", "AS")]
    public void Sadelestirme_turkce_harf_ve_noktalamaya_bakmaz(string a, string b) =>
        Assert.Equal(AktarimTablosu.Sadele(a), AktarimTablosu.Sadele(b));

    [Fact]
    public void Baslik_ustte_bilgi_satiri_olsa_da_bulunur_esanlamli_taninir()
    {
        var icerik = Dosya(["Rapor"],
            [null],
            ["Cari Adı", "VKN", "Tutar", "Fazla Sutun"],
            ["Ali AS", 1234567890m, "1.250,50"]);

        var tablo = AktarimTablosu.Oku(icerik, Sutunlar);

        Assert.True(tablo.BasariliMi);
        Assert.Equal(["Fazla Sutun"], tablo.Deger.TaninmayanSutunlar);
        var satir = Assert.Single(tablo.Deger.Satirlar);
        Assert.Equal("Ali AS", satir.Metin("ad"));
        Assert.Equal(1250.50m, satir.Sayi("tutar", []));
        Assert.Equal(4, satir.SatirNo);
    }

    [Fact]
    public void Sayi_hucresi_metne_ondaliksiz_cevrilir_tutar_olarak_hassas_okunur()
    {
        var icerik = Dosya(["Ünvan", "Vergi No", "Tutar"], ["Ali", 1234567890m, 234.125m]);

        var satir = Assert.Single(AktarimTablosu.Oku(icerik, Sutunlar).Deger.Satirlar);

        Assert.Equal("1234567890", satir.Metin("vergi"));

        // Sayı hücresindeki üç ondalık binlik sanılmaz.
        Assert.Equal(234.125m, satir.Sayi("tutar", []));
    }

    [Fact]
    public void Zorunlu_sutun_yoksa_hata_verir_adini_soyler()
    {
        var sonuc = AktarimTablosu.Oku(Dosya(["Vergi No", "Tutar"], ["1", 2m]), Sutunlar);

        Assert.False(sonuc.BasariliMi);
        Assert.Contains("Ünvan", sonuc.Hata!.Mesaj);
    }

    [Fact]
    public void Excel_olmayan_dosya_okunamadi_hatasi() =>
        Assert.Equal("dosya_okunamadi", AktarimTablosu.Oku("Unvan;Vergi\nAli;1"u8.ToArray(), Sutunlar).Hata!.Kod);

    [Fact]
    public void Bozuk_excel_hata_doner_patlamaz() =>
        Assert.Equal("dosya_okunamadi", AktarimTablosu.Oku([(byte)'P', (byte)'K', 3, 4, 0, 0, 0, 0, 0, 0], Sutunlar).Hata!.Kod);

    [Fact]
    public void Satir_siniri_asilinca_hata_doner()
    {
        var satirlar = Enumerable.Range(0, AktarimTablosu.AzamiSatir + 20).Select(i => new object?[] { $"Cari {i}" }).ToArray();

        Assert.Equal("dosya_siniri", AktarimTablosu.Oku(Dosya(["Ünvan"], satirlar), Sutunlar).Hata!.Kod);
    }

    [Fact]
    public void Sablon_zorunlu_sutunu_isaretler_ve_geri_okunur()
    {
        var tablo = AktarimTablosu.Oku(AktarimTablosu.Sablon("Cariler", Sutunlar), Sutunlar);

        Assert.True(tablo.BasariliMi);
        Assert.Empty(tablo.Deger.Satirlar);
        Assert.Empty(tablo.Deger.TaninmayanSutunlar);
    }

    [Fact]
    public void Her_sablon_kendi_kendini_okur()
    {
        Assert.Empty(AktarimTablosu.Oku(AktarimTablosu.Sablon("Cariler", CariAktarimServisi.Sutunlar), CariAktarimServisi.Sutunlar).Deger.TaninmayanSutunlar);
        Assert.Empty(AktarimTablosu.Oku(AktarimTablosu.Sablon("Stoklar", StokAktarimServisi.Sutunlar), StokAktarimServisi.Sutunlar).Deger.TaninmayanSutunlar);
    }

    [Fact]
    public void Ozet_once_hatalilari_koyar_sayilar_tum_dosyayi_yansitir()
    {
        var satirlar = Enumerable.Range(1, TopluAktarimSonucu.AzamiSatirOzeti + 10)
            .Select(i => new AktarimSatiriOzeti(i, null, "x", AktarimSatirDurumu.Eklenecek, []))
            .Append(new AktarimSatiriOzeti(9999, null, "h", AktarimSatirDurumu.Hatali, ["hata"])).ToList();

        var ozet = AktarimTablosu.Ozetle(false, satirlar, []);

        Assert.Equal(TopluAktarimSonucu.AzamiSatirOzeti + 11, ozet.Toplam);
        Assert.Equal(1, ozet.Hatali);
        Assert.Equal(TopluAktarimSonucu.AzamiSatirOzeti, ozet.Satirlar.Count);
        Assert.Equal(9999, ozet.Satirlar[0].SatirNo);
    }

    [Theory]
    [InlineData("123456789", "0123456789")]
    [InlineData("1234567890", "1234567890")]
    [InlineData("100 000 00146", "10000000146")]
    public void Vergi_no_bastaki_sifiri_geri_alir(string metin, string beklenen) =>
        Assert.Equal(beklenen, CariAktarimServisi.VergiNoTemizle(metin));

    [Theory]
    [InlineData("Müşteri", CariTuru.Musteri)]
    [InlineData("ALICI", CariTuru.Musteri)]
    [InlineData("", CariTuru.Musteri)]
    [InlineData("Satıcı", CariTuru.Tedarikci)]
    [InlineData("TEDARİKÇİ", CariTuru.Tedarikci)]
    [InlineData("Her ikisi", CariTuru.MusteriVeTedarikci)]
    [InlineData("Alıcı/Satıcı", CariTuru.MusteriVeTedarikci)]
    public void Cari_turu_yazimlari_taninir(string metin, CariTuru beklenen)
    {
        var hatalar = new List<string>();

        Assert.Equal(beklenen, CariAktarimServisi.TurCoz(metin, hatalar));
        Assert.Empty(hatalar);
    }

    [Fact]
    public void Bos_kisi_tipi_vergi_nosundan_anlasilir()
    {
        Assert.Equal(KisiTipi.Sahis, CariAktarimServisi.KisiTipiCoz(null, "10000000146", []));
        Assert.Equal(KisiTipi.Tuzel, CariAktarimServisi.KisiTipiCoz(null, "1234567890", []));
        Assert.Equal(KisiTipi.Tuzel, CariAktarimServisi.KisiTipiCoz(null, null, []));
    }

    [Theory]
    [InlineData("Adet", "ADET")]
    [InlineData("AD", "ADET")]
    [InlineData("", "ADET")]
    [InlineData("Kilo", "KG")]
    [InlineData("kg", "KG")]
    [InlineData("Metreküp", "M3")]
    [InlineData("Lt", "LT")]
    [InlineData("KOLİ", "KOLI")]
    public void Birim_yazimlari_taninir(string metin, string beklenen)
    {
        var hatalar = new List<string>();

        Assert.Equal(beklenen, StokAktarimServisi.BirimCoz(metin, hatalar));
        Assert.Empty(hatalar);
    }

    [Fact]
    public void Tanimsiz_birim_hatadir()
    {
        var hatalar = new List<string>();
        StokAktarimServisi.BirimCoz("Sepet", hatalar);

        Assert.Single(hatalar);
    }

    [Fact]
    public void Kod_artirma_besli_bicimi_korur_ve_tasar()
    {
        Assert.Equal("C00013", KodUretici.Artir("C00012", "C"));
        Assert.Equal("S100000", KodUretici.Artir("S99999", "S"));
    }
}
