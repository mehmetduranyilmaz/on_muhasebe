using OnMuhasebe.Modules.Cari;
using OnMuhasebe.Modules.Fatura;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Modules.KasaBanka;
using OnMuhasebe.Modules.Stok;
using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Modules.Yonetim;
using OnMuhasebe.Modules.Yonetim.Domain;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Infrastructure.Kimlik;

namespace OnMuhasebe.Hesaplama.Tests;

public class YetkiTestleri
{
    // Kurucu, bağımlı yetkilerin katalogda olduğunu da denetler.
    private static readonly YetkiKatalogu _katalog = new([new YonetimModulu(), new CariModulu(), new StokModulu(), new FaturaModulu(), new KasaBankaModulu()]);

    private static IReadOnlyList<string> Hesapla(string[] rol, string[]? ek = null, string[]? yasak = null) =>
        YetkiHesaplayici.Hesapla(rol, ek ?? [], yasak ?? [], _katalog.Tumu);

    [Fact]
    public void Katalog_kodlari_benzersiz_ve_her_modul_lisans_koduna_bagli()
    {
        Assert.Equal(_katalog.Tumu.Count, _katalog.Tumu.Select(k => k.Kod).Distinct().Count());
        Assert.All(_katalog.Tumu, k => Assert.True(k.Modul.Length == 0 || ModulKodlari.Tumu.Contains(k.Modul), k.Kod));
    }

    [Fact]
    public void Rol_ek_ve_yasak_birlesir_gecersiz_kod_atilir()
    {
        var sonuc = Hesapla(["cari.hesap.goruntule", "cari.hesap.sil", "uydurma.yetki"], ek: ["cari.hesap.ekle"], yasak: ["cari.hesap.sil"]);

        Assert.Contains("cari.hesap.ekle", sonuc);
        Assert.DoesNotContain("cari.hesap.sil", sonuc);
        Assert.DoesNotContain("uydurma.yetki", sonuc);
    }

    [Fact]
    public void Islem_yetkisi_kaynagi_gormeyi_ve_bagimli_yetkileri_zincirleme_getirir()
    {
        var sonuc = Hesapla([FaturaYetkileri.Belge.Yetki(YetkiIslemleri.Ekle)]);

        Assert.Contains("fatura.belge.goruntule", sonuc);
        Assert.Contains("cari.hesap.goruntule", sonuc);
        Assert.Contains("stok.kart.goruntule", sonuc);
        // stok.kart -> stok.tanim, cari.hesap -> cari.grup: dolaylı bağımlılık
        Assert.Contains("stok.tanim.goruntule", sonuc);
        Assert.Contains("cari.grup.goruntule", sonuc);
        Assert.DoesNotContain("fatura.belge.onayla", sonuc);
        Assert.DoesNotContain("cari.hesap.ekle", sonuc);
    }

    [Fact]
    public void Bagimli_yetki_yasakla_kaldirilamaz()
    {
        var sonuc = Hesapla([FaturaYetkileri.Belge.Yetki(YetkiIslemleri.Ekle)], yasak: ["cari.hesap.goruntule"]);

        Assert.Contains("cari.hesap.goruntule", sonuc);
    }

    [Fact]
    public void Bos_rol_hicbir_yetki_vermez() => Assert.Empty(Hesapla([]));

    [Fact]
    public void Sablonlarda_yonetici_tam_yetkili_digerleri_gecerli_yetkiler_tasir()
    {
        var roller = RolSablonlari.Olustur(_katalog.Tumu, Guid.CreateVersion7());

        Assert.True(roller[0].TamYetkiliMi);
        Assert.Single(roller, r => r.TamYetkiliMi);
        Assert.Equal(roller.Count, roller.Select(r => r.Ad).Distinct().Count());
        Assert.All(roller.SelectMany(r => r.Yetkiler), y => Assert.Contains(y, _katalog.Yetkiler));
    }

    [Fact]
    public void Kullanici_sablonu_yonetim_disindaki_tum_yetkileri_kapsar()
    {
        var kullanici = RolSablonlari.Olustur(_katalog.Tumu, Guid.CreateVersion7()).Single(r => r.Ad == "Kullanıcı");
        var modulYetkileri = _katalog.Tumu.Where(k => k.Modul.Length > 0).SelectMany(k => k.Yetkiler()).Order();

        Assert.Equal(modulYetkileri, kullanici.Yetkiler);
    }

    [Fact]
    public void Salt_okur_sablonu_yalnizca_goruntuler_ve_maliyeti_gorur()
    {
        var saltOkur = RolSablonlari.Olustur(_katalog.Tumu, Guid.CreateVersion7()).Single(r => r.Ad.StartsWith("Salt Okur", StringComparison.Ordinal));

        Assert.All(saltOkur.Yetkiler, y => Assert.EndsWith(".goruntule", y));
        Assert.Contains(StokYetkileri.Maliyet.Yetki(YetkiIslemleri.Goruntule), saltOkur.Yetkiler);
        Assert.DoesNotContain(YonetimYetkileri.Kullanici.Yetki(YetkiIslemleri.Goruntule), saltOkur.Yetkiler);
    }

    [Fact]
    public void Satis_sablonu_maliyet_gormez()
    {
        var satis = RolSablonlari.Olustur(_katalog.Tumu, Guid.CreateVersion7()).Single(r => r.Ad == "Satış");
        var etkin = Hesapla([.. satis.Yetkiler]);

        Assert.DoesNotContain(StokYetkileri.Maliyet.Yetki(YetkiIslemleri.Goruntule), etkin);
    }

    [Fact]
    public void Tam_yetkili_her_yetkiye_sahiptir()
    {
        var yetkiler = new KullaniciYetkileri(true, [], [], null);

        Assert.True(yetkiler.VarMi("fatura.belge.onayla"));
        Assert.False(KullaniciYetkileri.Yok.VarMi("fatura.belge.goruntule"));
    }
}
