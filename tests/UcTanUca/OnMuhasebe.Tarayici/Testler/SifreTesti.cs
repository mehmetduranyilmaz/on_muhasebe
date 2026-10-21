using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OnMuhasebe.Tarayici.Testler;

// Şifremi unuttum (Mailpit'teki bağlantıyla), yeni şifre ve menüden şifre değiştirme ekranlarını dener; yazı tipinin
// kendi sunucumuzdan geldiğini de denetler.
internal sealed class SifreTesti : TarayiciTesti
{
    public override string Ad => "sifre";

    protected override async Task CalistirAsync()
    {
        await Komut("Page.enable");
        await Komut("Network.enable");
        var eposta = $"sifreui{DateTime.UtcNow.Ticks}@ornek.com";
        await KayitOlAsync(new { firmaUnvani = "Sifre Arayuz", ad = "Selin", soyad = "Yeni", eposta, sifre = "Sifre1234" });

        // Sıfırlama e-postası Mailpit'e gitsin.
        var sistem = YeniIstemci();
        var sistemToken = (await (await sistem.PostAsJsonAsync(Api + "api/yonetim/giris", new { eposta = SistemEposta, sifre = SistemSifre }))
            .Content.ReadFromJsonAsync<JsonObject>())!["accessToken"]!.GetValue<string>();
        sistem.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sistemToken);
        (await sistem.PutAsJsonAsync(Api + "api/bildirim/sistem/eposta", new
        {
            aktifMi = true, sunucu = "localhost", port = 1025, guvenlik = "Yok", kullaniciAdi = (string?)null,
            gonderenAdres = "bildirim@onmuhasebe.local", gonderenAd = "On Muhasebe", sifreKayitliMi = false
        })).EnsureSuccessStatusCode();

        // --- Şifremi unuttum
        await Git("/giris");
        Kontrol(await Bekle("[...document.querySelectorAll('a')].some(a => a.textContent.trim() === 'Şifremi unuttum')", 15), "Giriste 'Sifremi unuttum' baglantisi");
        await Js("[...document.querySelectorAll('a')].find(a => a.textContent.trim() === 'Şifremi unuttum').click()");
        Kontrol(await Bekle("location.pathname === '/sifremi-unuttum' && " + Govde("Şifrenizi mi unuttunuz?"), 10), "Sifremi unuttum sayfasi");
        await Doldur("E-posta", eposta);
        await Ekran("1-sifremi-unuttum");
        await Tikla("Bağlantı gönder");
        Kontrol(await Bekle(Govde("şifre sıfırlama bağlantısı gönderildi"), 10), "Gonderildi mesaji");
        await Ekran("2-gonderildi");

        var kod = await MailpitKoduAsync(eposta, "sifre-sifirla");
        Kontrol(kod is { Length: >= 40 }, "Mailpit'te sifirlama baglantisi");

        // --- Yeni şifre
        await Git("/sifre-sifirla");
        Kontrol(await Bekle(Govde("Bağlantı eksik"), 10), "Kodsuz baglantida uyari");
        await Git($"/sifre-sifirla#kod={kod}");
        await Bekle("!!" + Alan("Yeni şifre"), 10);
        await Doldur("Yeni şifre", "Unuttum123");
        await Doldur("Yeni şifre (tekrar)", "Farkli123");
        await Tikla("Şifreyi kaydet");
        Kontrol(await Bekle(Govde("Şifreler eşleşmiyor."), 5), "Tekrar eslesmiyor uyarisi");
        await Doldur("Yeni şifre (tekrar)", "Unuttum123");
        await Ekran("3-yeni-sifre");
        await Tikla("Şifreyi kaydet");
        Kontrol(await Bekle($"location.pathname === '/giris' && {Govde("Şifreniz değiştirildi.")}", 15), "Sifre kaydedildi, girise donuldu");

        await Git($"/sifre-sifirla#kod={kod}");
        await Bekle("!!" + Alan("Yeni şifre"), 10);
        await Doldur("Yeni şifre", "Tekrar1234");
        await Doldur("Yeni şifre (tekrar)", "Tekrar1234");
        await Tikla("Şifreyi kaydet");
        Kontrol(await Bekle($"{Govde("geçersiz ya da süresi dolmuş")} && {Govde("Yeni bağlantı iste")}", 10), "Kullanilmis baglanti reddedildi, yeni baglanti onerisi");

        await GirisYap(eposta, "Unuttum123");
        Kontrol(await Bekle(Govde("Hoş geldiniz, Selin"), 15), "Yeni sifreyle giris");

        // --- Menüden şifre değiştirme
        await Js("document.querySelector('.avatar-dugmesi').click()");
        await Bekle("[...document.querySelectorAll('.mud-menu-item')].some(e => e.textContent.includes('Şifre değiştir'))", 5);
        await Tikla("Şifre değiştir");
        Kontrol(await Bekle("!!document.querySelector('.mud-dialog') && " + Govde("diğer cihazlardaki oturumlarınız kapanır"), 10), "Sifre degistir diyalogu");
        await Doldur("Mevcut şifre", "Yanlis1234");
        await Doldur("Yeni şifre", "Degisti123");
        await Doldur("Yeni şifre (tekrar)", "Degisti123");
        await Js("[...document.querySelectorAll('.mud-dialog button')].find(b => b.textContent.includes('Şifreyi değiştir')).click()");
        Kontrol(await Bekle(Govde("Mevcut şifreniz hatalı."), 10), "Yanlis mevcut sifre uyarisi");
        await Doldur("Mevcut şifre", "Unuttum123");
        await Ekran("4-degistir");
        await Js("[...document.querySelectorAll('.mud-dialog button')].find(b => b.textContent.includes('Şifreyi değiştir')).click()");
        Kontrol(await Bekle($"!document.querySelector('.mud-dialog') && {Govde("Şifreniz değiştirildi. Diğer cihazlardaki oturumlar kapatıldı.")}", 10), "Sifre degisti bildirimi");
        await Komut("Page.reload");
        Kontrol(await Bekle(Govde("Hoş geldiniz, Selin"), 15), "Bu cihazda oturum suruyor (yenileme sonrasi)");
        await GirisYap(eposta, "Degisti123");
        Kontrol(await Bekle(Govde("Hoş geldiniz, Selin"), 15), "Degisen sifreyle giris");

        // --- Yazı tipi kendi sunucumuzdan
        Kontrol(await Js("document.fonts.check('600 16px Inter')") == "true", "Inter yazi tipi yuklu");
        Kontrol(await Js("performance.getEntriesByType('resource').some(r => r.name.includes('/fonts/inter-latin'))") == "true"
            && await Js($"performance.getEntriesByType('resource').some(r => ![location.origin, {S(new Uri(Api).GetLeftPart(UriPartial.Authority))}, 'data:'].some(k => r.name.startsWith(k)))") == "false",
            "Yazi tipi yerelden, dis kaynaga istek yok");
    }
}
