using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OnMuhasebe.Tarayici.Testler;

// Bildirim ekranlarını dener.
internal sealed class BildirimTesti : TarayiciTesti
{
    public override string Ad => "bildirim";

    protected override async Task CalistirAsync()
    {
        await Komut("Page.enable");
        await Komut("Network.enable");
        var eposta = $"bildirimui{DateTime.UtcNow.Ticks}@ornek.com";
        var kayit = (await (await http.PostAsJsonAsync(Api + "api/yonetim/kayit",
            new { firmaUnvani = "Bildirim Arayuz", ad = "Zeynep", soyad = "Ak", eposta, sifre = "Sifre1234" })).Content.ReadFromJsonAsync<JsonObject>())!;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", kayit["accessToken"]!.GetValue<string>());
        var cariId = (await Post("api/cari/hesaplar", new { unvan = "Cekli Musteri", tur = "Musteri", kisiTipi = "Tuzel", paraBirimi = "TRY" }))["id"]!.GetValue<string>();
        var bugun = DateTime.Today;
        await Post("api/kasabanka/cek-senet/giris", new
        {
            cariHesapId = cariId, tarih = bugun.AddDays(-3).ToString("yyyy-MM-dd"), riskOnaylandi = true,
            evraklar = new[]
            {
                new { tur = "Cek", seriNo = "7001", vadeTarihi = bugun.ToString("yyyy-MM-dd"), tutar = 2500, paraBirimi = "TRY", borclu = new { ad = "Kesideci", kimlikNo = "9876543217" }, bankaAdi = "Akbank" },
                new { tur = "Cek", seriNo = "7002", vadeTarihi = bugun.AddDays(2).ToString("yyyy-MM-dd"), tutar = 1500, paraBirimi = "TRY", borclu = new { ad = "Kesideci", kimlikNo = "9876543217" }, bankaAdi = "Akbank" }
            }
        });
        var sistem = new HttpClient();
        var sistemToken = (await (await sistem.PostAsJsonAsync(Api + "api/yonetim/giris", new { eposta = SistemEposta, sifre = SistemSifre })).Content.ReadFromJsonAsync<JsonObject>())!["accessToken"]!.GetValue<string>();
        sistem.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sistemToken);
        (await sistem.PostAsJsonAsync(Api + "api/bildirim/sistem/calistir", new { })).EnsureSuccessStatusCode();

        await GirisYap(eposta, "Sifre1234");
        Kontrol(await Bekle("document.querySelector('.zil-dugmesi .zil-sayisi')?.textContent.trim() === '2'", 15), "Zilde 2 okunmamis bildirim");
        Kontrol(await Js("document.querySelector('.zil-dugmesi').classList.contains('var')") == "true", "Zil sallaniyor (okunmamis var)");
        await Js("document.querySelector('.zil-dugmesi').click()");
        Kontrol(await Bekle("document.querySelectorAll('.bildirim-menusu .bildirim-satiri').length === 2", 10), "Menude iki bildirim");
        Kontrol(await Bekle($"{Govde("Çek bugün vadeli: tahsil zamanı")} && {Govde("Çek vadesine 2 gün kaldı")}", 5), "Basliklar dogru");
        await Ekran("1-zil");
        await Js("[...document.querySelectorAll('.bildirim-menusu .bildirim-satiri')].find(b => b.textContent.includes('bugün vadeli')).click()");
        Kontrol(await Bekle("location.pathname.startsWith('/cek-senet/') && document.body.innerText.includes('7001')", 15), "Tiklaninca evrak acildi");
        Kontrol(await Bekle("document.querySelector('.zil-dugmesi .zil-sayisi')?.textContent.trim() === '1'", 10), "Sayac bire dustu");

        await Git("/bildirimler");
        Kontrol(await Bekle("document.querySelectorAll('.bildirim-listesi .bildirim-satiri').length === 2", 15), "Bildirimler sayfasi: iki kayit");
        Kontrol(await Js("document.querySelectorAll('.bildirim-listesi .bildirim-satiri.okunmamis').length") == "1", "Biri okunmus, biri okunmamis");
        await Js("document.querySelector('.mud-switch input').click()");
        Kontrol(await Bekle("document.querySelectorAll('.bildirim-listesi .bildirim-satiri').length === 1", 10), "Yalnizca okunmamislar filtresi");
        await Ekran("2-sayfa");
        await Tikla("Tümünü okundu say");
        Kontrol(await Bekle("!document.querySelector('.zil-dugmesi .zil-sayisi')", 10), "Tumu okundu: zil sayisi kalkti");

        await Tikla("Tercihlerim");
        Kontrol(await Bekle(Govde("Bildirim almak istiyorum"), 10), "Tercihler sekmesi");
        Kontrol(await Js("[...document.querySelectorAll('button')].find(b => b.textContent.includes('Tercihlerimi kaydet')).disabled") == "true", "Degisiklik yokken kaydet kapali");
        await Js("[...document.querySelectorAll('.zorunlu-alan')].find(b => b.textContent.includes('Risk limiti')).click()");
        await Js("[...document.querySelectorAll('.ayar-secenek')].find(d => d.textContent.includes('Anında')).querySelector('input').click()");
        await Task.Delay(300);
        await Ekran("3-tercihler");
        await Tikla("Tercihlerimi kaydet");
        Kontrol(await Bekle(Govde("Tercihleriniz kaydedildi."), 10), "Tercihler kaydedildi");
        var tercih = await http.GetFromJsonAsync<JsonObject>(Api + "api/bildirim/tercihler");
        Kontrol(tercih!["eposta"]!.GetValue<string>() == "Aninda" && tercih["kapaliTurler"]!.AsArray().Count == 1, "API'de tercih: aninda, risk kapali");

        await Git("/ayarlar");
        Kontrol(await Bekle($"{Govde("Firmada üretilecek türler")} && !!{Alan("Vade kaç gün önce hatırlatılsın")}", 20), "Ayarlarda bildirim paneli");
        await Doldur("Vade kaç gün önce hatırlatılsın", "10");
        await Task.Delay(300);
        await Tikla("Bildirim ayarlarını kaydet");
        Kontrol(await Bekle(Govde("Bildirim ayarları kaydedildi."), 10), "Firma bildirim ayari kaydedildi");

        // --- Sistem yöneticisi
        await GirisYap(SistemEposta, SistemSifre);
        Kontrol(await Js("!!document.querySelector('.zil-dugmesi')") == "false", "Sistem yoneticisinde (firmasiz) zil yok");
        await Git("/sistem/eposta");
        Kontrol(await Bekle($"{Alan("Sunucu")}?.value === 'localhost' && {Alan("Port")}?.value === '1025'", 15), "SMTP ayarlari yuklendi");
        await Doldur("Alıcı", "uitest@ornek.com");
        await Task.Delay(300);
        await Tikla("Test gönder");
        Kontrol(await Bekle(Govde("Test e-postası uitest@ornek.com adresine gönderildi."), 15), "Test e-postasi gonderildi");
        await Tikla("Bildirimleri şimdi üret");
        Kontrol(await Bekle(Govde("firma tarandı"), 30), "Elle uretim sonucu");
        await Ekran("4-sistem-eposta");
    }
}
