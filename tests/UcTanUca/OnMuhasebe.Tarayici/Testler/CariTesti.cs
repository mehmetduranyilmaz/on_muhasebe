using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OnMuhasebe.Tarayici.Testler;

// Cari ekranlarını dener.
internal sealed class CariTesti : TarayiciTesti
{
    public override string Ad => "cari";

    protected override async Task CalistirAsync()
    {
        string CariDerlemesiIndi = "performance.getEntriesByType('resource').some(r => /OnMuhasebe.Web.Cari.[a-z0-9]+.wasm/.test(r.name))";

        await Komut("Page.enable");

        // Kayıt API'den: UI kaydı ilk testte denendi. Oturum çerezini tarayıcıya giriş ekranından alıyoruz.
        var eposta = $"cariui{DateTime.UtcNow.Ticks}@ornek.com";
        var kayit = await http.PostAsJsonAsync(Api + "api/yonetim/kayit",
            new { firmaUnvani = "Arayuz Test Ltd", ad = "Cem", soyad = "Yildiz", eposta, sifre = "Sifre1234" });
        kayit.EnsureSuccessStatusCode();

        await Git("/giris");
        await Bekle("document.querySelectorAll('input').length >= 2");
        await Doldur("E-posta", eposta);
        await Doldur("Şifre", "Sifre1234");
        await Tikla("Giriş");
        Kontrol(await Bekle(Govde("Hoş geldiniz, Cem")), "Giris yapildi");
        Console.WriteLine("  [tani] eslesen kaynaklar: " + await Js("performance.getEntriesByType('resource').filter(r => r.name.includes('OnMuhasebe.Web.Cari')).map(r => r.name + ' (' + r.initiatorType + ')').join(' | ')"));
        Kontrol(await Bekle(CariDerlemesiIndi), "Lisansli Cari paketi ana sayfa ozet paneli icin indirildi");

        await Js("document.querySelector('.mud-drawer a[href=\"cari\"]').click()");
        Kontrol(await Bekle($"{Govde("Cari Hesaplar")} && {Govde("Kayıtlı cari bulunamadı")}"), "Menuden Cari Hesaplar acildi");
        Kontrol(await Js(CariDerlemesiIndi) == "true", "Cari ekran kodu module girince indirildi");

        await Tikla("Yeni Cari");
        Kontrol(await Bekle(Govde("Ticari koşullar")), "Yeni cari formu acildi");
        await Doldur("Ünvan", "Euro Tedarik GmbH");
        await Doldur("Vade (gün)", "30");
        await Sec("Tür", "Tedarikçi");
        await Sec("Para birimi", "EUR");
        await Tikla("Kaydet");
        Kontrol(await Bekle($"{Govde("Euro Tedarik GmbH")} && {Govde("Euro")} && {Govde("Hareketler")}", 15), "Kaydedildi, cari karti acildi (EUR)");
        Kontrol(await Bekle(Govde("C00001")), "Otomatik kod C00001 verildi");

        await Tikla("Hareket Ekle");
        Kontrol(await Bekle($"document.querySelector('.mud-dialog') && {Govde("döviz alış kuru")}"), "Hareket diyalogunda TCMB kuru onerildi");
        var kur = await Js($"{Alan("Kur (TL)")}.value");
        Kontrol(kur.Length > 0 && kur != "0", $"Kur alani dolu: {kur}");
        await Sec("Yön", "Alacak");
        await Doldur("Tutar (EUR)", "250");
        Kontrol(await Bekle(Govde("TL karşılığı")), "TL karsiligi on izlemesi gorunuyor");
        await Doldur("Açıklama", "Tarayici testi");
        await Js("[...document.querySelectorAll('.mud-dialog button')].find(b => b.textContent.trim() === 'Kaydet').click()");
        Kontrol(await Bekle($"!document.querySelector('.mud-dialog') && {Govde("Tarayici testi")}"), "Hareket kaydedildi ve listede");
        Kontrol(await Bekle(Govde("250,00 EUR (A)")), "Bakiye 250,00 EUR (A) gosteriliyor");

        await Tikla("Ekstre");
        Kontrol(await Bekle($"{Govde("Devreden bakiye")} && {Govde("Dönem toplamı")} && {Govde("Tarayici testi")}"), "Ekstre acildi");
        Kontrol(await Bekle("document.body.textContent.includes('Bakiye (TL)')"), "Dovizli ekstrede TL kolonu var");

        await Git("/cari/bakiye-listesi");
        Kontrol(await Bekle($"{Govde("Para birimine göre toplamlar")} && {Govde("Euro Tedarik GmbH")} && {Govde("Kur farkı")}"),
            "Bakiye listesi (dogrudan adresle, sayfa yenilenerek) acildi");

        // Lisans süresini doldur: kayıt düğmeleri gizlenmeli.
        var sys = await (await http.PostAsJsonAsync(Api + "api/yonetim/giris", new { eposta = SistemEposta, sifre = SistemSifre }))
            .Content.ReadFromJsonAsync<JsonObject>();
        var kul = await (await http.PostAsJsonAsync(Api + "api/yonetim/giris", new { eposta, sifre = "Sifre1234" })).Content.ReadFromJsonAsync<JsonObject>();
        using var benIstegi = new HttpRequestMessage(HttpMethod.Get, Api + "api/yonetim/ben");
        benIstegi.Headers.Authorization = new AuthenticationHeaderValue("Bearer", kul!["accessToken"]!.GetValue<string>());
        var ben = await (await http.SendAsync(benIstegi)).Content.ReadFromJsonAsync<JsonObject>();
        var firmaId = ben!["aktifFirma"]!["id"]!.GetValue<string>();
        using var lisansIstegi = new HttpRequestMessage(HttpMethod.Put, Api + $"api/yonetim/sistem/firmalar/{firmaId}/lisans")
        {
            Content = JsonContent.Create(new { paketKodu = "basic", bitisTarihi = "2020-01-01", ekModuller = Array.Empty<string>() })
        };
        lisansIstegi.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sys!["accessToken"]!.GetValue<string>());
        (await http.SendAsync(lisansIstegi)).EnsureSuccessStatusCode();

        await Git("/cari");
        Kontrol(await Bekle($"{Govde("Lisansınızın süresi doldu")} && {Govde("Euro Tedarik GmbH")}"), "Suresi dolan lisansta liste okunuyor ve uyari bandi var");
        Kontrol(await Js(Govde("Yeni Cari")) == "false", "Suresi dolan lisansta Yeni Cari dugmesi gizli");
    }
}
