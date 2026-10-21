using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OnMuhasebe.Tarayici.Testler;

// Değişiklik geçmişi ve dönem kilidi ekranlarını dener.
internal sealed class DenetimTesti : TarayiciTesti
{
    public override string Ad => "denetim";

    protected override async Task CalistirAsync()
    {
        await Komut("Page.enable");
        var eposta = $"denetimui{DateTime.UtcNow.Ticks}@ornek.com";
        var kayit = await KayitOlAsync(new { firmaUnvani = "Denetim Arayuz Ltd", ad = "Selin", soyad = "Ak", eposta, sifre = "Sifre1234" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", kayit["accessToken"]!.GetValue<string>());
        var cari = await Post("api/cari/hesaplar", new { unvan = "Gecmis Market", tur = "Musteri", kisiTipi = "Tuzel", paraBirimi = "TRY", telefon = "0212 100" });
        var cariId = cari["id"]!.GetValue<string>();
        var guncel = await http.PutAsJsonAsync(Api + $"api/cari/hesaplar/{cariId}", new
        {
            unvan = "Gecmis Market Ltd", tur = "Musteri", kisiTipi = "Tuzel", paraBirimi = "TRY", telefon = "0212 200", riskLimiti = 2500,
            surum = cari["surum"]!.GetValue<uint>()
        });
        Kontrol(guncel.IsSuccessStatusCode, "Cari API ile guncellendi");

        await Git("/giris");
        await Bekle("document.querySelectorAll('input').length >= 2");
        await Doldur("E-posta", eposta);
        await Doldur("Şifre", "Sifre1234");
        await Tikla("Giriş Yap");
        await Bekle(Govde("Hoş"), 15);

        await Git($"/cari/{cariId}");
        Kontrol(await Bekle("!!document.querySelector('.degisiklik-gecmisi-dugmesi')", 15), "Yoneticiye cari kartinda gecmis dugmesi gorunuyor");
        await Js("document.querySelector('.degisiklik-gecmisi-dugmesi').click()");
        Kontrol(await Bekle("document.querySelectorAll('.degisiklik-zaman-cizelgesi .degisiklik').length === 1", 10), "Gecmiste bir guncelleme var");
        var metin = await Js("document.querySelector('.degisiklik-dialog').innerText");
        Kontrol(metin.Contains("Güncellendi") && metin.Contains("Selin Ak"), "Kim ve islem yaziyor");
        Kontrol(metin.Contains("Unvan") && metin.Contains("Gecmis Market Ltd") && metin.Contains("Risk limiti") && metin.Contains("2500"),
            "Alan adlari okunur, eski/yeni degerler gosteriliyor");
        Kontrol(await Js("[...document.querySelectorAll('.degisiklik-alanlari tr')].find(t => t.querySelector('th').textContent === 'Unvan').querySelector('td.eski').textContent.trim()") == "Gecmis Market", "Eski deger ayri hucrede");
        await Ekran("1-gecmis");
        await Js("document.querySelector('.mud-dialog .mud-button-close')?.click()");
        await Task.Delay(500);

        await Git("/ayarlar");
        Kontrol(await Bekle($"{Govde("Dönem kilidi")} && !!{Alan("Kilit tarihi")}", 20), "Ayarlarda donem kilidi paneli");
        Kontrol(!await Bekle(Govde("ve öncesine ait hareketler kilitli"), 2), "Kilit yokken bilgi notu yok");
        var kilit = await http.PutAsJsonAsync(Api + "api/yonetim/donem-kilidi", new { kilitTarihi = "2026-09-30", aciklama = "Eylul KDV" });
        Kontrol(kilit.IsSuccessStatusCode, "Kilit API ile kondu");
        await Git("/ayarlar");
        Kontrol(await Bekle(Govde("30.09.2026 ve öncesine ait hareketler kilitli"), 20), "Panel kilit tarihini gosteriyor");
        Kontrol(await Js($"{Alan("Açıklama")}.value") == "Eylul KDV", "Aciklama dolu geldi");
        await Js("[...document.querySelectorAll('.mud-paper')].find(p => p.textContent.includes('Dönem kilidi'))?.scrollIntoView()");
        await Ekran("2-kilit");
        await Doldur("Açıklama", "Eylul KDV beyannamesi verildi");
        await Task.Delay(300);
        await Tikla("Dönem kilidini kaydet");
        Kontrol(await Bekle(Govde("Dönem kilidi kaydedildi."), 10), "Aciklama arayuzden kaydedildi");
        var son = await http.GetFromJsonAsync<JsonObject>(Api + "api/yonetim/donem-kilidi");
        Kontrol(son!["aciklama"]?.GetValue<string>() == "Eylul KDV beyannamesi verildi" && son["kilitTarihi"]?.GetValue<string>() == "2026-09-30",
            "Kayitli kilit arayuzden degisti, tarih korundu");
    }
}
