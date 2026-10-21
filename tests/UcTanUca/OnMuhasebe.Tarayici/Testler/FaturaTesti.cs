using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OnMuhasebe.Tarayici.Testler;

// Fatura ekranlarını dener.
internal sealed class FaturaTesti : TarayiciTesti
{
    public override string Ad => "fatura";

    protected override async Task CalistirAsync()
    {
        string GenelToplam = "document.querySelector('.genel-toplam b')?.innerText ?? ''";

        await Komut("Page.enable");
        var eposta = $"faturaui{DateTime.UtcNow.Ticks}@ornek.com";
        await KayitOlAsync(new { firmaUnvani = "Fatura Arayuz Ltd", ad = "Ece", soyad = "Ak", eposta, sifre = "Sifre1234" });
        var token = (await (await http.PostAsJsonAsync(Api + "api/yonetim/giris", new { eposta, sifre = "Sifre1234" })).Content.ReadFromJsonAsync<JsonObject>())!["accessToken"]!.GetValue<string>();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var cari = await (await http.PostAsJsonAsync(Api + "api/cari/hesaplar",
            new { unvan = "Deniz Market Ltd", tur = "Musteri", kisiTipi = "Tuzel", paraBirimi = "TRY", vadeGun = 30, adres = "Liman Cad. 7", il = "Izmir" })).Content.ReadFromJsonAsync<JsonObject>();
        var cariId = cari!["id"]!.GetValue<string>();
        var kart = await (await http.PostAsJsonAsync(Api + "api/stok/kartlar",
            new { ad = "Zeytinyagi 1 L", tur = "Urun", temelBirim = "ADET", kdvOrani = 10, alisFiyati = 150, satisFiyati = 200, perakendeFiyati = 247.5, paraBirimi = "TRY",
                  ekBirimler = new[] { new { birimKodu = "KOLI", katsayi = 12 } } })).Content.ReadFromJsonAsync<JsonObject>();
        var kartId = kart!["id"]!.GetValue<string>();
        (await http.PostAsJsonAsync(Api + $"api/stok/kartlar/{kartId}/hareketler",
            new { tur = "Acilis", tarih = "2026-01-01", birim = "ADET", miktar = 100, birimFiyat = 150 })).EnsureSuccessStatusCode();

        await Git("/giris");
        await Bekle("document.querySelectorAll('input').length >= 2");
        // Varsayılan kaynak zamanlama tamponu (250) modül paketleri yüklenince dolabilir.
        await Js("performance.setResourceTimingBufferSize(5000)");
        await Doldur("E-posta", eposta);
        await Doldur("Şifre", "Sifre1234");
        await Tikla("Giriş Yap");
        Kontrol(await Bekle(Govde("Fatura özeti")), "Ana sayfada Fatura ozet paneli var");
        Kontrol(await Bekle(Govde("Henüz onaylı fatura yok")), "Bos grafik icin bos durum mesaji");

        await Js("document.querySelector('.mud-drawer a[href=\"fatura\"]').click()");
        Kontrol(await Bekle(Govde("Fatura bulunamadı")), "Bos fatura listesi");
        Kontrol(await Js("performance.getEntriesByType('resource').some(r => /OnMuhasebe\\.Web\\.Fatura\\.[a-z0-9]+\\.wasm/.test(r.name))") == "true", "Fatura ekran paketi lazy yuklendi");

        await Js("[...document.querySelectorAll('button')].find(b => b.textContent.trim().startsWith('Yeni Fatura')).click()");
        Kontrol(await Bekle(Govde("Tedarikçiden alış")), "Yeni fatura menusu turleri aciklamalariyla gosterdi");
        await Js("[...document.querySelectorAll('.mud-menu-item')].find(e => e.textContent.includes('Satış Faturası')).click()");
        Kontrol(await Bekle(Govde("Yeni Satış Faturası")), "Yeni satis faturasi formu acildi");
        await Bekle("!!" + Alan("Cari"));

        Kontrol(await Otomatik(Alan("Cari"), "Deniz", "Deniz Market"), "Cari otomatik tamamlamayla bulundu");
        Kontrol(await Bekle("document.querySelectorAll('.mud-picker input')[1]?.value?.length > 0"), "Carinin 30 gunluk vadesi otomatik geldi");

        Kontrol(await Otomatik("document.querySelector('.stok-arama input')", "Zeytin", "Zeytinyagi"), "Stok karti aramayla bulundu");
        Kontrol(await Bekle("document.querySelectorAll('.fatura-satiri').length === 1"), "Secilen urun satir olarak eklendi");
        Kontrol(await Bekle($"{GenelToplam}.includes('220,00')"), "Toptan fiyat 200 + %10 KDV = 220 canli hesaplandi");

        await SatirDoldur(0, "Miktar", "10");
        await SatirDoldur(0, "İsk. %", "10");
        Kontrol(await Bekle($"{GenelToplam}.includes('1.980,00')"), "10 adet, %10 iskonto: 1800 + 180 KDV = 1.980 (canli)");

        await Tikla("Serbest satır");
        await Bekle("document.querySelectorAll('.fatura-satiri').length === 2");
        await SatirDoldur(1, "Ürün / hizmet", "Nakliye bedeli");
        await SatirDoldur(1, "Fiyat", "100");
        Kontrol(await Bekle($"{GenelToplam}.includes('2.100,00')"), "Serbest satir eklendi (KDV %20): toplam 2.100");
        Kontrol(await Bekle(Govde("KDV %10") + " && " + Govde("KDV %20")), "KDV oranlarina gore dagilim gosterildi");

        await Tikla("Faturayı kaydet");
        Kontrol(await Bekle($"{Govde("TPT")} && {Govde("Yalnız")}", 15), "Kaydedilen fatura detayda acildi, numara aldi");
        Kontrol(await Bekle(Govde("İki Bin Yüz Türk Lirası")), "Tutar yaziyla: Iki Bin Yuz Turk Lirasi");
        var faturaAdresi = await Js("location.pathname");

        await Komut("Emulation.setEmulatedMedia", new { media = "print" });
        Kontrol(await Js("getComputedStyle(document.querySelector('.fatura-kagidi')).display !== 'none' && getComputedStyle(document.querySelector('.yazdirma-gizle')).display === 'none'") == "true",
            "Yazdirma gorunumunde yalniz fatura kagidi gorunuyor");
        var pdf = await Komut("Page.printToPDF", new { printBackground = true, preferCSSPageSize = true });
        await File.WriteAllBytesAsync(Path.Combine(Ortam.Cikti, Ad, "fatura.pdf"), Convert.FromBase64String(pdf!["data"]!.GetValue<string>()));
        await Komut("Emulation.setEmulatedMedia", new { media = "" });

        await Git($"/cari/{cariId}");
        Kontrol(await Bekle(Govde("Fatura Kes")), "Cari kartinda Fatura Kes dugmesi var");
        Kontrol(await Bekle("[...document.querySelectorAll('a.belge-baglantisi')].some(a => a.textContent.includes('TPT'))"), "Cari hareketinde faturaya baglanti var");
        await Js("[...document.querySelectorAll('a.belge-baglantisi')][0].click()");
        Kontrol(await Bekle($"location.pathname === {S(faturaAdresi)}"), "Baglantiyla faturaya gidildi");

        await Git("/fatura/yeni/satis");
        await Bekle(Govde("Yeni Satış Faturası"));
        await Bekle("!!document.querySelector('.stok-arama input')");
        await Tikla("Perakende");
        Kontrol(await Bekle(Govde("Ad soyad (isteğe bağlı)")), "Perakendede nihai tuketici alanlari acildi");
        await Otomatik("document.querySelector('.stok-arama input')", "Zeytin", "Zeytinyagi");
        Kontrol(await Bekle($"{GenelToplam}.includes('247,50')"), "Perakende fiyat KDV dahil 247,50 olarak geldi, toplam aynen 247,50");
        await Tikla("Faturayı kaydet");
        Kontrol(await Bekle($"{Govde("PRK")} && {Govde("Nihai Tüketici")}", 15), "Perakende fatura PRK serisiyle nihai tuketiciye kesildi");

        await Js("document.querySelector('.baslik-islemleri .mud-menu button').click()");
        await Bekle("[...document.querySelectorAll('.mud-menu-item')].some(e => e.textContent.includes('İptal et'))", 5);
        await Js("[...document.querySelectorAll('.mud-menu-item')].find(e => e.textContent.includes('İptal et')).click()");
        await Bekle("document.querySelector('.mud-dialog')", 5);
        await Js("[...document.querySelectorAll('.mud-dialog button')].find(b => b.textContent.trim() === 'İptal et').click()");
        Kontrol(await Bekle($"{Govde("Fatura iptal edildi")} && {Govde("tarihinde iptal edildi")}"), "Fatura detaydan iptal edildi");

        await Git("/stok/" + kartId);
        Kontrol(await Bekle("[...document.querySelectorAll('a.belge-baglantisi')].some(a => a.textContent.includes('PRK'))"), "Stok hareketlerinde faturaya baglanti var");
        Kontrol(await Bekle(Govde("90 Adet")), "Stok miktari 100 - 10 - 1 + 1 (iptal) = 90");

        await Git("/ayarlar");
        Kontrol(await Bekle($"{Govde("Firma bilgileri")} && {Govde("Kayıt akışı")} && {Govde("Numara serileri")}"), "Ayarlarda firma ve fatura panelleri var");
        await Doldur("Adres", "Kordon Boyu 12");
        await Tikla("Firma bilgilerini kaydet");
        Kontrol(await Bekle(Govde("Firma bilgileri kaydedildi")), "Firma adresi kaydedildi");
        await Js("[...document.querySelectorAll('.ayar-secenek')].find(e => e.innerText.startsWith('Taslak ve onay')).querySelector('input').click()");
        await Tikla("Fatura ayarlarını kaydet");
        Kontrol(await Bekle(Govde("Fatura ayarları kaydedildi")), "Taslak-onay akisina gecildi");

        await Git("/fatura/yeni/satis");
        await Bekle(Govde("Yeni Satış Faturası"));
        await Bekle("!!document.querySelector('.stok-arama input')");
        Kontrol(await Bekle($"{Govde("Kaydet ve onayla")} && {Govde("Taslak olarak kaydet")}"), "Taslak akisinda iki kayit dugmesi var");
        await Otomatik(Alan("Cari"), "Deniz", "Deniz Market");
        await Otomatik("document.querySelector('.stok-arama input')", "Zeytin", "Zeytinyagi");
        await Bekle("document.querySelectorAll('.fatura-satiri').length === 1");
        await Tikla("Taslak olarak kaydet");
        Kontrol(await Bekle($"{Govde("Taslak fatura cari ve stoğa işlenmedi")}", 15), "Taslak kaydedildi, uyari gosterildi");
        await Tikla("Onayla");
        await Bekle("document.querySelector('.mud-dialog')", 5);
        await Js("[...document.querySelectorAll('.mud-dialog button')].find(b => b.textContent.trim() === 'Onayla').click()");
        Kontrol(await Bekle($"{Govde("Fatura onaylandı")} && {Govde("TPT2026000000002")}"), "Taslak detaydan onaylandi ve sira numarasini aldi");

        await Git("/");
        Kontrol(await Bekle("document.querySelectorAll('.sutun').length >= 2"), "Ana sayfada aylik satis/alis sutun grafigi cizildi");
        Kontrol(await Bekle(Govde("Bu ay satış")), "Ana sayfada bu ay satis karti var");
    }
}
