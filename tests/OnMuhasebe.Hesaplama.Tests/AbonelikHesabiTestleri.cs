using OnMuhasebe.Modules.Yonetim.Domain;

namespace OnMuhasebe.Hesaplama.Tests;

public class AbonelikHesabiTestleri
{
    private static readonly DateTime _simdi = new(2026, 10, 21, 14, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Suresi_dolmamis_lisans_mevcut_bitisten_uzar()
    {
        // 30.10 dahil geçerli (bitiş 31.10 00:00): bir ay eklenince 30.11 dahil.
        var bitis = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 11, 30, 0, 0, 0, DateTimeKind.Utc), AbonelikHesabi.YeniBitis(bitis, _simdi, 1));
    }

    [Fact]
    public void Suresi_dolmus_lisans_bugunden_baslar()
    {
        var bitis = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 11, 21, 0, 0, 0, DateTimeKind.Utc), AbonelikHesabi.YeniBitis(bitis, _simdi, 1));
    }

    [Fact]
    public void Lisansi_olmayan_firma_bugunden_yillik_uzar()
    {
        Assert.Equal(new DateTime(2027, 10, 21, 0, 0, 0, DateTimeKind.Utc), AbonelikHesabi.YeniBitis(null, _simdi, 12));
    }

    [Fact]
    public void Ay_sonu_kisa_aya_tasinirken_ayin_son_gunune_iner()
    {
        var bitis = new DateTime(2027, 1, 31, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2027, 2, 28, 0, 0, 0, DateTimeKind.Utc), AbonelikHesabi.YeniBitis(bitis, _simdi, 1));
    }

    [Theory]
    [InlineData(2026, 10, 22, 0, 1)]
    [InlineData(2026, 10, 28, 0, 7)]
    [InlineData(2026, 10, 21, 0, 0)]
    public void Kalan_gun_yukari_yuvarlanir_eksiye_dusmez(int yil, int ay, int gun, int saat, int beklenen) =>
        Assert.Equal(beklenen, AbonelikHesabi.KalanGun(new DateTime(yil, ay, gun, saat, 0, 0, DateTimeKind.Utc), _simdi));
}
