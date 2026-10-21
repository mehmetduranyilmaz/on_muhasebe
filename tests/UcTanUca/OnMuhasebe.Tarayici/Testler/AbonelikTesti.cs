using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OnMuhasebe.Tarayici.Testler;

// Abonelik, ödeme bildirimi ve onay ekranlarını dener.
internal sealed class AbonelikTesti : TarayiciTesti
{
    public override string Ad => "abonelik";

    protected override async Task CalistirAsync()
    {
        await Komut("Page.enable");
        await Komut("Network.enable");
        var eposta = $"aboneui{DateTime.UtcNow.Ticks}@ornek.com";
        var unvan = $"Arayuz Abone {DateTime.UtcNow.Ticks % 100000}";
        (await http.PostAsJsonAsync(Api + "api/yonetim/kayit",
            new { firmaUnvani = unvan, ad = "Nil", soyad = "Ay", eposta, sifre = "Sifre1234" })).EnsureSuccessStatusCode();

        // --- Firma: abonelik ekranı ve bildirim
        await GirisYap(eposta, "Sifre1234");
        Kontrol(await Bekle("[...document.querySelectorAll('.mud-nav-link')].some(a => a.textContent.includes('Abonelik'))", 10), "Yonetici menusunde Abonelik var");
        await Git("/abonelik");
        Kontrol(await Bekle($"!!document.querySelector('.abonelik-ozeti') && {Govde("Deneme")} && {Govde("gün kaldı")}", 20), "Ozet: deneme paketi ve kalan gun halkasi");
        Kontrol(await Bekle("document.querySelectorAll('.paket-karti').length >= 3", 5), "Satistaki paket kartlari");
        Kontrol(await Js("document.querySelector('.odeme-iban code')?.textContent") == "TR33 0006 1005 1978 6457 8413 26", "IBAN dortlu gruplarla");
        var temelUcret = await Js("[...document.querySelectorAll('.paket-karti')].find(k => k.querySelector('.paket-adi').textContent === 'Temel').querySelector('.paket-ucret').innerText");
        Kontrol(temelUcret.Contains("500,00") && temelUcret.Contains("/ ay"), $"Temel aylik ucret gosteriliyor ({temelUcret.Replace('\n', ' ')})");
        await Tikla("Yıllık");
        Kontrol(await Bekle("[...document.querySelectorAll('.paket-karti')].find(k => k.querySelector('.paket-adi').textContent === 'Temel').querySelector('.paket-ucret').innerText.includes('5.000,00')", 5),
            "Yillik sekmesinde yillik ucret");
        await Tikla("Aylık");
        await Ekran("1-abonelik");
        await Js("[...document.querySelectorAll('.paket-karti')].find(k => k.querySelector('.paket-adi').textContent === 'Temel').querySelector('button').click()");
        Kontrol(await Bekle($"!!document.querySelector('.mud-dialog') && {Alan("Gönderilen tutar")}?.value === '500,00'", 10), "Bildirim diyalogu tutar dolu acildi");
        Kontrol(await Js($"{Alan("Gönderen")}.value") == unvan, "Gonderen firma unvaniyla dolu");
        await Doldur("Açıklama", "EFT 4455");
        await Ekran("2-bildirim");
        await DiyalogDugmesi("Bildir");
        Kontrol(await Bekle($"{Govde("Bildiriminiz alındı")} && {Govde("onay bekliyor")}", 10), "Bildirim gonderildi, bekliyor uyarisi");
        Kontrol(await Bekle("[...document.querySelectorAll('.paket-karti button')].every(b => b.disabled)", 5), "Bekleyen bildirim varken yeni bildirim dugmeleri kapali");

        // --- Sistem yöneticisi: onay
        await GirisYap(SistemEposta, SistemSifre);
        await Git("/sistem/odeme-bildirimleri");
        Kontrol(await Bekle($"{Govde(unvan)} && {Govde("EFT 4455")}", 20), "Bekleyen bildirim sistem ekraninda");
        Kontrol(await Bekle("!!document.querySelector('.tur-sekmesi.secili .sekme-sayisi')", 5), "Bekleyen sayisi rozette");
        await Ekran("3-sistem-bildirimler");
        await Js($"[...document.querySelectorAll('.veri-tablosu tbody tr')].find(t => t.textContent.includes({S(unvan)})).querySelectorAll('button')[1].click()");
        Kontrol(await Bekle(Govde("hesabınıza geçti mi"), 5), "Onay sorusu");
        await DiyalogDugmesi("Onayla");
        Kontrol(await Bekle(Govde("Onaylandı. Yeni son gün"), 10), "Onaylandi bildirimi");
        Kontrol(await Bekle($"!{Govde(unvan)}", 5), "Bekleyenlerden dustu");
        await Git("/sistem/paketler");
        Kontrol(await Bekle($"{Govde("Temel")} && {Govde("Profesyonel")} && {Govde("Kurumsal")} && {Govde("Ödeme hesabı")}", 15), "Paketler ve odeme hesabi sayfasi");
        Kontrol(await Bekle($"{Alan("IBAN")}?.value === 'TR33 0006 1005 1978 6457 8413 26'", 5), "Sistem IBAN alani dolu");
        await Ekran("4-sistem-paketler");
        await SatirTikla("Profesyonel");
        Kontrol(await Bekle($"!!document.querySelector('.mud-dialog') && {Alan("Kullanıcı limiti")}?.value === '5'", 5), "Paket duzenleme diyalogu dolu");
        await Ekran("5-paket-duzenle");
        await Js("document.querySelector('.mud-dialog .mud-button-close, .mud-dialog-actions button')?.click()");

        // --- Firma: yeni paket
        await GirisYap(eposta, "Sifre1234");
        await Git("/abonelik");
        Kontrol(await Bekle($"document.querySelector('.abonelik-paket')?.textContent.includes('Temel') && {Govde("Mevcut paketiniz")}", 20), "Firma Temel pakete gecti");
        Kontrol(await Bekle($"{Govde("Onaylandı")} && {Govde("EFT")} === false || {Govde("Onaylandı")}", 5), "Bildirim gecmisinde onaylandi");
        Kontrol(await Bekle("parseInt(document.querySelector('.abonelik-gun').textContent) >= 42", 5),
            $"Kalan gun denemenin ustune eklendi ({await Js("document.querySelector('.abonelik-gun').textContent.trim()")})");
        await Ekran("6-abonelik-sonra");
    }
}
