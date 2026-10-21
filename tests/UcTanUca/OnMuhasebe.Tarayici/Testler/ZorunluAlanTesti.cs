using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OnMuhasebe.Tarayici.Testler;

// Zorunlu alan ekranlarını dener.
internal sealed class ZorunluAlanTesti : TarayiciTesti
{
    public override string Ad => "zorunlu";

    protected override async Task CalistirAsync()
    {
        await Komut("Page.enable");
        var eposta = $"zorunluui{DateTime.UtcNow.Ticks}@ornek.com";
        await KayitOlAsync(new { firmaUnvani = "Zorunlu Arayuz Ltd", ad = "Ece", soyad = "Ak", eposta, sifre = "Sifre1234" });

        await Git("/giris");
        await Bekle("document.querySelectorAll('input').length >= 2");
        await Js("performance.setResourceTimingBufferSize(5000)");
        await Doldur("E-posta", eposta);
        await Doldur("Şifre", "Sifre1234");
        await Tikla("Giriş Yap");
        await Bekle(Govde("Hoş"), 15);

        await Git("/cari/yeni");
        await Bekle("!!" + Alan("Telefon"), 15);
        Kontrol((await Js(Etiket("Telefon"))) == "serbest", "Ayar yokken telefon zorunlu isaretli degil");

        await Git("/ayarlar");
        Kontrol(await Bekle($"{Govde("Zorunlu alanlar")} && {Govde("Cari kartı")} && {Govde("Stok kartı")} && {Govde("Çek/senet bordrosu")}", 20),
            "Ayarlarda zorunlu alanlar paneli, lisansli formlar gruplu");
        await Js("[...document.querySelectorAll('.zorunlu-alanlar')].find(g => g.closest('.mud-grid-item').textContent.includes('Cari kartı'))"
            + ".querySelectorAll('.zorunlu-alan').forEach(b => { if (b.textContent.trim() === 'Telefon') b.click(); })");
        Kontrol(await Bekle("[...document.querySelectorAll('.zorunlu-alanlar')].find(g => g.closest('.mud-grid-item').textContent.includes('Cari kartı'))"
            + ".querySelectorAll('.zorunlu-alan.secili').length === 1"), "Telefon secildi");
        await Js("[...document.querySelectorAll('.zorunlu-alanlar')].find(g => g.closest('.mud-grid-item').textContent.includes('Cari kartı')).scrollIntoView()");
        await Ekran("1-ayar");
        await Tikla("Zorunlu alanları kaydet");
        Kontrol(await Bekle(Govde("Zorunlu alanlar kaydedildi.")), "Ayar kaydedildi");

        await Git("/cari/yeni");
        await Bekle("!!" + Alan("Telefon"), 15);
        Kontrol(await Bekle($"{Etiket("Telefon")} === 'zorunlu'", 10), "Cari formunda telefon zorunlu (*) isaretlendi");
        await Doldur("Ünvan", "Yildiz Market");
        await Task.Delay(300);
        await Kaydet();
        Kontrol(await Bekle($"{Govde("Bu alan zorunlu (firma ayarı).")} && location.pathname === '/cari/yeni'", 10), "Telefon bos: form kaydetmedi, alan altinda uyari");
        await Ekran("2-form");
        await Doldur("Telefon", "0212 555 44 33");
        await Task.Delay(300);
        await Kaydet();
        Kontrol(await Bekle($"location.pathname.startsWith('/cari/') && location.pathname !== '/cari/yeni' && {Govde("Yildiz Market")}", 15), "Telefon dolunca cari kaydedildi");
    }
}
