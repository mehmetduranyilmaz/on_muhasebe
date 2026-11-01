using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Modules.Fatura.Domain;

namespace OnMuhasebe.Hesaplama.Tests;

public class TekrarFaturaTestleri
{
    private static FaturaSatiriIstegi Satir(decimal fiyat) => new(null, "Kira", null, "ADET", 1, fiyat, false, 20, 0, 0, 0);

    private static TekrarPlaniKaydetIstegi Istek(
        TekrarSikligi siklik = TekrarSikligi.Aylik, int gun = 15, string baslangic = "2026-01-01", string? bitis = null, decimal? artis = null, int? artisAyi = null, string? aciklama = null) =>
        new("Kira", Guid.NewGuid(), siklik, gun, DateOnly.Parse(baslangic), bitis is null ? null : DateOnly.Parse(bitis), 10, aciklama, false, 0, null, null, artis, artisAyi, [Satir(1000)]);

    private static TekrarPlani Plan(TekrarPlaniKaydetIstegi? istek = null) => TekrarPlani.Olustur("TP00001", istek ?? Istek(), Guid.NewGuid());

    [Theory]
    [InlineData("2026-01-31", TekrarSikligi.Aylik, 31, "2026-02-28")]
    [InlineData("2026-02-28", TekrarSikligi.Aylik, 31, "2026-03-31")]
    [InlineData("2028-01-31", TekrarSikligi.Aylik, 31, "2028-02-29")]
    [InlineData("2026-11-15", TekrarSikligi.UcAylik, 15, "2027-02-15")]
    [InlineData("2026-08-31", TekrarSikligi.AltiAylik, 31, "2027-02-28")]
    [InlineData("2026-03-01", TekrarSikligi.Yillik, 1, "2027-03-01")]
    public void Sonraki_tarih_ay_sonuna_uyar(string donem, TekrarSikligi siklik, int gun, string beklenen) =>
        Assert.Equal(DateOnly.Parse(beklenen), TekrarTakvimi.Sonraki(DateOnly.Parse(donem), siklik, gun));

    [Theory]
    [InlineData("2026-01-10", 15, "2026-01-15")]
    [InlineData("2026-01-15", 15, "2026-01-15")]
    [InlineData("2026-01-20", 15, "2026-02-15")]
    [InlineData("2026-02-10", 31, "2026-02-28")]
    public void Ilk_tarih_baslangictan_once_olmaz(string baslangic, int gun, string beklenen) =>
        Assert.Equal(DateOnly.Parse(beklenen), TekrarTakvimi.Ilk(DateOnly.Parse(baslangic), TekrarSikligi.Aylik, gun));

    [Fact]
    public void Aciklamada_donem_yazar() =>
        Assert.Equal("Kasım 2026 kira bedeli", TekrarTakvimi.Aciklama("{ay} {yil} kira bedeli", new DateOnly(2026, 11, 1)));

    [Fact]
    public void Plan_uretildikce_ilerler_bitis_gecince_biter()
    {
        var plan = Plan(Istek(baslangic: "2026-01-01", bitis: "2026-02-20"));
        Assert.Equal(new DateOnly(2026, 1, 15), plan.SonrakiTarih);

        plan.Uretildi(new DateOnly(2026, 1, 15));
        Assert.Equal((new DateOnly(2026, 2, 15), TekrarDurumu.Aktif), (plan.SonrakiTarih!.Value, plan.Durum));

        plan.Uretildi(new DateOnly(2026, 2, 15));
        Assert.Null(plan.SonrakiTarih);
        Assert.Equal(TekrarDurumu.Bitti, plan.Durum);
    }

    [Fact]
    public void Bitis_ilk_donemden_onceyse_plan_hemen_biter()
    {
        var plan = Plan(Istek(baslangic: "2026-01-20", bitis: "2026-02-01"));

        Assert.Equal(TekrarDurumu.Bitti, plan.Durum);
        Assert.Null(plan.SonrakiTarih);
    }

    [Fact]
    public void Basarili_uretim_hatayi_temizler_hata_ilerletmez()
    {
        var plan = Plan();
        var donem = plan.SonrakiTarih!.Value;
        plan.HataYaz("Kur bulunamadı", DateTime.UtcNow);
        Assert.Equal(donem, plan.SonrakiTarih);

        plan.Uretildi(donem);

        Assert.Null(plan.SonHata);
        Assert.Equal(donem.AddMonths(1), plan.SonrakiTarih);
    }

    [Fact]
    public void Fatura_istegi_tarih_vade_ve_aciklama_tasir()
    {
        var plan = Plan(Istek(aciklama: "{ay} {yil} kira"));
        var i = plan.FaturaIstegi(new DateOnly(2026, 3, 15));

        Assert.Equal((FaturaTuru.Satis, new DateOnly(2026, 3, 15), new DateOnly(2026, 3, 25), "Mart 2026 kira"), (i.Tur, i.Tarih, i.VadeTarihi!.Value, i.Aciklama!));
    }

    [Theory]
    [InlineData("2026-03-15", false)]
    [InlineData("2027-02-15", false)]
    [InlineData("2027-03-15", true)]
    [InlineData("2027-05-15", true)]
    public void Yillik_artis_belirlenen_aydan_itibaren(string donem, bool beklenen) =>
        Assert.Equal(beklenen, TekrarTakvimi.ArtisVarMi(DateOnly.Parse(donem), 25, 3, 2026));

    [Fact]
    public void Artis_yilda_bir_uygulanir_ve_birikir()
    {
        var plan = Plan(Istek(artis: 25, artisAyi: 3));

        Assert.Null(plan.ArtisUygula(new DateOnly(2026, 6, 15)));
        Assert.Equal(25m, plan.ArtisUygula(new DateOnly(2027, 3, 15)));
        Assert.Equal(1250m, plan.Satirlar[0].BirimFiyat);
        Assert.Null(plan.ArtisUygula(new DateOnly(2027, 4, 15)));
        Assert.Equal(25m, plan.ArtisUygula(new DateOnly(2028, 3, 15)));
        Assert.Equal(1562.5m, plan.Satirlar[0].BirimFiyat);
    }

    [Fact]
    public void Ay_atlayan_plan_artisi_kacirmaz()
    {
        var plan = Plan(Istek(TekrarSikligi.UcAylik, artis: 10, artisAyi: 3));

        Assert.Equal(10m, plan.ArtisUygula(new DateOnly(2027, 4, 15)));
    }

    [Fact]
    public void Takvim_degisince_kesilmis_donem_yeniden_uretilmez()
    {
        var plan = Plan();
        plan.Uretildi(new DateOnly(2026, 1, 15));
        var kesilen = new DateOnly(2026, 2, 15);
        plan.Uretildi(kesilen);

        plan.Guncelle(Istek(gun: 20), Guid.NewGuid(), kesilen);

        Assert.Equal(new DateOnly(2026, 3, 20), plan.SonrakiTarih);
    }

    [Fact]
    public void Bitis_ileri_alininca_plan_yeniden_aktif_olur()
    {
        var plan = Plan(Istek(baslangic: "2026-01-20", bitis: "2026-02-01"));

        plan.Guncelle(Istek(baslangic: "2026-01-20", bitis: "2026-12-31"), Guid.NewGuid(), null);

        Assert.Equal(TekrarDurumu.Aktif, plan.Durum);
        Assert.Equal(new DateOnly(2026, 2, 15), plan.SonrakiTarih);
    }

    [Fact]
    public void Duraklatma_ve_devam()
    {
        var plan = Plan();

        Assert.False(plan.DevamEt().BasariliMi);
        Assert.True(plan.Duraklat().BasariliMi);
        Assert.False(plan.Duraklat().BasariliMi);
        Assert.True(plan.DevamEt().BasariliMi);
        Assert.Equal(TekrarDurumu.Aktif, plan.Durum);
    }
}
