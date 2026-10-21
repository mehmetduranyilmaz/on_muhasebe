using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OnMuhasebe.Tarayici.Testler;

// Cari ekranlarını dener.
internal sealed class StokTesti : TarayiciTesti
{
    public override string Ad => "stok";

    protected override async Task CalistirAsync()
    {
        await Komut("Page.enable");
        var eposta = $"stokui{DateTime.UtcNow.Ticks}@ornek.com";
        await KayitOlAsync(new { firmaUnvani = "Stok Arayuz Ltd", ad = "Ece", soyad = "Ak", eposta, sifre = "Sifre1234" });

        await Git("/giris");
        await Bekle("document.querySelectorAll('input').length >= 2");
        await Doldur("E-posta", eposta);
        await Doldur("Şifre", "Sifre1234");
        await Tikla("Giriş Yap");
        Kontrol(await Bekle(Govde("Stok özeti")), "Giris: ana sayfada Stok ozet paneli var");

        await Js("document.querySelector('.mud-drawer a[href=\"stok\"]').click()");
        Kontrol(await Bekle($"{Govde("Stok kartı bulunamadı")}"), "Bos stok listesi ve bos durum mesaji");

        await Tikla("Yeni Kart");
        await Bekle(Govde("Ek birim ekle"));
        await Doldur("Ad", "Maden Suyu 200 ml");
        await Tikla("Ek birim ekle");
        await Bekle("document.querySelectorAll('.ek-birim-satiri').length === 1");
        Kontrol(await Js("document.querySelector('.ek-birim-satiri').innerText.includes('Koli')") == "true", "Ek birim varsayilani Koli");
        await Js("""
            (() => { const i = document.querySelector('.ek-birim-katsayi input');
              Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(i, '24');
              i.dispatchEvent(new Event('input', { bubbles: true })); i.dispatchEvent(new Event('change', { bubbles: true })); i.dispatchEvent(new Event('blur', { bubbles: true })); })()
            """);
        await Task.Delay(300);
        await Tikla("Kaydet");
        Kontrol(await Bekle($"{Govde("Maden Suyu 200 ml")} && {Govde("Depolara göre")} && {Govde("24 Adet")}", 15), "Kart kaydedildi: 1 Koli = 24 Adet");

        await Tikla("Hareket Ekle");
        await Bekle($"document.querySelector('.mud-dialog') && {Govde("Hareket türü")}");
        await Sec("Birim", "Koli");
        await Doldur("Miktar", "2");
        await Doldur("Birim maliyet", "120");
        await Js("[...document.querySelectorAll('.mud-dialog button')].find(b => b.textContent.trim() === 'Kaydet').click()");
        Kontrol(await Bekle($"!document.querySelector('.mud-dialog') && {Govde("48 Adet")}"), "Acilis 2 koli -> toplam 48 adet");
        Kontrol(await Bekle(Govde("240,00 TL")), "Stok degeri 2 x 120 = 240 TL");

        await Tikla("Hareket Ekle");
        await Bekle($"document.querySelector('.mud-dialog') && {Govde("Hareket türü")}");
        await Sec("Hareket türü", "Fire");
        await Doldur("Miktar", "50");
        await Js("[...document.querySelectorAll('.mud-dialog button')].find(b => b.textContent.trim() === 'Kaydet').click()");
        Kontrol(await Bekle(Govde("eksiye düştü")), "Varsayilan ayarda eksi stok uyarisi gosterildi");
        Kontrol(await Bekle(Govde("-2 Adet")), "Miktar -2 adet");

        await Js("document.querySelector('.mud-drawer a[href=\"ayarlar\"]').click()");
        await Bekle(Govde("Stok eksiye düşerse"));
        await Js("[...document.querySelectorAll('.ayar-secenek')].find(e => e.innerText.startsWith('Engelle')).querySelector('input').click()");
        await Bekle(Govde("Stok ayarlarını kaydet"));
        await Tikla("Stok ayarlarını kaydet");
        Kontrol(await Bekle(Govde("Stok ayarları kaydedildi")), "Ayarlar ekraninda Engelle secildi ve kaydedildi");

        await Js("history.back()");
        Kontrol(await Bekle(Govde("Maden Suyu 200 ml")), "Stok kartina geri donuldu");
        await Tikla("Hareket Ekle");
        await Bekle($"document.querySelector('.mud-dialog') && {Govde("Hareket türü")}");
        await Sec("Hareket türü", "Fire");
        var secilenTur = await Js("[...document.querySelectorAll('.mud-dialog .mud-input-control')].find(c => c.querySelector('label')?.textContent.startsWith('Hareket türü'))?.querySelector('input')?.value");
        await Doldur("Miktar", "1");
        await Js("[...document.querySelectorAll('.mud-dialog button')].find(b => b.textContent.trim() === 'Kaydet').click()");
        Kontrol(await Bekle($"document.querySelector('.mud-dialog') && {Govde("Depoda yeterli stok yok")}"), "Engelle ayarinda cikis diyalogda reddedildi");
        var ekranGoruntusu = await Komut("Page.captureScreenshot", new { format = "png" });
        await File.WriteAllBytesAsync(Path.Combine(Path.GetTempPath(), "om-engelle.png"), Convert.FromBase64String(ekranGoruntusu!["data"]!.GetValue<string>()));
        Console.WriteLine("  [tani] diyalog: " + await Js("document.querySelector('.mud-dialog')?.innerText.replaceAll('\n', ' | ') ?? '(diyalog yok)'"));
        await Task.Delay(1500);
        Console.WriteLine("  [tani] secili tur once: " + secilenTur);
        Console.WriteLine("  [tani] ilk satir: " + await Js("document.querySelector('.veri-tablosu tbody tr')?.innerText.replaceAll('\n', ' | ')"));
        var tk = (await (await http.PostAsJsonAsync(Api + "api/yonetim/giris", new { eposta, sifre = "Sifre1234" })).Content.ReadFromJsonAsync<JsonObject>())!["accessToken"]!.GetValue<string>();
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tk);
        Console.WriteLine("  [tani] api ayar: " + await http.GetStringAsync(Api + "api/stok/ayarlar"));
        var kartlar = await http.GetFromJsonAsync<JsonObject>(Api + "api/stok/kartlar");
        var kid = kartlar!["kayitlar"]![0]!["id"]!.GetValue<string>();
        var hareketJson = await http.GetStringAsync(Api + "api/stok/kartlar/" + kid + "/hareketler");
        Console.WriteLine("  [tani] api hareketler: " + hareketJson[..Math.Min(700, hareketJson.Length)]);
        Console.WriteLine("  [tani] adres: " + await Js("location.pathname"));
        await Js("[...document.querySelectorAll('.mud-dialog button')].find(b => b.textContent.trim() === 'Vazgeç').click()");

        await Git("/stok/durum");
        Kontrol(await Bekle($"{Govde("Maden Suyu 200 ml")} && {Govde("Eksi")} && {Govde("1 ürün")}"), "Stok durumunda eksi urun isaretli");
        Kontrol(await Js("performance.getEntriesByType('resource').some(r => /OnMuhasebe\\.Web\\.Stok\\.[a-z0-9]+\\.wasm/.test(r.name))") == "true", "Stok ekran paketi lazy yuklendi");
    }
}
