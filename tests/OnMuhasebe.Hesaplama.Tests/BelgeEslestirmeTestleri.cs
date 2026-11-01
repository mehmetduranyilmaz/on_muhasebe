using System.Text;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.KasaBanka.Domain.Ekstre;

namespace OnMuhasebe.Hesaplama.Tests;

public class BelgeEslestirmeTestleri
{
    private static readonly DateOnly Gun = new(2026, 10, 15);
    private static readonly Guid CariA = Guid.NewGuid();
    private static readonly Guid CariB = Guid.NewGuid();
    private static readonly Oneri Varsayilan = new(EkstreIslemTuru.Tahsilat, null, null, null, "Varsayılan", null);

    private static FaturaAdayi Fatura(string no, Guid cari, decimal kalan, bool giris = true) => new(Guid.NewGuid(), no, cari, giris, kalan);

    private static EvrakAdayi Evrak(string no, decimal tutar, DateOnly? vade = null, bool giris = true, Guid? cari = null) =>
        new(Guid.NewGuid(), no, cari ?? CariA, giris, tutar, vade ?? Gun);

    private static CozulmusSatir Satir(string aciklama, decimal tutar) => new(Gun, aciklama, tutar, null, null);

    private static Oneri Uygula(BelgeEslestirici e, string aciklama, decimal tutar, Oneri? oneri = null, HesapTuru tur = HesapTuru.Banka) =>
        e.Uygula(Satir(aciklama, tutar), oneri ?? Varsayilan, tur);

    [Fact]
    public void Fatura_numarasi_cariyi_ve_faturayi_bulur_kismi_odemede_de()
    {
        var f = Fatura("ABC2026000000042", CariA, 1000);
        var o = Uygula(new BelgeEslestirici([f, Fatura("ABC2026000000043", CariB, 1000)], []), "HAVALE abc2026000000042 ODEMESI", 400);

        Assert.Equal((EkstreIslemTuru.Tahsilat, CariA, f.BelgeId, BelgeEslestirici.FaturaNo), (o.IslemTuru, o.CariHesapId, o.KapatilacakBelgeId, o.Kaynak));
    }

    [Fact]
    public void Fatura_numarasi_kuraldan_once_gelir_kural_tutardan_once()
    {
        var f = Fatura("ABC2026000000042", CariA, 500);
        var kural = new Oneri(EkstreIslemTuru.Gelir, null, Guid.NewGuid(), null, "Kural", Guid.NewGuid());

        Assert.Equal(f.BelgeId, Uygula(new BelgeEslestirici([f], []), "ABC2026000000042", 500, kural).KapatilacakBelgeId);
        Assert.Same(kural, Uygula(new BelgeEslestirici([f], [Evrak("7001", 500)]), "FAIZ GELIRI", 500, kural));
    }

    [Fact]
    public void Kisa_fatura_numarasi_rastgele_eslesmez()
    {
        var o = Uygula(new BelgeEslestirici([Fatura("A1", CariA, 1000)], []), "A1 SUBESI HAVALE", 400);

        Assert.Null(o.KapatilacakBelgeId);
    }

    [Fact]
    public void Cari_belliyse_tutari_tutan_tek_fatura_secilir_ikinci_satir_ayni_faturayi_almaz()
    {
        var f = Fatura("F1", CariA, 600);
        var e = new BelgeEslestirici([f, Fatura("F2", CariA, 300), Fatura("F3", CariB, 600)], []);
        var cari = new Oneri(EkstreIslemTuru.Tahsilat, CariA, null, null, "Unvan", null);

        var ilk = Uygula(e, "EFT", 600, cari);

        Assert.Equal((f.BelgeId, BelgeEslestirici.FaturaTutari), (ilk.KapatilacakBelgeId, ilk.Kaynak));
        Assert.Same(cari, Uygula(e, "EFT", 600, cari));
    }

    [Fact]
    public void Ayni_tutarda_iki_aday_varsa_secilmez()
    {
        var o = Uygula(new BelgeEslestirici([Fatura("F1", CariA, 840), Fatura("F2", CariB, 840)], []), "HAVALE", 840);

        Assert.Same(Varsayilan, o);
    }

    [Fact]
    public void Yalniz_tutar_isaretlenir_kart_ve_pos_hesabinda_aranmaz()
    {
        var f = Fatura("F1", CariB, 2400, giris: false);
        var o = Uygula(new BelgeEslestirici([f], []), "GIDEN HAVALE", -2400);

        Assert.Equal((EkstreIslemTuru.Odeme, CariB, f.BelgeId, BelgeEslestirici.YalnizTutar), (o.IslemTuru, o.CariHesapId, o.KapatilacakBelgeId, o.Kaynak));
        Assert.Null(Uygula(new BelgeEslestirici([f], []), "MARKET", -2400, tur: HesapTuru.KrediKarti).KapatilacakBelgeId);
    }

    [Fact]
    public void Yon_uymayan_belge_eslesmez()
    {
        var o = Uygula(new BelgeEslestirici([Fatura("F1", CariA, 100, giris: false)], [Evrak("7001", 100, giris: false)]), "GELEN 7001", 100);

        Assert.Same(Varsayilan, o);
    }

    [Theory]
    [InlineData("TAKAS CEK 7001", 5000, true)]
    [InlineData("TAKAS CEK 0007001", 5000, true)]
    [InlineData("TAKAS CEK 7001", 4999, false)]
    [InlineData("TAKAS CEK 70012", 5000, true)]
    public void Cek_numarasi_tutarla_birlikte_aranir(string aciklama, decimal tutar, bool evrakOlur)
    {
        // Son satırda numara tutmaz ama tutar ve vade tutar: evrak yine önerilir, kaynağı farklıdır.
        var c = Evrak("7001", 5000);
        var o = Uygula(new BelgeEslestirici([], [c]), aciklama, tutar);

        Assert.Equal(evrakOlur ? c.Id : null, o.EvrakId);
        Assert.Equal(evrakOlur ? EkstreIslemTuru.Evrak : EkstreIslemTuru.Tahsilat, o.IslemTuru);
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(-3, true)]
    [InlineData(4, false)]
    public void Numarasiz_cek_tutar_ve_vade_toleransiyla_eslesir(int gun, bool eslesir)
    {
        var c = Evrak("7002", 3300, Gun.AddDays(gun));

        Assert.Equal(eslesir ? c.Id : null, Uygula(new BelgeEslestirici([], [c]), "TAKAS TAHSILAT", 3300).EvrakId);
    }

    [Fact]
    public void Cari_belliyse_baska_carinin_cekine_baglanmaz()
    {
        var cari = new Oneri(EkstreIslemTuru.Tahsilat, CariA, null, null, "Unvan", null);

        Assert.Null(Uygula(new BelgeEslestirici([], [Evrak("7002", 3300, cari: CariB)]), "TAKAS", 3300, cari).EvrakId);
    }

    [Fact]
    public void Ayni_cek_iki_satira_baglanmaz()
    {
        var e = new BelgeEslestirici([], [Evrak("7001", 5000)]);

        Assert.NotNull(Uygula(e, "CEK 7001", 5000).EvrakId);
        Assert.Null(Uygula(e, "CEK 7001", 5000).EvrakId);
    }

    [Theory]
    [InlineData("TR56 0001 0012 3456 7890 1234 56", null, null)]
    [InlineData(null, "1234567890", null)]
    [InlineData(null, null, null)]
    [InlineData(null, "5400 **** 4821", null)]
    [InlineData("TR33 0006 1005 1978 6457 8413 26", null, "uyuşmuyor")]
    [InlineData(null, "9999", "uyuşmuyor")]
    public void Hesap_uyarisi(string? iban, string? hesapNo, string? beklenen)
    {
        var uyari = EkstreDosyaKontrolu.HesapUyarisi(["TR560001001234567890123456"], iban, hesapNo);

        if (beklenen is null)
        {
            Assert.Null(uyari);
        }
        else
        {
            Assert.Contains(beklenen, uyari);
        }
    }

    [Theory]
    [InlineData("TRY", "TRY", false)]
    [InlineData("TL", "TRY", false)]
    [InlineData("USD", "TRY", true)]
    [InlineData(null, "TRY", false)]
    public void Para_birimi_kontrolu(string? dosya, string hesap, bool beklenen) =>
        Assert.Equal(beklenen, EkstreDosyaKontrolu.ParaBirimiUyarMi(dosya, hesap));

    [Fact]
    public void Mt940_hesap_ve_para_birimi_okunur()
    {
        var e = Mt940Okuyucu.Oku(Encoding.UTF8.GetBytes("""
            :20:A
            :25:TR560001001234567890123456
            :60F:C261014TRY100,00
            :61:2610151015C50,00NTRFNONREF
            :86:GELEN
            :62F:C261015TRY150,00
            -
            :20:B
            :25:TR330006100519786457841326
            :60F:C261014USD10,00
            :62F:C261014USD10,00
            -
            """));

        Assert.Equal(["TR560001001234567890123456", "TR330006100519786457841326"], e.Hesaplar);
        Assert.Equal(["TRY", "USD"], e.ParaBirimleri);
    }

    [Fact]
    public void Aciklanamayan_fark()
    {
        // Ekstre 1000, program 900; bankada 300 aktarılmamış giriş, programda 200 ekstrede olmayan giriş: fark tam açıklanır.
        Assert.Equal(0m, MutabakatHesabi.Aciklanamayan(1000m, 900m, 300m, 200m));
        Assert.Equal(50m, MutabakatHesabi.Aciklanamayan(1050m, 900m, 300m, 200m));
    }

    [Fact]
    public void Kapanis_bakiyesi_siradan_bagimsiz_son_tarihin_bakiyesi()
    {
        CozulmusSatir Bakiyeli(int gun, decimal bakiye) => new(Gun.AddDays(gun), "X", 1, bakiye, null);

        Assert.Equal(30m, MutabakatHesabi.Kapanis([Bakiyeli(0, 10), Bakiyeli(1, 20), Bakiyeli(2, 30)]));
        Assert.Equal(30m, MutabakatHesabi.Kapanis([Bakiyeli(2, 30), Bakiyeli(1, 20), Bakiyeli(0, 10)]));
        Assert.Null(MutabakatHesabi.Kapanis([Satir("X", 1)]));
    }
}
