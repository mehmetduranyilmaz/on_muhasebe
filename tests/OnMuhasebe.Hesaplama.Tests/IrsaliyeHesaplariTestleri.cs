using OnMuhasebe.Modules.Fatura.Contracts;

namespace OnMuhasebe.Hesaplama.Tests;

public class IrsaliyeHesaplariTestleri
{
    [Fact]
    public void Fatura_sevkten_itibaren_yedi_gun_icinde_kesilmelidir()
    {
        var sevk = new DateOnly(2026, 10, 1);

        Assert.Equal(new DateOnly(2026, 10, 8), IrsaliyeHesaplari.FaturaSonGunu(sevk));
        Assert.False(IrsaliyeHesaplari.FaturaSuresiGectiMi(sevk, new DateOnly(2026, 10, 8)));
        Assert.True(IrsaliyeHesaplari.FaturaSuresiGectiMi(sevk, new DateOnly(2026, 10, 9)));
    }

    [Fact]
    public void Faturalama_durumu_satirlarin_faturalanan_miktarina_gore_belirlenir()
    {
        Assert.Equal(FaturalamaDurumu.Faturalanmadi, IrsaliyeHesaplari.Durum([(10m, 0m), (5m, 0m)]));
        Assert.Equal(FaturalamaDurumu.KismenFaturalandi, IrsaliyeHesaplari.Durum([(10m, 10m), (5m, 0m)]));
        Assert.Equal(FaturalamaDurumu.KismenFaturalandi, IrsaliyeHesaplari.Durum([(10m, 4m)]));
        Assert.Equal(FaturalamaDurumu.Faturalandi, IrsaliyeHesaplari.Durum([(10m, 10m), (5m, 5m)]));
    }

    [Fact]
    public void Kalan_miktar_eksiye_dusmez()
    {
        Assert.Equal(6m, IrsaliyeHesaplari.Kalan(10m, 4m));
        Assert.Equal(0m, IrsaliyeHesaplari.Kalan(10m, 12m));
    }

    [Fact]
    public void Faturalanmamis_alis_irsaliyesi_tahmini_maliyetle_kalir()
    {
        Assert.Equal(50m, IrsaliyeHesaplari.AgirlikliMaliyet(10m, 50m, []));
    }

    [Fact]
    public void Tamami_faturalanan_satir_fatura_maliyetini_alir()
    {
        Assert.Equal(55m, IrsaliyeHesaplari.AgirlikliMaliyet(10m, 50m, [(10m, 55m)]));
    }

    [Fact]
    public void Kismi_faturalamada_maliyet_faturali_ve_tahmini_kisim_ile_agirliklandirilir()
    {
        // 10 adet tahmini 50'den girdi; 4'ü 55'ten, 3'ü 52'den faturalandı, 3'ü bekliyor:
        // (4×55 + 3×52 + 3×50) / 10 = (220 + 156 + 150) / 10 = 52,6
        Assert.Equal(52.6m, IrsaliyeHesaplari.AgirlikliMaliyet(10m, 50m, [(4m, 55m), (3m, 52m)]));
    }

    [Fact]
    public void Agirlikli_maliyet_dort_haneye_yuvarlanir()
    {
        // (1×10 + 2×0) / 3 = 3,33333... -> 3,3333
        Assert.Equal(3.3333m, IrsaliyeHesaplari.AgirlikliMaliyet(3m, 0m, [(1m, 10m)]));
    }

    [Fact]
    public void Irsaliye_ve_fatura_turleri_karsilikli_eslesir()
    {
        foreach (var tur in Enum.GetValues<IrsaliyeTuru>().Where(IrsaliyeTurleri.FaturalanirMi))
        {
            Assert.Equal(tur, IrsaliyeTurleri.Tur(IrsaliyeTurleri.FaturaTuru(tur)!.Value));
        }

        Assert.Null(IrsaliyeTurleri.FaturaTuru(IrsaliyeTuru.DepoSevk));
        Assert.Null(IrsaliyeTurleri.Tur(FaturaTuru.GiderPusulasi));
    }
}
