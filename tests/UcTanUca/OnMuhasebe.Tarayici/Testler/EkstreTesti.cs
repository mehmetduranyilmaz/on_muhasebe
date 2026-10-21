using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OnMuhasebe.Tarayici.Testler;

// Ekstre içe aktarma ekranlarını dener.
internal sealed class EkstreTesti : TarayiciTesti
{
    public override string Ad => "ekstre";

    protected override async Task CalistirAsync()
    {
        await Komut("Page.enable");
        await Komut("DOM.enable");
        var eposta = $"ekstreui{DateTime.UtcNow.Ticks}@ornek.com";
        (await http.PostAsJsonAsync(Api + "api/yonetim/kayit",
            new { firmaUnvani = "Ekstre Arayuz Ltd", ad = "Ece", soyad = "Ak", eposta, sifre = "Sifre1234" })).EnsureSuccessStatusCode();
        var token = (await Post("api/yonetim/giris", new { eposta, sifre = "Sifre1234" }))["accessToken"]!.GetValue<string>();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await Post("api/cari/hesaplar", new { unvan = "Anadolu Gida Ltd. Sti.", tur = "Musteri", kisiTipi = "Tuzel", paraBirimi = "TRY" });
        await Post("api/cari/hesaplar", new { unvan = "Beta Ticaret", tur = "Musteri", kisiTipi = "Tuzel", vergiNo = "1234567890", paraBirimi = "TRY" });
        var musteri = (await Post("api/cari/hesaplar", new { unvan = "Musteri A", tur = "Musteri", kisiTipi = "Tuzel", paraBirimi = "TRY" }))["id"]!.GetValue<string>();
        await http.GetAsync(Api + "api/kasabanka/kategoriler");
        var banka = (await Post("api/kasabanka/hesaplar", new { ad = "Is Bankasi", tur = "Banka", paraBirimi = "TRY" }))["id"]!.GetValue<string>();
        await Post("api/kasabanka/hesaplar", new { ad = "Sirket Karti", tur = "KrediKarti", paraBirimi = "TRY", hesapNo = "5400 **** 4821" });
        await Post("api/kasabanka/islemler/acilis", new { hesapId = banka, tarih = "2026-10-01", tutar = 10000 });
        await Post("api/kasabanka/islemler/tahsilat", new { hesapId = banka, cariHesapId = musteri, tarih = "2026-10-11", tutar = 2000 });

        await Git("/giris");
        await Bekle("document.querySelectorAll('input').length >= 2");
        await Js("performance.setResourceTimingBufferSize(5000)");
        await Doldur("E-posta", eposta);
        await Doldur("Şifre", "Sifre1234");
        await Tikla("Giriş Yap");
        await Bekle(Govde("Kasa / Banka özeti"));

        await Git($"/kasabanka/{banka}");
        Kontrol(await Bekle(Govde("Ekstre içe aktar")), "Banka hesap sayfasinda 'Ekstre ice aktar' dugmesi");
        await Tikla("Ekstre içe aktar");
        Kontrol(await Bekle($"{Govde("Ekstre dosyasını sürükleyin")} && {Govde("Henüz ekstre aktarılmadı")}"), "Sihirbaz: dosya adimi ve bos gecmis");
        await Ekran("1-dosya");

        await DosyaSec("banka.csv");
        Kontrol(await Bekle($"{Govde("Kolonları eşleyin")} && {Govde("TÜRKİYE İŞ BANKASI")}", 15), "CSV yuklendi, Windows-1254 Turkce karakterlerle onizleme");
        Kontrol(await Js("[...document.querySelectorAll('.kolon-rolu')].map(e => e.textContent).join(',')") == "Tarih,Açıklama,Çıkış,Giriş,Bakiye",
            "Tahmin edilen kolon rolleri onizlemede isaretli");
        Kontrol(await Js("document.querySelector('.esleme-onizleme tr.baslik-satiri')?.textContent.includes('Açıklama')") == "true", "Baslik satiri (3.) vurgulandi");
        await Ekran("2-esleme");

        await Tikla("Satırları oku");
        Kontrol(await Bekle($"{Govde("Onayla ve aktar")} && {Govde("ENERJİSA ELEKTRİK FATURASI")}", 15), "Esleme uygulandi, satir onay tablosu");
        Kontrol((await Satir("EFT GELEN ANADOLU")).Contains("Anadolu Gida Ltd. Sti."), "Unvandan cari onerisi gorunuyor");
        Kontrol((await Satir("KREDI KARTI ODEMESI")).Contains("Sirket Karti"), "Kart odemesi kendi kartina virman onerisi");
        Kontrol((await Satir("GELEN HAVALE MUSTERI")).Contains("Kayıtlı hareketle eşleşti"), "Elle girilen tahsilat eslesti");
        Kontrol((await Satir("ENERJİSA EK")).Contains("Kategori seçilmedi"), "Kategorisiz gider satirinda eksik uyarisi");
        Kontrol(await Bekle(Govde("Seçim eksik")), "'Secim eksik' filtresi gorunuyor");
        await Ekran("3-onay");

        await SatirTikla("ENERJİSA ELEKTRİK FATURASI");
        Kontrol(await Bekle("!!document.querySelector('.mud-dialog') && document.querySelector('.mud-dialog').innerText.includes('ENERJİSA')"), "Satir duzenleme diyalogu acildi");
        await SecAlan("Kategori", "Elektrik");
        await Js("[...document.querySelectorAll('.mud-dialog .mud-switch')].find(s => s.textContent.includes('Bu seçimi hatırla'))?.querySelector('input').click()");
        Kontrol(await Bekle("!!" + Alan("Açıklamada geçen kelime") + " && " + Alan("Açıklamada geçen kelime") + ".value === 'ENERJİSA'"), "Kural icin kelime onerildi: ENERJISA");
        await Ekran("4-diyalog");
        await DiyalogDugmesi("Uygula");
        Kontrol(await Bekle("!document.querySelector('.mud-dialog')"), "Diyalog kapandi");
        Kontrol(await Bekle("(() => { const t = [...document.querySelectorAll('.veri-tablosu tbody tr')].find(t => t.textContent.includes('ENERJİSA EK')); return t && t.textContent.includes('Elektrik') && t.textContent.includes('Kural'); })()"),
            "Ogrenilen kural diger ENERJISA satirina hemen uygulandi");

        await Js("[...document.querySelectorAll('.veri-tablosu tbody tr')].find(t => t.textContent.includes('HESAP İŞLETİM')).querySelector('input[type=checkbox]').click()");
        Kontrol(await Bekle(Govde("1 seçili")), "Satir secilince toplu islem cubugu");
        await Js("[...document.querySelectorAll('.toplu-cubuk button')].find(b => b.textContent.includes('Düzenle')).click()");
        await Bekle("!!document.querySelector('.mud-dialog')");
        await Js("[...document.querySelectorAll('.mud-dialog .tur-sekmesi')].find(b => b.textContent.trim() === 'Atla').click()");
        await Task.Delay(300);
        await DiyalogDugmesi("Uygula");
        Kontrol(await Bekle("!document.querySelector('.mud-dialog') && document.body.innerText.includes('Atlanacak')"), "Secili satir atlanacak olarak isaretlendi");

        await Tikla("Onayla ve aktar");
        Kontrol(await Bekle("!!document.querySelector('.mud-dialog') && document.querySelector('.mud-dialog').innerText.includes('1 satır atlanacak')"), "Aktarim onayi ozet bilgiyle");
        await DiyalogDugmesi("Aktar");
        Kontrol(await Bekle($"{Govde("Ekstre aktarıldı")} && {Govde("Kayıtlı harekete bağlandı")}", 20), "Sonuc ekrani: ekstre aktarildi");
        await Ekran("5-sonuc");
        Kontrol(await Js("[...document.querySelectorAll('.ekstre-adimi')].filter(a => a.classList.contains('tamam')).length") == "3", "Adim cubugunda ilk 3 adim tamamlandi");

        await Tikla("Hesaba dön");
        Kontrol(await Bekle($"{Govde("10.480,00")} && {Govde("ENERJİSA")}", 15), "Hesap bakiyesi 10.480 ve ekstre hareketleri listede");

        await Git($"/kasabanka/{banka}/ekstre-aktar");
        Kontrol(await Bekle($"{Govde("banka.csv")} && {Govde("Tamamlandı")} && {Govde("6 / 7")}"), "Aktarim gecmisinde tamamlanan ekstre");
        await SatirTikla("banka.csv");
        Kontrol(await Bekle($"{Govde("Bu ekstre aktarıldı")} && !document.body.innerText.includes('Onayla ve aktar')"), "Tamamlanan aktarim salt okunur aciliyor");

        await Tikla("Yeni dosya");
        await Bekle(Govde("Ekstre dosyasını sürükleyin"));
        await DosyaSec("banka.csv");
        await Bekle(Govde("satır okundu"), 15);
        Kontrol(await Bekle($"{Govde("Daha önce aktarılan")} && {Govde("Onayla ve aktar")}"), "Tekrar yukleme: kayitli sablonla esleme atlandi, satirlar daha once aktarildi");

        await Git("/ayarlar");
        Kontrol(await Bekle($"{Govde("Ekstre eşleştirme kuralları")} && {Govde("ENERJISA")} && {Govde("1 kez")}"), "Ayarlarda ogrenilen kural ve kullanim sayisi");
        await Tikla("Kural ekle");
        await Bekle("!!document.querySelector('.mud-dialog')");
        await Doldur("Açıklamada geçen kelime", "shell");
        await Js("[...document.querySelectorAll('.mud-dialog .tur-sekmesi')].find(b => b.textContent.trim() === 'Gider').click()");
        await SecAlan("Kategori", "Yakıt");
        await DiyalogDugmesi("Kaydet");
        Kontrol(await Bekle($"!document.querySelector('.mud-dialog') && {Govde("SHELL")} && {Govde("Yakıt ve ulaşım")}"), "Ayarlardan elle kural eklendi");
        await Js("document.querySelector('.kural-listesi').scrollIntoView()");
        await Ekran("6-kurallar");
    }
}
