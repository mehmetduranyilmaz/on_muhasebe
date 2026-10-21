using Microsoft.Extensions.Caching.Memory;
using OnMuhasebe.Modules.Yonetim.Infrastructure.Kimlik;

namespace OnMuhasebe.Hesaplama.Tests;

// Hesap kilidi e-posta + IP çiftine bağlı: saldırgan kendi IP'sinden kurbanı kilitleyemez.
public class GirisDenemeleriTestleri
{
    private const string Eposta = "kurban@ornek.com";

    private static GirisDenemeleri Yeni() => new(new MemoryCache(new MemoryCacheOptions()));

    private static void Dene(GirisDenemeleri d, string ip, int kez)
    {
        for (var i = 0; i < kez; i++)
        {
            d.Basarisiz(Eposta, ip);
        }
    }

    [Fact]
    public void Saldirganin_ip_si_kilitlenir_kurbaninki_kilitlenmez()
    {
        var d = Yeni();
        Dene(d, "10.0.0.66", GirisDenemeleri.IpBasinaAzami);
        Assert.True(d.KilitliMi(Eposta, "10.0.0.66"));
        Assert.False(d.KilitliMi(Eposta, "192.168.1.5"));
    }

    [Fact]
    public void Basarili_giris_ayni_ip_nin_sayacini_sifirlar()
    {
        var d = Yeni();
        Dene(d, "1.1.1.1", GirisDenemeleri.IpBasinaAzami - 1);
        d.Basarili(Eposta, "1.1.1.1");
        Dene(d, "1.1.1.1", GirisDenemeleri.IpBasinaAzami - 1);
        Assert.False(d.KilitliMi(Eposta, "1.1.1.1"));
    }

    [Fact]
    public void Dagitik_denemede_eposta_geneli_sinir_devreye_girer()
    {
        var d = Yeni();
        for (var i = 0; i < GirisDenemeleri.EpostaBasinaAzami; i++)
        {
            d.Basarisiz(Eposta, $"10.0.{i / 250}.{i % 250}");
        }

        Assert.True(d.KilitliMi(Eposta, "172.16.0.1"));
    }

    [Fact]
    public void Sifre_sifirlama_tum_kilitleri_kaldirir()
    {
        var d = Yeni();
        Dene(d, "10.0.0.66", GirisDenemeleri.IpBasinaAzami);
        Dene(d, "10.0.0.67", GirisDenemeleri.IpBasinaAzami);
        d.Sifirla(Eposta);
        Assert.False(d.KilitliMi(Eposta, "10.0.0.66"));
        Assert.False(d.KilitliMi(Eposta, "10.0.0.67"));
    }

    [Fact]
    public void Kayitli_olmayan_e_posta_da_ayni_sayilir()
    {
        var d = Yeni();
        Dene(d, "1.1.1.1", GirisDenemeleri.IpBasinaAzami);
        Assert.True(d.KilitliMi(Eposta, "1.1.1.1"));
        Assert.False(d.KilitliMi("baska@ornek.com", "1.1.1.1"));
    }
}
