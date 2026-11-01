using OnMuhasebe.Modules.Satinalma.Contracts;
using OnMuhasebe.Modules.Satinalma.Domain;

namespace OnMuhasebe.Hesaplama.Tests;

public class SatinalmaTestleri
{
    private static readonly DateOnly Bugun = new(2026, 11, 1);
    private static readonly DateTime Simdi = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TalepEden = Guid.NewGuid();
    private static readonly Guid Onayci = Guid.NewGuid();
    private static readonly Guid Rol = Guid.NewGuid();

    private static TalepKaydetIstegi Istek(decimal fiyat = 100, decimal miktar = 2) =>
        new(Bugun, null, "Gerekçe", null, null, [new(null, "Kalem", "ADET", miktar, fiyat, 20)]);

    private static SatinalmaTalebi Talep() => SatinalmaTalebi.Olustur("TL00001", TalepEden, "Talep Eden", Istek()).Deger;

    private static OnayBasamagi Basamak(int sira, decimal enAz = 0) => OnayBasamagi.Olustur(sira, enAz, Rol, null, $"Rol {sira}");

    [Fact]
    public void Toplam_kalemlerden_hesaplanir() => Assert.Equal(200m, Talep().Toplam);

    [Fact]
    public void Kalemsiz_talep_olmaz() =>
        Assert.False(SatinalmaTalebi.Olustur("TL1", TalepEden, "A", Istek() with { Kalemler = [] }).BasariliMi);

    [Theory]
    [InlineData(5000, 1)]
    [InlineData(10000, 2)]
    [InlineData(99999.99, 2)]
    [InlineData(100000, 3)]
    public void Uygulanan_basamak_tutara_bagli(decimal toplam, int beklenen) =>
        Assert.Equal(beklenen, OnayKurallari.Uygulanan([Basamak(1), Basamak(2, 10000), Basamak(3, 100000)], toplam).Count);

    [Fact]
    public void Basamaklar_sirayla_uygulanir()
    {
        var sonuc = OnayKurallari.Uygulanan([Basamak(2, 10), Basamak(1)], 100);

        Assert.Equal([1, 2], sonuc.Select(b => b.Sira));
    }

    [Fact]
    public void Rol_kisi_ve_yonetici_yetkisi()
    {
        var kisi = Guid.NewGuid();
        Assert.True(OnayKurallari.YetkiliMi(Rol, null, kisi, Rol, false));
        Assert.False(OnayKurallari.YetkiliMi(Rol, null, kisi, Guid.NewGuid(), false));
        Assert.True(OnayKurallari.YetkiliMi(null, kisi, kisi, null, false));
        Assert.False(OnayKurallari.YetkiliMi(null, Guid.NewGuid(), kisi, Rol, false));
        Assert.False(OnayKurallari.YetkiliMi(Rol, null, kisi, null, false));
        Assert.True(OnayKurallari.YetkiliMi(Rol, null, kisi, null, true));
    }

    [Fact]
    public void Basamaklar_sirayla_ilerler_son_onayla_onaylanir()
    {
        var talep = Talep();
        talep.OnayaGonder([Basamak(1), Basamak(2)]);
        Assert.Equal(TalepDurumu.Onayda, talep.Durum);
        Assert.Equal(1, talep.BekleyenOnay!.Sira);

        Assert.True(talep.Karar(Onayci, "Onaycı", true, true, null, Simdi).BasariliMi);
        Assert.Equal(TalepDurumu.Onayda, talep.Durum);
        Assert.Equal(2, talep.BekleyenOnay!.Sira);

        Assert.True(talep.Karar(Onayci, "Onaycı", true, true, "tamam", Simdi).BasariliMi);
        Assert.Equal(TalepDurumu.Onaylandi, talep.Durum);
        Assert.Null(talep.BekleyenOnay);
        Assert.Equal(Simdi, talep.KararTarihi);
    }

    [Fact]
    public void Kimse_kendi_talebini_onaylayamaz_yonetici_olsa_bile()
    {
        var talep = Talep();
        talep.OnayaGonder([Basamak(1)]);

        var sonuc = talep.Karar(TalepEden, "Talep Eden", true, true, null, Simdi);

        Assert.False(sonuc.BasariliMi);
        Assert.Equal("kendi_talebi", sonuc.Hata!.Kod);
        Assert.Equal(TalepDurumu.Onayda, talep.Durum);
    }

    [Fact]
    public void Yetkisiz_karar_veremez()
    {
        var talep = Talep();
        talep.OnayaGonder([Basamak(1)]);

        Assert.Equal("onay_sirasi", talep.Karar(Onayci, "O", false, true, null, Simdi).Hata!.Kod);
    }

    [Fact]
    public void Red_gerekce_ister_ve_talebi_sonlandirir()
    {
        var talep = Talep();
        talep.OnayaGonder([Basamak(1), Basamak(2)]);

        Assert.Equal("red_gerekcesi", talep.Karar(Onayci, "O", true, false, " ", Simdi).Hata!.Kod);
        Assert.True(talep.Karar(Onayci, "O", true, false, "Bütçe yok", Simdi).BasariliMi);
        Assert.Equal(TalepDurumu.Reddedildi, talep.Durum);
        Assert.Equal("Bütçe yok", talep.Onaylar[0].Not);
        Assert.False(talep.Karar(Onayci, "O", true, true, null, Simdi).BasariliMi);
    }

    [Fact]
    public void Reddedilen_duzeltilip_yeniden_gonderilir_eski_kararlar_kalir()
    {
        var talep = Talep();
        talep.OnayaGonder([Basamak(1)]);
        talep.Karar(Onayci, "O", true, false, "Fiyat yüksek", Simdi);

        Assert.True(talep.Guncelle(Istek(50)).BasariliMi);
        Assert.Equal(100m, talep.Toplam);
        Assert.True(talep.OnayaGonder([Basamak(1)]).BasariliMi);

        Assert.Equal(2, talep.GonderimNo);
        Assert.Equal(2, talep.Onaylar.Count);
        Assert.Equal(2, talep.BekleyenOnay!.GonderimNo);
        Assert.Equal(OnayDurumu.Reddedildi, talep.Onaylar[0].Durum);
    }

    [Fact]
    public void Onaydaki_talep_degistirilemez_ve_tekrar_gonderilemez()
    {
        var talep = Talep();
        talep.OnayaGonder([Basamak(1)]);

        Assert.False(talep.Guncelle(Istek(1)).BasariliMi);
        Assert.False(talep.OnayaGonder([Basamak(1)]).BasariliMi);
    }

    [Fact]
    public void Yalniz_onaylanan_siparise_donusur_iptal_siparisten_yeniden_donebilir()
    {
        var talep = Talep();
        Assert.False(talep.SipariseDonusebilirMi(false).BasariliMi);

        talep.OnayaGonder([Basamak(1)]);
        talep.Karar(Onayci, "O", true, true, null, Simdi);
        Assert.True(talep.SipariseDonusebilirMi(false).BasariliMi);

        talep.SipariseDonustu(Guid.NewGuid());
        Assert.Equal(TalepDurumu.SipariseDonustu, talep.Durum);
        Assert.False(talep.SipariseDonusebilirMi(true).BasariliMi);
        Assert.True(talep.SipariseDonusebilirMi(false).BasariliMi);
        Assert.False(talep.IptalEt().BasariliMi);
    }
}
