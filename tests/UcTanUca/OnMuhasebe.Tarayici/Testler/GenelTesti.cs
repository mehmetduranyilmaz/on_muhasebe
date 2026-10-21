using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OnMuhasebe.Tarayici.Testler;

// Web uygulamasını gerçek tarayıcıda dener.
internal sealed class GenelTesti : TarayiciTesti
{
    public override string Ad => "genel";

    protected override async Task CalistirAsync()
    {
        // Blazor MudTextField 'change' olayını dinler.
        async Task Doldur(int sira, string deger) => await Js($$"""
            (() => { const i = document.querySelectorAll('input')[{{sira}}];
              Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(i, {{JsonSerializer.Serialize(deger)}});
              i.dispatchEvent(new Event('input', { bubbles: true })); i.dispatchEvent(new Event('change', { bubbles: true })); })()
            """);

        async Task Tikla(string metin) => await Js($$"""
            [...document.querySelectorAll('button, a, .mud-menu-item')].find(e => e.textContent.trim().startsWith({{JsonSerializer.Serialize(metin)}}))?.click()
            """);

        const string GovdeIcerir = "document.body.innerText.includes";
        await Komut("Page.enable");

        await Git("/");
        Kontrol(await Bekle("location.pathname === '/giris' && document.querySelectorAll('input').length >= 2"),
            "Oturum yokken ana sayfa girise yonlendiriyor");

        await Git("/kayit");
        await Bekle("document.querySelectorAll('input').length >= 7");
        var eposta = $"tarayici{DateTime.UtcNow.Ticks}@ornek.com";
        string[] alanlar = ["Tarayici Test Ltd", "", "Deniz", "Kara", eposta, "Sifre1234", "Sifre1234"];
        for (var i = 0; i < alanlar.Length; i++) await Doldur(i, alanlar[i]);
        await Tikla("Hesap Oluştur");
        Kontrol(await Bekle($"location.pathname === '/' && {GovdeIcerir}('Hoş geldiniz, Deniz')"), "Kayit sonrasi ana sayfa acildi");
        Kontrol(await Bekle($"{GovdeIcerir}('Cari Hesaplar') && {GovdeIcerir}('Kasa / Banka')"), "Deneme lisansi: tum modul kartlari ve menu gorunuyor");
        Kontrol(await Bekle($"{GovdeIcerir}('Tarayici Test Ltd')"), "Ust cubukta aktif firma gorunuyor");
        Kontrol(await Js("document.cookie.includes('om_yenile')") == "false", "Refresh token JavaScript'ten okunamiyor (HttpOnly)");
        Kontrol(await Js("Object.keys(localStorage).length + Object.keys(sessionStorage).length") == "0", "Tarayici depolamasina token yazilmamis");

        await Komut("Page.reload");
        Kontrol(await Bekle($"location.pathname === '/' && {GovdeIcerir}('Hoş geldiniz, Deniz')"), "Sayfa yenilenince oturum korunuyor (sessiz giris)");

        await Git("/kullanicilar");
        Kontrol(await Bekle($"{GovdeIcerir}('{eposta}') && {GovdeIcerir}('Yönetici')"), "Yonetici kullanicilar sayfasini goruyor");

        await Git("/sistem/firmalar");
        Kontrol(await Bekle($"{GovdeIcerir}('erişim yetkiniz yok')"), "Normal kullanici sistem sayfasina erisemiyor");

        await Git("/");
        await Bekle($"{GovdeIcerir}('Hoş geldiniz')");
        // Gerçek fare tıklaması: avatarın ortasına bas-bırak.
        var konum = JsonNode.Parse(await Js("JSON.stringify((r => ({ x: r.x + r.width / 2, y: r.y + r.height / 2 }))(document.querySelector('.ust-cubuk .kullanici-avatar').getBoundingClientRect()))"))!;
        var (fx, fy) = (konum["x"]!.GetValue<double>(), konum["y"]!.GetValue<double>());
        await Komut("Input.dispatchMouseEvent", new { type = "mouseMoved", x = fx, y = fy });
        await Komut("Input.dispatchMouseEvent", new { type = "mousePressed", x = fx, y = fy, button = "left", clickCount = 1 });
        await Komut("Input.dispatchMouseEvent", new { type = "mouseReleased", x = fx, y = fy, button = "left", clickCount = 1 });
        Console.WriteLine("  [tani] menu acildi mi: " + await Bekle($"{GovdeIcerir}('Çıkış')", 5));
        Console.WriteLine("  [tani] cikis adaylari: " + await Js("[...document.querySelectorAll('*')].filter(e => e.children.length === 0 && e.textContent.trim() === 'Çıkış').map(e => e.tagName + '.' + e.className + ' < ' + e.parentElement.tagName + '.' + e.parentElement.className).join(' | ')"));
        await Tikla("Çıkış");
        Kontrol(await Bekle("location.pathname === '/giris'"), "Cikis sonrasi giris ekrani");
        await Komut("Page.reload");
        await Task.Delay(3000);
        Kontrol(await Bekle("location.pathname === '/giris' && document.querySelectorAll('input').length >= 2"), "Cikistan sonra yenileme oturumu geri getirmiyor");

        await Doldur(0, eposta);
        await Doldur(1, "YanlisSifre1");
        await Tikla("Giriş");
        Kontrol(await Bekle($"{GovdeIcerir}('E-posta veya şifre hatalı')"), "Hatali sifrede Turkce hata mesaji");

        await Git("/kayit");
        await Bekle("document.querySelectorAll('input').length >= 7");
        string[] tekrar = ["Tekrar Ltd", "", "Deniz", "Kara", eposta, "Sifre1234", "Sifre1234"];
        for (var i = 0; i < tekrar.Length; i++) await Doldur(i, tekrar[i]);
        await Tikla("Hesap Oluştur");
        Kontrol(await Bekle($"{GovdeIcerir}('zaten bir hesap var')"), "Ayni e-postayla kayitta hata mesaji ekranda (IslemDugmesi yeniden cizim)");
        await Git("/giris");
        await Bekle("document.querySelectorAll('input').length >= 2");
        await Doldur(0, SistemEposta);
        await Doldur(1, SistemSifre);
        await Tikla("Giriş");
        var sistemMenusu = await Bekle("document.querySelector('.mud-drawer')?.textContent.includes('Firmalar ve Lisanslar')");
        if (!sistemMenusu) Console.WriteLine("  [tani] ekrandaki uyari: " + await Js("document.querySelector('.mud-alert')?.innerText ?? '(uyari yok) ' + location.pathname"));
        Kontrol(sistemMenusu, "Sistem yoneticisi menusu gorunuyor");
        await Git("/sistem/firmalar");
        await Bekle("document.querySelectorAll('input').length >= 1");
        await Doldur(0, "Tarayici Test");
        Kontrol(await Bekle($"{GovdeIcerir}('Tarayici Test Ltd') && {GovdeIcerir}('Deneme')"), "Sistem firmalar tablosu yeni firmayi ve deneme paketini gosteriyor");
    }
}
