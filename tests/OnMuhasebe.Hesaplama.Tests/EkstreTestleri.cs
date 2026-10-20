using System.Text;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.KasaBanka.Domain.Ekstre;

namespace OnMuhasebe.Hesaplama.Tests;

public class EkstreTestleri
{
    private static readonly DateOnly _gun = new(2026, 10, 15);

    [Theory]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("-1.234,56", -1234.56)]
    [InlineData("1.234,56-", -1234.56)]
    [InlineData("(250,00)", -250)]
    [InlineData("1,234.56", 1234.56)]
    [InlineData("1234.50", 1234.50)]
    [InlineData("1.250", 1250)]
    [InlineData("12,5", 12.5)]
    [InlineData("1.234.567,89 TL", 1234567.89)]
    [InlineData("  3500 ", 3500)]
    public void Turkce_ve_ingilizce_tutar_bicimleri_okunur(string metin, double beklenen) =>
        Assert.Equal((decimal)beklenen, EkstreMetni.Tutar(metin));

    [Theory]
    [InlineData("")]
    [InlineData("Devreden bakiye")]
    [InlineData(null)]
    public void Tutar_olmayan_metin_null_doner(string? metin) => Assert.Null(EkstreMetni.Tutar(metin));

    [Theory]
    [InlineData("15.10.2026")]
    [InlineData("15/10/2026")]
    [InlineData("2026-10-15")]
    [InlineData("15.10.2026 14:32")]
    [InlineData("15.10.26")]
    public void Banka_tarih_bicimleri_okunur(string metin) => Assert.Equal(_gun, EkstreMetni.Tarih(metin));

    [Fact]
    public void Normalize_turkce_harfleri_ve_bosluklari_birlestirir() =>
        Assert.Equal("ISTANBUL GIDA SAN.", EkstreMetni.Normalize("  istanbul   Gıda san. "));

    [Fact]
    public void Windows_1254_kodlu_noktali_virgullu_csv_okunur()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var metin = "Tarih;Açıklama;Tutar;Bakiye\r\n15.10.2026;\"EFT: ŞAHİN; GIDA\";1.500,00;11.500,00\r\n";
        var tablo = CsvOkuyucu.Oku(Encoding.GetEncoding(1254).GetBytes(metin));

        Assert.Equal(2, tablo.Count);
        Assert.Equal(["15.10.2026", "EFT: ŞAHİN; GIDA", "1.500,00", "11.500,00"], tablo[1]);
    }

    [Fact]
    public void Basliklardan_borc_alacak_kolonlari_tahmin_edilir()
    {
        List<string[]> tablo =
        [
            ["ZİRAAT BANKASI HESAP HAREKETLERİ"],
            ["Hesap: TR33 0006 1005 1978 6457 8413 26"],
            ["İşlem Tarihi", "Valör", "Açıklama", "Borç", "Alacak", "Bakiye", "Dekont No"]
        ];

        var e = KolonTahmincisi.Tahmin(tablo);

        Assert.Equal(new KolonEslemesi(2, 0, 2, null, 4, 3, 5, 6, false), e);
    }

    [Fact]
    public void Ayri_borc_alacak_kolonlari_isaretli_tutara_cevrilir_ozet_satirlari_atlanir()
    {
        List<string[]> tablo =
        [
            ["Tarih", "Açıklama", "Borç", "Alacak", "Bakiye"],
            ["", "Devreden bakiye", "", "", "10.000,00"],
            ["15.10.2026", "Kira ödemesi", "3.000,00", "", "7.000,00"],
            ["16.10.2026", "Gelen havale", "", "1.250,50", "8.250,50"],
            ["", "Toplam", "3.000,00", "1.250,50", ""]
        ];

        var cozum = SatirCozucu.Coz(tablo, KolonTahmincisi.Tahmin(tablo)!);

        Assert.Equal([-3000m, 1250.50m], cozum.Satirlar.Select(s => s.Tutar));
        Assert.Equal(2, cozum.AtlananSatir);
        Assert.Null(BakiyeKontrolu.Kontrol(cozum.Satirlar));
    }

    [Fact]
    public void Kart_ekstresinde_pozitif_tutar_harcama_olarak_okunur()
    {
        List<string[]> tablo = [["Tarih", "İşlem", "Tutar"], ["15.10.2026", "MARKET", "450,00"]];

        var cozum = SatirCozucu.Coz(tablo, new KolonEslemesi(0, 0, 1, 2, null, null, null, null, TutarTersMi: true));

        Assert.Equal(-450m, cozum.Satirlar.Single().Tutar);
    }

    [Fact]
    public void Yeniden_eskiye_siralanmis_bakiye_dogrulanir_ve_eksik_satir_yakalanir()
    {
        List<CozulmusSatir> dogru =
        [
            new(_gun.AddDays(2), "c", -200m, 1300m, null),
            new(_gun.AddDays(1), "b", 500m, 1500m, null),
            new(_gun, "a", 1000m, 1000m, null)
        ];
        Assert.Null(BakiyeKontrolu.Kontrol(dogru));

        List<CozulmusSatir> eksik = [dogru[0], dogru[2]];
        Assert.Contains("1 satırda", BakiyeKontrolu.Kontrol(eksik));
    }

    [Fact]
    public void Mt940_hareketleri_aciklamalari_ve_bakiyeleri_okunur()
    {
        const string mt940 = """
            :20:STMT261015
            :25:TR330006100519786457841326
            :28C:00001/001
            :60F:C261014TRY10000,00
            :61:2610151015C1500,00NTRFREF123//BANKREF1
            :86:?20EFT GELEN ANADOLU GIDA?21LTD STI FATURA ODEMESI
            :61:2610161016D320,50NCHGNONREF
            :86:HESAP ISLETIM UCRETI
            :62F:C261016TRY11179,50
            -
            """;

        var e = Mt940Okuyucu.Oku(Encoding.UTF8.GetBytes(mt940));

        Assert.True(Mt940Okuyucu.Mt940Mu(Encoding.UTF8.GetBytes(mt940)));
        Assert.Equal((10000m, 11179.50m), (e.AcilisBakiyesi, e.KapanisBakiyesi));
        Assert.Equal(new CozulmusSatir(_gun, "EFT GELEN ANADOLU GIDA LTD STI FATURA ODEMESI", 1500m, null, "REF123"), e.Satirlar[0]);
        Assert.Equal((-320.50m, (string?)null), (e.Satirlar[1].Tutar, e.Satirlar[1].Referans));
        Assert.Null(BakiyeKontrolu.Kontrol(e.Satirlar, e.AcilisBakiyesi, e.KapanisBakiyesi));
    }

    [Fact]
    public void Ayni_dosyada_birebir_ayni_iki_satir_farkli_parmak_izi_alir_tekrar_yuklemede_ayni_kalir()
    {
        var hesap = Guid.NewGuid();
        List<CozulmusSatir> satirlar = [new(_gun, "POS SATIS", 100m, null, null), new(_gun, "POS SATIS", 100m, null, null)];

        var ilk = ParmakIzi.Hesapla(hesap, satirlar);
        var ikinci = ParmakIzi.Hesapla(hesap, satirlar);

        Assert.NotEqual(ilk[0], ilk[1]);
        Assert.Equal(ilk, ikinci);
        Assert.NotEqual(ilk[0], ParmakIzi.Hesapla(Guid.NewGuid(), satirlar)[0]);
    }

    [Fact]
    public void Mutabakat_ayni_yon_ve_tutardaki_en_yakin_tarihli_hareketi_bulur()
    {
        var yakin = new MutabakatAdayi(Guid.NewGuid(), _gun.AddDays(1), 500m, 0m);
        List<MutabakatAdayi> adaylar =
        [
            new(Guid.NewGuid(), _gun, 0m, 500m),
            new(Guid.NewGuid(), _gun.AddDays(3), 500m, 0m),
            yakin,
            new(Guid.NewGuid(), _gun.AddDays(-5), 500m, 0m)
        ];

        Assert.Equal(yakin, Mutabakat.Bul(new CozulmusSatir(_gun, "x", 500m, null, null), adaylar));
        Assert.Null(Mutabakat.Bul(new CozulmusSatir(_gun, "x", 499m, null, null), adaylar));
    }

    [Fact]
    public void Oneri_onceligi_kural_kendi_hesabi_vkn_unvan_varsayilan()
    {
        var cari = new CariEslestirmeAdayi(Guid.NewGuid(), "Anadolu Gıda Ltd. Şti.", "9876543217", null, "TRY");
        var vknCari = new CariEslestirmeAdayi(Guid.NewGuid(), "Başka Firma", "1234567890", null, "TRY");
        var kart = new KendiHesabi(Guid.NewGuid(), null, "5400 **** 4821");
        var kategori = Guid.NewGuid();
        List<KuralVerisi> kurallar = [new(Guid.NewGuid(), "ELEKTRIK", KuralYonu.Cikis, EkstreIslemTuru.Gider, null, kategori, null)];

        Oneri Oner(string aciklama, decimal tutar) =>
            OneriMotoru.Oner(new CozulmusSatir(_gun, aciklama, tutar, null, null), HesapTuru.Banka, kurallar, [cari, vknCari], [kart]);

        Assert.Equal((EkstreIslemTuru.Gider, kategori, "Kural"), (Oner("Enerjisa elektrik faturası", -800m) is var a ? (a.IslemTuru, a.KategoriId, a.Kaynak) : default));
        Assert.Equal((EkstreIslemTuru.Virman, kart.Id), (Oner("KREDI KARTI ODEMESI 5400 **** 4821", -3000m) is var b ? (b.IslemTuru, b.KarsiHesapId) : default));
        Assert.Equal((EkstreIslemTuru.Tahsilat, vknCari.Id, "VKN/TCKN"), (Oner("EFT VKN 1234567890 FATURA", 900m) is var c ? (c.IslemTuru, c.CariHesapId, c.Kaynak) : default));
        Assert.Equal((EkstreIslemTuru.Odeme, cari.Id, "Unvan"), (Oner("Havale ANADOLU GIDA LTD", -150m) is var d ? (d.IslemTuru, d.CariHesapId, d.Kaynak) : default));
        Assert.Equal((EkstreIslemTuru.Tahsilat, (Guid?)null), (Oner("Gelen havale", 75m) is var e ? (e.IslemTuru, e.CariHesapId) : default));
        Assert.Equal(EkstreIslemTuru.Gider, Oner("Masraf", -10m).IslemTuru);
    }

    [Fact]
    public void Unvan_anahtari_sirket_eklerini_atar() =>
        Assert.Equal("ANADOLU GIDA", OneriMotoru.UnvanAnahtari("Anadolu Gıda San. ve Tic. Ltd. Şti."));
}
