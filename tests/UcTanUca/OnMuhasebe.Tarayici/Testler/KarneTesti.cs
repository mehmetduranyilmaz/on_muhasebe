using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OnMuhasebe.Tarayici.Testler;

// Çek karnesi ve senet defteri ekranlarını dener.
internal sealed class KarneTesti : TarayiciTesti
{
    public override string Ad => "karne";

    protected override async Task CalistirAsync()
    {
        await Komut("Page.enable");
        var eposta = $"karneui{DateTime.UtcNow.Ticks}@ornek.com";
        var kayit = (await (await http.PostAsJsonAsync(Api + "api/yonetim/kayit",
            new { firmaUnvani = "Karne Arayuz", ad = "Can", soyad = "Er", eposta, sifre = "Sifre1234" })).Content.ReadFromJsonAsync<JsonObject>())!;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", kayit["accessToken"]!.GetValue<string>());
        var cariId = (await Post("api/cari/hesaplar", new { unvan = "Tedarikci Ltd", tur = "Tedarikci", kisiTipi = "Tuzel", paraBirimi = "TRY" }))["id"]!.GetValue<string>();
        await Post("api/kasabanka/hesaplar", new { ad = "Is Bankasi TL", tur = "Banka", paraBirimi = "TRY" });
        await Post("api/kasabanka/cek-senet/karneler", new { tur = "Senet", baslangicSeri = "0001", yaprakSayisi = 50, tarih = "2026-10-01", aciklama = "Senet defteri" });

        await Git("/giris");
        await Bekle("document.querySelectorAll('input').length >= 2");
        await Doldur("E-posta", eposta);
        await Doldur("Şifre", "Sifre1234");
        await Tikla("Giriş Yap");
        await Bekle(Govde("Hoş"), 15);

        await Git("/cek-senet");
        Kontrol(await Bekle("[...document.querySelectorAll('a')].some(a => a.textContent.trim() === 'Karneler')", 15), "Cek/senet sayfasinda Karneler dugmesi");
        await Git("/cek-senet/karneler");
        Kontrol(await Bekle(Govde("Çek karnesi yok"), 15), "Bos durum");
        await Tikla("Yeni çek karnesi");
        await Bekle($"!!{Alan("İlk yaprağın seri no")}", 10);
        await Doldur("İlk yaprağın seri no", "ABC 0001098");
        await Doldur("Yaprak sayısı", "5");
        Kontrol(await Bekle("document.querySelector('.karne-onizleme')?.textContent.includes('ABC0001098 – ABC0001102')", 5), "Bitis serisi onizlemede");
        await Ekran("1-yeni-karne");
        await DiyalogDugmesi("Kaydet");
        Kontrol(await Bekle("document.querySelectorAll('.yaprak').length === 5", 10), "Karne kaydedildi, 5 yaprak goruldu");
        Kontrol(await Js("document.querySelector('.yaprak.siradaki .yaprak-no')?.textContent") == "ABC0001098", "Ilk yaprak siradaki");

        await Js("[...document.querySelectorAll('.yaprak')].find(y => y.textContent.includes('ABC0001099')).click()");
        await Bekle("!!document.querySelector('.mud-dialog')", 5);
        await Js("[...document.querySelectorAll('.mud-dialog .durum-filtresi')].find(b => b.textContent.trim() === 'Yırtıldı').click()");
        await Task.Delay(300);
        await DiyalogDugmesi("Yaprağı iptal et");
        Kontrol(await Bekle("[...document.querySelectorAll('.yaprak.iptal')].some(y => y.textContent.includes('ABC0001099') && y.textContent.includes('Yırtıldı'))", 10),
            "Yirtik yaprak iptal gorunuyor");
        await Ekran("2-yapraklar");

        await Git($"/cek-senet/cikis?cari={cariId}");
        Kontrol(await Bekle($"{Alan("Çek karnesi")}?.closest('.mud-input-control')?.innerText.includes('ABC0001098') && {Alan("Çek no")}?.value === 'ABC0001098'", 15),
            "Cikis bordrosunda karne kendiliginden secildi, siradaki yaprak geldi");
        await Doldur("Vade", "20.12.2026");
        await Doldur("Tutar", "1500");
        await Task.Delay(300);
        await Js("document.querySelector('.evrak-karti button[aria-label=\"Kopyala\"]').click()");
        Kontrol(await Bekle("[...document.querySelectorAll('.evrak-karti')][1]?.querySelectorAll('input')[0]?.value === 'ABC0001100'", 5),
            "Kopyalanan satira iptali atlayan sonraki yaprak (ABC0001100)");
        await Tikla("Senet ekle");
        Kontrol(await Bekle("[...document.querySelectorAll('.evrak-karti')][2]?.querySelectorAll('input')[0]?.value === '0001'", 5), "Senet satirina defterin ilk numarasi geldi");
        await Js("(() => { const k = [...document.querySelectorAll('.evrak-karti')][2]; const set = (i, v) => { Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(i, v); i.dispatchEvent(new Event('input', { bubbles: true })); i.dispatchEvent(new Event('change', { bubbles: true })); i.dispatchEvent(new Event('blur', { bubbles: true })); }; const ins = k.querySelectorAll('input'); set(ins[1], '15.01.2027'); set(ins[2], '800'); })()");
        Kontrol(await Bekle(Govde("Bu bordrodan sonra karnede 2 boş yaprak kalacak"), 5), "Karne azalma ipucu bordrodaki satirlari dusuyor");
        await Ekran("3-cikis");
        await Task.Delay(500);
        await Tikla("Bordroyu kaydet");
        Kontrol(await Bekle("location.pathname.startsWith('/cek-senet/bordro/')", 15), "Bordro kaydedildi");

        await Git("/cek-senet/karneler");
        Kontrol(await Bekle("document.querySelector('.karne-karti')?.textContent.includes('2 kullanıldı') && document.querySelector('.karne-karti')?.textContent.includes('ABC0001101')", 15),
            "Karnede 2 kullanildi, siradaki ABC0001101");
        await Js("document.querySelector('.karne-karti').click()");
        Kontrol(await Bekle("[...document.querySelectorAll('a.yaprak.kullanildi')].some(y => y.textContent.includes('Tedarikci Ltd'))", 10), "Kullanilan yaprakta cari adi");
        await Ekran("4-sonra");
        await Tikla("Senet defterleri");
        Kontrol(await Bekle("document.querySelector('.karne-karti')?.textContent.includes('1 kullanıldı') && document.querySelector('.karne-karti')?.textContent.includes('0002')", 10),
            "Senet defterinde 1 kullanildi, siradaki 0002");
    }
}
