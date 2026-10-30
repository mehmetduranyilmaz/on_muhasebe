using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.KasaBanka.Domain.CekSenet;

namespace OnMuhasebe.Hesaplama.Tests;

public class CekSenetTestleri
{
    private static readonly DateOnly _gun = new(2026, 10, 15);
    private static readonly Guid _musteri = Guid.NewGuid();
    private static readonly Guid _tedarikci = Guid.NewGuid();

    private static Evrak AlinanCek(decimal tutar = 10_000m) => Evrak.Alinan("AC00001",
        new AlinanEvrakGirdisi(EvrakTuru.Cek, "123456", _gun.AddDays(30), tutar, "TRY", null, null,
            new BorcluGirdisi(null, "Ahmet Yılmaz", "10000000146", null), "Ziraat", "Merkez", "555", null, null, null, null),
        _gun, _musteri, 1m, tutar, 1m, null, "Ahmet Yılmaz", "10000000146");

    private static Gecis G(EvrakYonu yon, EvrakDurumu durum, EvrakIslemi islem) => EvrakGecisleri.Bul(yon, durum, islem)!;

    private static EvrakHareketi Uygula(Evrak e, EvrakIslemi islem, Guid? hesap = null, (Guid, decimal, decimal)? ciro = null, decimal? tutar = null,
        int gun = 1)
    {
        var sonuc = e.Uygula(G(e.Yon, e.Durum, islem), Guid.NewGuid(), Guid.NewGuid(), _gun.AddDays(gun), hesap, ciro, tutar, null);
        Assert.True(sonuc.BasariliMi, sonuc.BasariliMi ? "" : sonuc.Hata.Mesaj);
        return sonuc.Deger;
    }

    [Theory]
    [InlineData(EvrakDurumu.Portfoyde, new[] { EvrakIslemi.TahsileVerme, EvrakIslemi.ElTahsil, EvrakIslemi.Ciro, EvrakIslemi.Karsiliksiz, EvrakIslemi.Iade, EvrakIslemi.TeminataVerme, EvrakIslemi.Oteleme })]
    [InlineData(EvrakDurumu.Tahsilde, new[] { EvrakIslemi.TahsildenGeriAlma, EvrakIslemi.Tahsil, EvrakIslemi.Karsiliksiz })]
    [InlineData(EvrakDurumu.CiroEdildi, new[] { EvrakIslemi.CirodanTahsil, EvrakIslemi.CirodanGeriAlma, EvrakIslemi.CirodanKarsiliksiz })]
    [InlineData(EvrakDurumu.Karsiliksiz, new[] { EvrakIslemi.HukukiTakip, EvrakIslemi.KarsiliksizdanTahsil, EvrakIslemi.KarsiliksizIade })]
    [InlineData(EvrakDurumu.HukukiTakipte, new[] { EvrakIslemi.KarsiliksizdanTahsil, EvrakIslemi.KarsiliksizIade })]
    [InlineData(EvrakDurumu.TahsilEdildi, new EvrakIslemi[0])]
    public void Alinan_evrakta_durum_bazinda_yapilabilecek_islemler(EvrakDurumu durum, EvrakIslemi[] beklenen) =>
        Assert.Equal(beklenen.Order(), EvrakGecisleri.Uygunlar(EvrakYonu.Alinan, durum).Order());

    [Fact]
    public void Verilen_evrak_odenir_iade_alinir_ya_da_karsiliksiz_doner()
    {
        Assert.Equal([EvrakIslemi.Odeme, EvrakIslemi.VerilenIade, EvrakIslemi.VerilenKarsiliksiz], EvrakGecisleri.Uygunlar(EvrakYonu.Verilen, EvrakDurumu.Verildi).Order());
        Assert.Null(EvrakGecisleri.Bul(EvrakYonu.Verilen, EvrakDurumu.Verildi, EvrakIslemi.Ciro));
        Assert.Null(EvrakGecisleri.Bul(EvrakYonu.Alinan, EvrakDurumu.Portfoyde, EvrakIslemi.Odeme));
    }

    [Fact]
    public void Karsiliksiz_musteriyi_borclandirir_iadesi_cariyi_etkilemez_portfoyden_iade_borclandirir()
    {
        Assert.Equal(CariEtkisi.MusteriKarsiliksiz, G(EvrakYonu.Alinan, EvrakDurumu.Portfoyde, EvrakIslemi.Karsiliksiz).Cari);
        Assert.Equal(CariEtkisi.Yok, G(EvrakYonu.Alinan, EvrakDurumu.Karsiliksiz, EvrakIslemi.KarsiliksizIade).Cari);
        Assert.Equal(CariEtkisi.MusteriIade, G(EvrakYonu.Alinan, EvrakDurumu.Portfoyde, EvrakIslemi.Iade).Cari);
        Assert.Equal(CariEtkisi.CiroKarsiliksiz, G(EvrakYonu.Alinan, EvrakDurumu.CiroEdildi, EvrakIslemi.CirodanKarsiliksiz).Cari);
    }

    [Fact]
    public void Tahsile_verilen_cek_bankayi_hatirlar_tahsilde_konum_temizlenir()
    {
        var e = AlinanCek();
        var banka = Guid.NewGuid();
        Uygula(e, EvrakIslemi.TahsileVerme, banka);
        Assert.Equal((EvrakDurumu.Tahsilde, banka), (e.Durum, e.KonumHesapId));

        var tahsil = Uygula(e, EvrakIslemi.Tahsil, banka, gun: 30);
        Assert.Equal((EvrakDurumu.TahsilEdildi, (Guid?)null, 10_000m), (e.Durum, e.KonumHesapId, tahsil.Tutar));
        Assert.Empty(EvrakGecisleri.Uygunlar(e.Yon, e.Durum));
    }

    [Fact]
    public void Cirodan_karsiliksiz_donen_evrak_takibe_dusur_ciro_cari_bilgisi_hareketle_saklanir()
    {
        var e = AlinanCek();
        Uygula(e, EvrakIslemi.Ciro, ciro: (_tedarikci, 10_000m, 1m));
        Assert.Equal(_tedarikci, e.CiroCariId);

        var donus = Uygula(e, EvrakIslemi.CirodanKarsiliksiz, gun: 31);
        Assert.Equal((EvrakDurumu.Karsiliksiz, (Guid?)null, _gun.AddDays(31)), (e.Durum, e.CiroCariId, e.KarsiliksizTarihi));
        Assert.Equal((_tedarikci, 10_000m), (donus.OncekiCiroCariId!.Value, donus.OncekiCiroCariTutar!.Value));
    }

    [Fact]
    public void Karsiliksizdan_kismi_tahsil_kalani_dusurur_tamami_odenince_kapanir()
    {
        var e = AlinanCek();
        Uygula(e, EvrakIslemi.Karsiliksiz);
        Uygula(e, EvrakIslemi.HukukiTakip, gun: 2);

        Uygula(e, EvrakIslemi.KarsiliksizdanTahsil, Guid.NewGuid(), tutar: 4_000m, gun: 3);
        Assert.Equal((EvrakDurumu.HukukiTakipte, 6_000m), (e.Durum, e.KalanTutar));

        var fazla = e.Uygula(G(e.Yon, e.Durum, EvrakIslemi.KarsiliksizdanTahsil), Guid.NewGuid(), Guid.NewGuid(), _gun.AddDays(4), Guid.NewGuid(), null, 6_000.01m, null);
        Assert.False(fazla.BasariliMi);

        Uygula(e, EvrakIslemi.KarsiliksizdanTahsil, Guid.NewGuid(), gun: 5);
        Assert.Equal((EvrakDurumu.TahsilEdildi, 0m), (e.Durum, e.KalanTutar));
        Assert.NotNull(e.KarsiliksizTarihi);
    }

    [Fact]
    public void Islem_tarihi_son_hareketten_once_olamaz()
    {
        var e = AlinanCek();
        Uygula(e, EvrakIslemi.TahsileVerme, Guid.NewGuid(), gun: 10);
        var geri = e.Uygula(G(e.Yon, e.Durum, EvrakIslemi.Tahsil), Guid.NewGuid(), Guid.NewGuid(), _gun.AddDays(5), Guid.NewGuid(), null, null, null);
        Assert.False(geri.BasariliMi);
    }

    [Fact]
    public void Gecersiz_durumdaki_evraka_islem_uygulanamaz()
    {
        var e = AlinanCek();
        var gecis = G(EvrakYonu.Alinan, EvrakDurumu.Tahsilde, EvrakIslemi.Tahsil);
        Assert.False(e.Uygula(gecis, Guid.NewGuid(), Guid.NewGuid(), _gun, Guid.NewGuid(), null, null, null).BasariliMi);
    }

    [Fact]
    public void Geri_alma_son_hareketi_tersine_cevirir_girisi_geri_almak_evraki_iptal_eder()
    {
        var e = AlinanCek();
        e.Ilk(Guid.NewGuid(), Guid.NewGuid(), _gun, null);
        Uygula(e, EvrakIslemi.Ciro, ciro: (_tedarikci, 10_000m, 1m));
        Uygula(e, EvrakIslemi.CirodanKarsiliksiz, gun: 2);
        Uygula(e, EvrakIslemi.KarsiliksizdanTahsil, Guid.NewGuid(), tutar: 2_500m, gun: 3);

        Assert.True(e.SonHareketiGeriAl(DateTime.UtcNow).BasariliMi);
        Assert.Equal((EvrakDurumu.Karsiliksiz, 0m), (e.Durum, e.TahsilEdilen));

        Assert.True(e.SonHareketiGeriAl(DateTime.UtcNow).BasariliMi);
        Assert.Equal((EvrakDurumu.CiroEdildi, _tedarikci, (DateOnly?)null), (e.Durum, e.CiroCariId!.Value, e.KarsiliksizTarihi));

        Assert.True(e.SonHareketiGeriAl(DateTime.UtcNow).BasariliMi);
        Assert.Equal((EvrakDurumu.Portfoyde, (Guid?)null), (e.Durum, e.CiroCariId));

        Assert.True(e.SonHareketiGeriAl(DateTime.UtcNow).BasariliMi);
        Assert.Equal(EvrakDurumu.Iptal, e.Durum);
        Assert.False(e.SonHareketiGeriAl(DateTime.UtcNow).BasariliMi);
    }

    [Theory]
    [InlineData(false, false, 0, false)]
    [InlineData(false, false, 1, true)]
    [InlineData(false, true, 2, false)]
    [InlineData(true, false, 0, true)]
    public void Riskli_borclu_kara_liste_ya_da_uyarisi_acik_karsiliksiz_gecmis(bool karaListe, bool uyariKapali, int karsiliksiz, bool beklenen) =>
        Assert.Equal(beklenen, RiskMesaji.RiskliMi(karaListe, uyariKapali, karsiliksiz));

    [Fact]
    public void Risk_mesaji_tarih_tutar_ve_guncel_durumu_icerir()
    {
        var mesaj = RiskMesaji.Olustur(
        [
            new RiskliBorclu(Guid.NewGuid(), "Ahmet Yılmaz", "10000000146", true, "İcra takibi var",
                [new KarsiliksizKaydi(Guid.NewGuid(), "AC00007", EvrakTuru.Cek, new DateOnly(2026, 3, 12), 15_000m, "TRY", EvrakDurumu.Karsiliksiz)])
        ]);

        Assert.Equal("Ahmet Yılmaz (10000000146): kara listede: İcra takibi var; 12.03.2026 tarihli 15.000,00 TRY evrakı karşılıksız çıktı (takipte).", mesaj);
    }

    [Fact]
    public void Vade_takvimi_haftalik_dilimler_gecikmisleri_ayri_toplar()
    {
        var bugun = new DateOnly(2026, 10, 15); // Perşembe
        List<VadeKalemi> kalemler =
        [
            new(bugun.AddDays(-3), EvrakYonu.Alinan, 1_000m),
            new(bugun, EvrakYonu.Alinan, 2_000m),
            new(bugun.AddDays(4), EvrakYonu.Verilen, 500m),
            new(bugun.AddDays(5), EvrakYonu.Alinan, 300m),
            new(bugun.AddDays(40), EvrakYonu.Alinan, 9_999m)
        ];

        var t = VadeTakvimiHesaplayici.Olustur(kalemler, bugun, bugun, bugun.AddDays(13), VadeGruplama.Hafta);

        Assert.Equal((1_000m, 1), (t.GecikmisAlinanTl, t.GecikmisAlinanAdet));
        Assert.Equal(3, t.Dilimler.Count);
        Assert.Equal(new VadeDilimi(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 18), 2_000m, 0m, 1, 0), t.Dilimler[0]);
        Assert.Equal(new VadeDilimi(new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 25), 300m, 500m, 1, 1), t.Dilimler[1]);
        Assert.Equal(0m, t.Dilimler[2].AlinanTl);
    }

    [Fact]
    public void Vade_takvimi_aylik_dilim_ay_basindan_baslar()
    {
        var t = VadeTakvimiHesaplayici.Olustur([new(new DateOnly(2026, 11, 30), EvrakYonu.Verilen, 750m)],
            new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 15), new DateOnly(2026, 11, 30), VadeGruplama.Ay);

        Assert.Equal([new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1)], t.Dilimler.Select(d => d.Baslangic));
        Assert.Equal(750m, t.Dilimler[1].VerilenTl);
    }
}
