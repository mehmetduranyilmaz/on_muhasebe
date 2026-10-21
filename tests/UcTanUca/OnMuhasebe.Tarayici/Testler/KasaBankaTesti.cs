using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OnMuhasebe.Tarayici.Testler;

// Kasa/Banka ekranlarını dener.
internal sealed class KasaBankaTesti : TarayiciTesti
{
    public override string Ad => "kasabanka";

    protected override async Task CalistirAsync()
    {
        await Komut("Page.enable");
        var eposta = $"kasaui{DateTime.UtcNow.Ticks}@ornek.com";
        (await http.PostAsJsonAsync(Api + "api/yonetim/kayit",
            new { firmaUnvani = "Kasa Arayuz Ltd", ad = "Ece", soyad = "Ak", eposta, sifre = "Sifre1234" })).EnsureSuccessStatusCode();
        var token = (await (await http.PostAsJsonAsync(Api + "api/yonetim/giris", new { eposta, sifre = "Sifre1234" })).Content.ReadFromJsonAsync<JsonObject>())!["accessToken"]!.GetValue<string>();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var cariId = (await (await http.PostAsJsonAsync(Api + "api/cari/hesaplar",
            new { unvan = "Liman Market", tur = "Musteri", kisiTipi = "Tuzel", paraBirimi = "TRY" })).Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<string>();
        var faturaId = (await (await http.PostAsJsonAsync(Api + "api/fatura/faturalar", new
        {
            tur = "Satis", cariHesapId = cariId, tarih = "2026-10-10", genelIskontoTuru = "Yuzde", genelIskonto = 0,
            satirlar = new[] { new { ad = "Hizmet", birim = "ADET", miktar = 1, birimFiyat = 1000, kdvOrani = 20, kdvDahilMi = false, iskonto1 = 0, iskonto2 = 0, iskonto3 = 0 } }
        })).Content.ReadFromJsonAsync<JsonObject>())!["fatura"]!["id"]!.GetValue<string>();

        await Git("/giris");
        await Bekle("document.querySelectorAll('input').length >= 2");
        // Varsayılan kaynak zamanlama tamponu (250) modül paketleri yüklenince dolabilir.
        await Js("performance.setResourceTimingBufferSize(5000)");
        await Doldur("E-posta", eposta);
        await Doldur("Şifre", "Sifre1234");
        await Tikla("Giriş Yap");
        Kontrol(await Bekle(Govde("Kasa / Banka özeti")), "Ana sayfada Kasa/Banka ozet paneli var");

        await Js("document.querySelector('.mud-drawer a[href=\"kasabanka\"]').click()");
        Kontrol(await Bekle(Govde("Henüz hesap yok")), "Bos hesap listesi");
        Kontrol(await Js("performance.getEntriesByType('resource').some(r => /OnMuhasebe\\.Web\\.KasaBanka\\.[a-z0-9]+\\.wasm/.test(r.name))") == "true", "Kasa/Banka ekran paketi lazy yuklendi");

        await Tikla("İlk hesabınızı ekleyin");
        await Bekle("!!" + Alan("Hesap adı"));
        await Doldur("Hesap adı", "Merkez Kasa");
        await Kaydet();
        Kontrol(await Bekle($"{Govde("KS00001")} && {Govde("Henüz hareket yok")}", 15), "Kasa hesabi acildi, hesap sayfasina gidildi");
        var kasaAdresi = await Js("location.pathname");
        var kasaId = kasaAdresi.Split('/').Last();

        await Git($"/kasabanka/islem/acilis?hesap={kasaId}&donus=kasabanka/{kasaId}");
        await Bekle("!!" + Alan("Tutar"));
        await Doldur("Tutar", "5000");
        await Task.Delay(400);
        await Kaydet();
        Kontrol(await Bekle($"location.pathname === {S(kasaAdresi)} && {Govde("5.000,00 TL")}", 15), "Acilis bakiyesi girildi, hesap kartinda 5.000 TL");

        await Git("/kasabanka/yeni");
        await Bekle("!!" + Alan("Hesap adı"));
        await Js("[...document.querySelectorAll('.tur-sekmesi')].find(b => b.textContent.includes('Banka')).click()");
        await Bekle("!!" + Alan("IBAN"));
        await Doldur("Hesap adı", "Ziraat TL");
        await Doldur("IBAN", "TR33 0006 1005 1978 6457 8413 26");
        await Kaydet();
        Kontrol(await Bekle($"{Govde("BN00001")} && {Govde("TR33 0006 1005 1978 6457 8413 26")}", 15), "Banka hesabi IBAN ile acildi");

        await Git($"/cari/{cariId}");
        await Bekle(Govde("Liman Market"));
        Kontrol(await Bekle(Govde("Tahsilat")), "Cari kartinda Tahsilat dugmesi var");
        await Js("[...document.querySelectorAll('a')].find(a => a.textContent.trim() === 'Tahsilat')?.click()");
        Kontrol(await Bekle($"{Govde("Cariden para alındı")} && {Alan("Cari")}?.value?.includes('Liman Market')", 15), "Tahsilat formu cari secili acildi");
        await Bekle($"{Alan("Hesap")}?.value?.length > 0", 5);
        await Doldur("Tutar", "500");
        await Task.Delay(400);
        await Kaydet();
        Kontrol(await Bekle($"location.pathname === '/cari/{cariId}' && {Govde("700,00 TL (B)")}", 15), "Tahsilat kaydedildi, cari kartina donuldu, bakiye 700");
        await Js("[...document.querySelectorAll('.tur-sekmesi')].find(b => b.textContent.includes('Açık kalemler')).click()");
        Kontrol(await Bekle($"{Govde("Ödenmemiş borç kalemleri")} && {Govde("700,00")}"), "Acik kalemlerde faturanin kalani 700");
        await Bekle("!!document.querySelector('.veri-tablosu button[aria-label=\"Kapamalar\"]')", 10);
        await Js("document.querySelector('.veri-tablosu button[aria-label=\"Kapamalar\"]')?.click()");
        Kontrol(await Bekle($"document.querySelector('.mud-dialog') && {Govde("Kapatılacak")}"), "Kapama diyalogu acildi");
        await Js("[...document.querySelectorAll('.mud-dialog button')].find(b => b.textContent.trim() === 'Kapat').click()");

        await Git($"/fatura/{faturaId}");
        Kontrol(await Bekle(Govde("Kısmi · kalan 700,00 TL")), "Fatura detayinda kismi odeme rozeti");
        await Js("[...document.querySelectorAll('a')].find(a => a.textContent.trim() === 'Tahsilat yap')?.click()");
        Kontrol(await Bekle($"{Govde("önce faturanın açık tutarını kapatır")} && {Alan("Tutar")}?.value?.startsWith('700')", 15), "Faturadan tahsilat formu kalan tutarla acildi");
        await Task.Delay(400);
        await Kaydet();
        Kontrol(await Bekle($"location.pathname === '/fatura/{faturaId}' && {Govde("Ödendi")}", 15), "Faturadan tahsilat sonrasi fatura Odendi");

        await Git("/kasabanka/islem/virman");
        await Bekle("!!" + Alan("Kaynak hesap"));
        await SecAlan("Kaynak hesap", "Merkez Kasa");
        await SecAlan("Hedef hesap", "Ziraat TL");
        await Doldur("Tutar", "1000");
        await Task.Delay(400);
        await Kaydet();
        Kontrol(await Bekle($"location.pathname.startsWith('/kasabanka') && {Govde("Virman kaydedildi")}", 15), "Kasadan bankaya virman");

        await Git("/kasabanka/islem/gider");
        await Bekle("!!" + Alan("Kategori"), 15);
        await SecAlan("Kategori", "Kira");
        await Doldur("Tutar", "300");
        await Task.Delay(400);
        await Kaydet();
        Kontrol(await Bekle(Govde("Gider kaydedildi"), 15), "Kira gideri kaydedildi");

        await Git("/kasabanka");
        Kontrol(await Bekle($"{Govde("Merkez Kasa")} && {Govde("4.900,00 TL")} && document.querySelectorAll('.hesap-karti').length === 2"),
            "Hesap kartlari: kasa 5.000 + 500 + 700 - 1.000 - 300 = 4.900");

        await Git("/kasabanka/gelir-gider");
        Kontrol(await Bekle($"{Govde("Kira (1)")} && {Govde("Toplam gider")}"), "Gelir/gider raporunda kira kategorisi");

        await Git("/cari/vade-analizi");
        Kontrol(await Bekle(Govde("Vade Analizi")), "Vade analizi sayfasi acildi");

        await Git("/fatura/yeni/satis");
        await Bekle("!!" + Alan("Cari"), 15);
        await Js($$"""
            (() => { const i = {{Alan("Cari")}}; i.focus();
              Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(i, 'Liman');
              i.dispatchEvent(new Event('input', { bubbles: true })); })()
            """);
        await Bekle("[...document.querySelectorAll('.mud-popover-open .mud-list-item')].some(e => e.textContent.includes('Liman'))", 8);
        await Js("[...document.querySelectorAll('.mud-popover-open .mud-list-item')].find(e => e.textContent.includes('Liman')).click()");
        await Task.Delay(600);
        await Tikla("Serbest satır");
        await Bekle("document.querySelectorAll('.fatura-satiri').length === 1");
        await Js("""
            (() => { const kart = document.querySelector('.fatura-satiri');
              for (const [etiket, deger] of [['Ürün / hizmet', 'Danismanlik'], ['Fiyat', '250']]) {
                const c = [...kart.querySelectorAll('.mud-input-control')].find(x => x.querySelector('label')?.textContent.trim().startsWith(etiket));
                const i = c.querySelector('input');
                Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(i, deger);
                i.dispatchEvent(new Event('input', { bubbles: true })); i.dispatchEvent(new Event('change', { bubbles: true })); i.dispatchEvent(new Event('blur', { bubbles: true }));
              } })()
            """);
        await Task.Delay(500);
        await SecAlan("Peşin tahsil et", "Merkez Kasa");
        Kontrol(await Bekle(Govde("seçilen hesaba tahsilat yazılır")), "Faturada pesin tahsil secenegi secildi");
        await Tikla("Faturayı kaydet");
        Kontrol(await Bekle($"{Govde("TPT")} && {Govde("Ödendi")} && {Govde("Peşin tahsil edildi")}", 15), "Pesin fatura kaydedildi ve odendi gorundu");

        await Git("/ayarlar");
        Kontrol(await Bekle($"{Govde("Gelir / gider kategorileri")} && {Govde("Faiz geliri")}"), "Ayarlarda gelir/gider kategorileri paneli");
    }
}
