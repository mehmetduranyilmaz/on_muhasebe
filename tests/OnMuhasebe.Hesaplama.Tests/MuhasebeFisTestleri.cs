using OnMuhasebe.Modules.Muhasebe.Contracts;
using OnMuhasebe.Modules.Muhasebe.Domain;
using OnMuhasebe.Shared.Contracts.Muhasebe;

namespace OnMuhasebe.Hesaplama.Tests;

public class MuhasebeFisTestleri
{
    private static readonly Guid Musteri = Guid.NewGuid();
    private static readonly Guid Tedarikci = Guid.NewGuid();
    private static readonly Guid IkisiBirden = Guid.NewGuid();
    private static readonly Guid Kasa = Guid.NewGuid();
    private static readonly Guid Banka = Guid.NewGuid();
    private static readonly Guid DolarBanka = Guid.NewGuid();
    private static readonly Guid Kira = Guid.NewGuid();
    private static readonly Guid KrediGideri = Guid.NewGuid();
    private static readonly DateOnly Tarih = new(2026, 10, 15);

    private static KodCozucu Kodlar(Dictionary<(KartTuru, Guid), string>? ozel = null) => new(
        new HesapPlani(new Dictionary<HesapRolu, string>(), ".", "01"), ozel ?? [],
        [new(Musteri, "C00001", "Alıcı AŞ", "Musteri"), new(Tedarikci, "C00002", "Satıcı Ltd", "Tedarikci"), new(IkisiBirden, "C00003", "Karma", "HerIkisi")],
        [new(Kasa, "K001", "Merkez Kasa", "Kasa"), new(Banka, "B001", "Ziraat TL", "Banka"), new(DolarBanka, "B002", "Ziraat USD", "Banka")],
        [new(Kira, "", "Kira", "Gider"), new(KrediGideri, "", "Kredi faiz ve masrafları", "Finansman")]);

    private static MuhasebeFaturasi Fatura(string tur, Guid? cari, decimal stoklu, decimal stoksuz, decimal kdv,
        decimal tevkif = 0, decimal stopaj = 0, decimal kur = 1) =>
        new(Guid.NewGuid(), tur, "A2026000000001", Tarih, cari, "Ünvan", kur, stoklu, stoksuz, kdv, tevkif, stopaj, Guid.NewGuid(), null);

    private static MuhasebeKasaHareketi Hareket(Guid islem, string tur, Guid hesap, decimal giris, decimal cikis, Guid? cari = null,
        Guid? kategori = null, string? karsiRol = null, decimal gider = 0, Guid? giderKategori = null) =>
        new(Guid.NewGuid(), islem, Tarih, tur, hesap, giris, cikis, cari, kategori, karsiRol, "BN1", null, null, gider, giderKategori);

    private static (decimal Borc, decimal Alacak) Satir(MuhasebeFisi fis, string kod) =>
        (fis.Satirlar.Where(s => s.HesapKodu == kod).Sum(s => s.Borc), fis.Satirlar.Where(s => s.HesapKodu == kod).Sum(s => s.Alacak));

    [Fact]
    public void Satis_faturasi_cari_borc_satis_ve_kdv_alacak()
    {
        var fis = FisUretici.Fatura(Fatura("Satis", Musteri, 800, 200, 200), Kodlar());

        Assert.True(fis.DengeliMi);
        Assert.Equal((1200m, 0m), Satir(fis, "120.01.00001"));
        Assert.Equal((0m, 1000m), Satir(fis, "600"));
        Assert.Equal((0m, 200m), Satir(fis, "391"));
        Assert.Equal(FisTuru.Mahsup, fis.Tur);
    }

    [Fact]
    public void Tevkifatli_satista_hesaplanan_kdv_tevkif_edilmeyen_kisimdir()
    {
        var fis = FisUretici.Fatura(Fatura("Satis", Musteri, 0, 1000, 200, tevkif: 140), Kodlar());

        Assert.True(fis.DengeliMi);
        Assert.Equal((0m, 60m), Satir(fis, "391"));
        Assert.Equal((1060m, 0m), Satir(fis, "120.01.00001"));
    }

    [Fact]
    public void Tevkifatli_alista_kdvnin_tamami_indirilir_tevkif_edilen_360a()
    {
        var fis = FisUretici.Fatura(Fatura("Alis", Tedarikci, 600, 400, 200, tevkif: 100), Kodlar());

        Assert.True(fis.DengeliMi);
        Assert.Equal((600m, 0m), Satir(fis, "153"));
        Assert.Equal((400m, 0m), Satir(fis, "770"));
        Assert.Equal((200m, 0m), Satir(fis, "191"));
        Assert.Equal((0m, 100m), Satir(fis, "360"));
        Assert.Equal((0m, 1100m), Satir(fis, "320.01.00002"));
    }

    [Fact]
    public void Smm_stopaji_360a_cariye_net_tutar()
    {
        var fis = FisUretici.Fatura(Fatura("SerbestMeslekMakbuzu", Tedarikci, 0, 1000, 200, stopaj: 200), Kodlar());

        Assert.True(fis.DengeliMi);
        Assert.Equal((0m, 200m), Satir(fis, "360"));
        Assert.Equal((0m, 1000m), Satir(fis, "320.01.00002"));
    }

    [Fact]
    public void Satista_kesilen_stopaj_pesin_odenen_vergidir()
    {
        var fis = FisUretici.Fatura(Fatura("Satis", Musteri, 0, 1000, 200, stopaj: 200), Kodlar());

        Assert.Equal((200m, 0m), Satir(fis, "193"));
        Assert.Equal((1000m, 0m), Satir(fis, "120.01.00001"));
    }

    [Fact]
    public void Iadeler_ters_kayittir()
    {
        var satistanIade = FisUretici.Fatura(Fatura("SatistanIade", Musteri, 100, 0, 20), Kodlar());
        Assert.Equal((100m, 0m), Satir(satistanIade, "610"));
        Assert.Equal((20m, 0m), Satir(satistanIade, "191"));
        Assert.Equal((0m, 120m), Satir(satistanIade, "120.01.00001"));

        var alistanIade = FisUretici.Fatura(Fatura("AlistanIade", Tedarikci, 100, 0, 20), Kodlar());
        Assert.Equal((0m, 100m), Satir(alistanIade, "153"));
        Assert.Equal((0m, 20m), Satir(alistanIade, "391"));
        Assert.Equal((120m, 0m), Satir(alistanIade, "320.01.00002"));
    }

    [Fact]
    public void Dovizli_faturada_cari_yuvarlama_farkini_alir()
    {
        var fis = FisUretici.Fatura(Fatura("Satis", Musteri, 0, 333.33m, 66.67m, kur: 34.5678m), Kodlar());

        Assert.True(fis.DengeliMi);
        Assert.Equal((0m, 11522.48m), Satir(fis, "600"));
        Assert.Equal((0m, 2304.64m), Satir(fis, "391"));
    }

    [Fact]
    public void Hem_musteri_hem_tedarikci_carinin_hesabi_belge_yonunden_secilir()
    {
        Assert.Equal((1200m, 0m), Satir(FisUretici.Fatura(Fatura("Satis", IkisiBirden, 1000, 0, 200), Kodlar()), "120.01.00003"));
        Assert.Equal((0m, 1200m), Satir(FisUretici.Fatura(Fatura("Alis", IkisiBirden, 1000, 0, 200), Kodlar()), "320.01.00003"));
    }

    [Fact]
    public void Carisiz_faturada_ana_hesap_ve_ozel_kod_onceligi()
    {
        Assert.Equal((120m, 0m), Satir(FisUretici.Fatura(Fatura("Satis", null, 100, 0, 20), Kodlar()), "120"));

        var ozel = Kodlar(new() { [(KartTuru.Cari, Musteri)] = "120.99.1" });
        Assert.Equal((120m, 0m), Satir(FisUretici.Fatura(Fatura("Satis", Musteri, 100, 0, 20), ozel), "120.99.1"));
    }

    [Fact]
    public void Kasa_tahsilati_tahsil_fisi_banka_odemesi_mahsup_fisidir()
    {
        var uyarilar = new List<string>();
        var fisler = FisUretici.KasaBanka(
            [Hareket(Guid.NewGuid(), "Tahsilat", Kasa, 500, 0, Musteri), Hareket(Guid.NewGuid(), "Odeme", Banka, 0, 300, Tedarikci)],
            Kodlar(), uyarilar);

        Assert.Empty(uyarilar);
        Assert.Equal(FisTuru.Tahsil, fisler[0].Tur);
        Assert.Equal((500m, 0m), Satir(fisler[0], "100.001"));
        Assert.Equal((0m, 500m), Satir(fisler[0], "120.01.00001"));
        Assert.Equal(FisTuru.Mahsup, fisler[1].Tur);
        Assert.Equal((300m, 0m), Satir(fisler[1], "320.01.00002"));
        Assert.Equal((0m, 300m), Satir(fisler[1], "102.001"));
    }

    [Fact]
    public void Odenmis_gider_kategori_hesabina_kasadan_tediye()
    {
        var fis = Assert.Single(FisUretici.KasaBanka([Hareket(Guid.NewGuid(), "Gider", Kasa, 0, 1000, kategori: Kira)], Kodlar(), []));

        Assert.Equal(FisTuru.Tediye, fis.Tur);
        Assert.Equal((1000m, 0m), Satir(fis, "770"));
        Assert.Equal((0m, 1000m), Satir(fis, "100.001"));
    }

    [Fact]
    public void Kredi_taksiti_anapara_300e_faiz_finansman_giderine()
    {
        var fis = Assert.Single(FisUretici.KasaBanka(
            [Hareket(Guid.NewGuid(), "KrediOdeme", Banka, 0, 1150, karsiRol: "BankaKredileri", gider: 150, giderKategori: KrediGideri)], Kodlar(), []));

        Assert.True(fis.DengeliMi);
        Assert.Equal((1000m, 0m), Satir(fis, "300"));
        Assert.Equal((150m, 0m), Satir(fis, "780"));
        Assert.Equal((0m, 1150m), Satir(fis, "102.001"));
    }

    [Fact]
    public void Cek_tahsili_alinan_ceklerden_bankaya()
    {
        var fis = Assert.Single(FisUretici.KasaBanka([Hareket(Guid.NewGuid(), "CekSenetTahsil", Banka, 2000, 0, karsiRol: "AlinanCekler")], Kodlar(), []));

        Assert.Equal((2000m, 0m), Satir(fis, "102.001"));
        Assert.Equal((0m, 2000m), Satir(fis, "101"));
    }

    [Fact]
    public void Virman_tek_fistir_kur_farki_kambiyo_hesabina()
    {
        var islem = Guid.NewGuid();
        var fis = Assert.Single(FisUretici.KasaBanka(
            [Hareket(islem, "VirmanCikis", DolarBanka, 0, 3450), Hareket(islem, "VirmanGiris", Banka, 3500, 0)], Kodlar(), []));

        Assert.True(fis.DengeliMi);
        Assert.Equal((0m, 3450m), Satir(fis, "102.002"));
        Assert.Equal((3500m, 0m), Satir(fis, "102.001"));
        Assert.Equal((0m, 50m), Satir(fis, "646"));
    }

    [Fact]
    public void Tek_bacakli_virman_uyari_verir()
    {
        var uyarilar = new List<string>();
        var fis = Assert.Single(FisUretici.KasaBanka([Hareket(Guid.NewGuid(), "VirmanGiris", Banka, 100, 0)], Kodlar(), uyarilar));

        Assert.False(fis.DengeliMi);
        Assert.Single(uyarilar);
    }

    private static MuhasebeEvrakHareketi Evrak(Guid bordro, string islem, string tur, decimal tutar, Guid cari, Guid? ciroCari = null) =>
        new(Guid.NewGuid(), bordro, "BR00001", Tarih, islem, tur, "AC00001/123", Tarih.AddMonths(2), tutar, cari, ciroCari, null);

    [Fact]
    public void Alinan_cek_portfoye_girer_bordro_tek_fistir()
    {
        var bordro = Guid.NewGuid();
        var fis = Assert.Single(FisUretici.CekSenet(
            [Evrak(bordro, "Giris", "Cek", 1000, Musteri), Evrak(bordro, "Giris", "Senet", 500, Musteri)], Kodlar()));

        Assert.True(fis.DengeliMi);
        Assert.Equal("BR00001", fis.BelgeNo);
        Assert.Equal((1000m, 0m), Satir(fis, "101"));
        Assert.Equal((500m, 0m), Satir(fis, "121"));
        Assert.Equal((0m, 1500m), Satir(fis, "120.01.00001"));
    }

    [Fact]
    public void Ciro_tedarikciyi_borclandirir_karsiliksizda_musteriye_doner()
    {
        var ciro = Assert.Single(FisUretici.CekSenet([Evrak(Guid.NewGuid(), "Ciro", "Cek", 1000, Musteri, Tedarikci)], Kodlar()));
        Assert.Equal((1000m, 0m), Satir(ciro, "320.01.00002"));
        Assert.Equal((0m, 1000m), Satir(ciro, "101"));

        var karsiliksiz = Assert.Single(FisUretici.CekSenet([Evrak(Guid.NewGuid(), "CirodanKarsiliksiz", "Cek", 1000, Musteri, Tedarikci)], Kodlar()));
        Assert.Equal((1000m, 0m), Satir(karsiliksiz, "120.01.00001"));
        Assert.Equal((0m, 1000m), Satir(karsiliksiz, "320.01.00002"));
    }

    [Fact]
    public void Verilen_cek_103e_iadesi_ters_kayit()
    {
        var cikis = Assert.Single(FisUretici.CekSenet([Evrak(Guid.NewGuid(), "Cikis", "Cek", 700, Tedarikci)], Kodlar()));
        Assert.Equal((700m, 0m), Satir(cikis, "320.01.00002"));
        Assert.Equal((0m, 700m), Satir(cikis, "103"));

        var iade = Assert.Single(FisUretici.CekSenet([Evrak(Guid.NewGuid(), "VerilenIade", "Senet", 700, Tedarikci)], Kodlar()));
        Assert.Equal((700m, 0m), Satir(iade, "321"));
        Assert.Equal((0m, 700m), Satir(iade, "320.01.00002"));
    }

    [Fact]
    public void Karsiliksiz_cek_portfoyden_musteriye()
    {
        var fis = Assert.Single(FisUretici.CekSenet([Evrak(Guid.NewGuid(), "Karsiliksiz", "Cek", 900, Musteri)], Kodlar()));

        Assert.Equal((900m, 0m), Satir(fis, "120.01.00001"));
        Assert.Equal((0m, 900m), Satir(fis, "101"));
    }

    [Fact]
    public void Gider_tahakkuku_kategori_borc_cari_alacak()
    {
        var fis = Assert.Single(FisUretici.Tahakkuk([new(Guid.NewGuid(), Tarih, "Gider", Kira, 5000, IkisiBirden, "KIRA-10", null)], Kodlar()));

        Assert.True(fis.DengeliMi);
        Assert.Equal((5000m, 0m), Satir(fis, "770"));
        Assert.Equal((0m, 5000m), Satir(fis, "320.01.00003"));
    }

    [Fact]
    public void Gelir_tahakkuku_cari_borc_gelir_alacak()
    {
        var fis = Assert.Single(FisUretici.Tahakkuk([new(Guid.NewGuid(), Tarih, "Gelir", Guid.NewGuid(), 2000, IkisiBirden, null, null)], Kodlar()));

        Assert.Equal((2000m, 0m), Satir(fis, "120.01.00003"));
        Assert.Equal((0m, 2000m), Satir(fis, "649"));
    }
}
