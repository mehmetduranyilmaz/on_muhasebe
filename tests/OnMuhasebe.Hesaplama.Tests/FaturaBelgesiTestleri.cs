using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Modules.Fatura.Domain;

namespace OnMuhasebe.Hesaplama.Tests;

public class FaturaBelgesiTestleri
{
    private static readonly DateOnly _tarih = new(2026, 10, 20);
    private static readonly DateTime _simdi = new(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);
    private static readonly FaturaCarisi _cari = new(Guid.NewGuid(), "C00001", "Deneme A.Ş.", "1234567890", "Kadıköy", null, null, null);

    private static FaturaBilgileri Bilgiler(
        FaturaTuru tur = FaturaTuru.Satis, string? numara = null, string paraBirimi = "TRY", decimal kur = 1m,
        GenelIskontoTuru iskontoTuru = GenelIskontoTuru.Yuzde, decimal genelIskonto = 0m, DateOnly? vade = null) =>
        new(tur, null, _cari, _tarih, vade, numara, paraBirimi, kur, iskontoTuru, genelIskonto, null,
            [new FaturaSatiriBilgisi(null, null, "Danışmanlık", false, null, "ADET", 2m, 500m, false, 20, 0m, 0m, 0m)]);

    [Fact]
    public void Yeni_fatura_taslaktir_ve_toplamlar_hesaplanir()
    {
        var f = FaturaBelgesi.Olustur(Bilgiler()).Deger;

        Assert.Equal(FaturaDurumu.Taslak, f.Durum);
        Assert.Equal((1000m, 200m, 1200m, 1200m), (f.Matrah, f.KdvToplami, f.GenelToplam, f.GenelToplamTl));
        Assert.Equal(SatisSekli.Toptan, f.SatisSekli);
        Assert.Single(f.Satirlar);
    }

    [Fact]
    public void Dovizli_faturanin_tl_karsiligi_kurla_hesaplanir()
    {
        var f = FaturaBelgesi.Olustur(Bilgiler(paraBirimi: "USD", kur: 41.2345678m)).Deger;

        Assert.Equal(41.234568m, f.Kur);
        Assert.Equal(49481.48m, f.GenelToplamTl);
    }

    [Fact]
    public void Alis_faturasinda_satis_sekli_olmaz()
    {
        var f = FaturaBelgesi.Olustur(Bilgiler(FaturaTuru.Alis, "ABC2026000000123")).Deger;

        Assert.Null(f.SatisSekli);
        Assert.Equal("ABC2026000000123", f.Numara);
    }

    [Fact]
    public void Giden_fatura_onayda_verilen_numarayi_alir()
    {
        var f = FaturaBelgesi.Olustur(Bilgiler()).Deger;

        Assert.True(f.Onayla("TPT2026000000001", _simdi).BasariliMi);
        Assert.Equal((FaturaDurumu.Onayli, "TPT2026000000001"), (f.Durum, f.Numara));
    }

    [Fact]
    public void Numarasiz_gelen_fatura_onaylanamaz()
    {
        var f = FaturaBelgesi.Olustur(Bilgiler(FaturaTuru.Alis)).Deger;

        Assert.Equal("numara_gerekli", f.Onayla(null, _simdi).Hata?.Kod);
    }

    [Fact]
    public void Taslak_iptal_edilemez_onayli_iptal_edilir_ve_numarasini_korur()
    {
        var f = FaturaBelgesi.Olustur(Bilgiler()).Deger;
        Assert.Equal("taslak_iptal", f.IptalEt(_simdi).Hata?.Kod);

        f.Onayla("TPT2026000000001", _simdi);
        Assert.True(f.IptalEt(_simdi).BasariliMi);
        Assert.Equal((FaturaDurumu.Iptal, "TPT2026000000001"), (f.Durum, f.Numara));
        Assert.Equal("fatura_iptal", f.Guncelle(Bilgiler()).Hata?.Kod);
    }

    [Fact]
    public void Tur_degistirilemez()
    {
        var f = FaturaBelgesi.Olustur(Bilgiler()).Deger;

        Assert.Equal("tur_degistirilemez", f.Guncelle(Bilgiler(FaturaTuru.Alis, "X1")).Hata?.Kod);
    }

    [Theory]
    [InlineData(1001.0, "genel_iskonto_fazla")]
    [InlineData(1000.0, null)]
    public void Tutar_genel_iskonto_net_toplami_asamaz(double iskonto, string? beklenen)
    {
        var sonuc = FaturaBelgesi.Olustur(Bilgiler(iskontoTuru: GenelIskontoTuru.Tutar, genelIskonto: (decimal)iskonto));

        Assert.Equal(beklenen, sonuc.Hata?.Kod);
    }

    [Fact]
    public void Vade_tarihi_fatura_tarihinden_once_olamaz()
    {
        Assert.Equal("vade_gecersiz", FaturaBelgesi.Olustur(Bilgiler(vade: _tarih.AddDays(-1))).Hata?.Kod);
    }

    [Fact]
    public void Guncellemede_satirlar_yenilenir()
    {
        var f = FaturaBelgesi.Olustur(Bilgiler()).Deger;
        var eskiSatir = f.Satirlar.Single().Id;
        var yeni = Bilgiler() with
        {
            Satirlar =
            [
                new FaturaSatiriBilgisi(null, null, "A", false, null, "ADET", 1m, 10m, false, 20, 0m, 0m, 0m),
                new FaturaSatiriBilgisi(null, null, "B", false, null, "ADET", 1m, 20m, false, 10, 0m, 0m, 0m)
            ]
        };

        Assert.True(f.Guncelle(yeni).BasariliMi);
        Assert.Equal([1, 2], f.Satirlar.Select(s => s.Sira));
        Assert.DoesNotContain(f.Satirlar, s => s.Id == eskiSatir);
        Assert.Equal(34m, f.GenelToplam);
    }
}
