using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OnMuhasebe.Tarayici.Testler;

// Çek/senet ekranlarını dener.
internal sealed class CekSenetTesti : TarayiciTesti
{
    public override string Ad => "cek";

    protected override async Task CalistirAsync()
    {
        // Bordro formundaki n. evrak kartının alanını doldurur.
        async Task KartDoldur(int kart, string etiket, string deger) => await Js($$"""
            (() => { const k = document.querySelectorAll('.evrak-karti')[{{kart}}];
              const c = [...k.querySelectorAll('.mud-input-control')].find(x => x.querySelector('label')?.textContent.trim().startsWith({{S(etiket)}}));
              const i = c.querySelector('input');
              Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(i, {{S(deger)}});
              i.dispatchEvent(new Event('input', { bubbles: true })); i.dispatchEvent(new Event('change', { bubbles: true }));
              i.dispatchEvent(new Event('blur', { bubbles: true })); })()
            """);

        async Task KartDugmesi(int kart, string metin) => await Js(
            $"[...document.querySelectorAll('.evrak-karti')[{kart}].querySelectorAll('button')].find(b => b.textContent.trim().startsWith({S(metin)}))?.click()");

        await Komut("Page.enable");
        await Komut("DOM.enable");
        var eposta = $"cekui{DateTime.UtcNow.Ticks}@ornek.com";
        (await http.PostAsJsonAsync(Api + "api/yonetim/kayit",
            new { firmaUnvani = "Cek Arayuz Ltd", ad = "Ece", soyad = "Ak", eposta, sifre = "Sifre1234" })).EnsureSuccessStatusCode();
        var token = (await Post("api/yonetim/giris", new { eposta, sifre = "Sifre1234" }))["accessToken"]!.GetValue<string>();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var musteri = (await Post("api/cari/hesaplar", new { unvan = "Liman Market", tur = "Musteri", kisiTipi = "Tuzel", paraBirimi = "TRY" }))["id"]!.GetValue<string>();
        var diger = (await Post("api/cari/hesaplar", new { unvan = "Eski Musteri", tur = "Musteri", kisiTipi = "Tuzel", paraBirimi = "TRY" }))["id"]!.GetValue<string>();
        await Post("api/cari/hesaplar", new { unvan = "Toptanci Gida", tur = "Tedarikci", kisiTipi = "Tuzel", paraBirimi = "TRY" });
        var banka = (await Post("api/kasabanka/hesaplar", new { ad = "Ziraat TL", tur = "Banka", paraBirimi = "TRY" }))["id"]!.GetValue<string>();
        await Post("api/kasabanka/hesaplar", new { ad = "Merkez Kasa", tur = "Kasa", paraBirimi = "TRY" });
        // Geçmişte karşılıksız çıkmış keşideci: yeni girişte uyarı görülmeli.
        var eski = await Post("api/kasabanka/cek-senet/giris", new
        {
            cariHesapId = diger, tarih = "2026-09-01", riskOnaylandi = false,
            evraklar = new[] { new { tur = "Cek", seriNo = "7001", vadeTarihi = "2026-09-30", tutar = 4500, paraBirimi = "TRY",
                borclu = new { ad = "Ahmet Yilmaz", kimlikNo = "10000000146" }, bankaAdi = "Halkbank", sube = "Kadikoy", hesapNo = "9988" } }
        });
        var eskiEvrak = eski["evrakIdleri"]![0]!.GetValue<string>();
        await Post("api/kasabanka/cek-senet/islemler", new { islem = "Karsiliksiz", tarih = "2026-10-01", evrakIdleri = new[] { eskiEvrak } });

        await Git("/giris");
        await Bekle("document.querySelectorAll('input').length >= 2");
        await Js("performance.setResourceTimingBufferSize(5000)");
        await Doldur("E-posta", eposta);
        await Doldur("Şifre", "Sifre1234");
        await Tikla("Giriş Yap");
        Kontrol(await Bekle($"{Govde("Çek / senet")} && {Govde("Karşılıksız takipte")}", 20), "Ana sayfada cek/senet ozeti");

        await Js("document.querySelector('.mud-drawer a[href=\"cek-senet\"]').click()");
        Kontrol(await Bekle($"{Govde("Portföy, ciro ve tahsil takibi")} && {Govde("Açık")}", 20), "Menuden cek/senet portfoyu acildi");
        Kontrol(await Bekle(Govde("AC00001")), "Karsiliksiz evrak acik listede");
        await Ekran("1-portfoy");

        await Tikla("Evrak al");
        Kontrol(await Bekle($"{Govde("Evrak giriş bordrosu")} && document.querySelectorAll('.evrak-karti').length === 1"), "Giris bordrosu bir cek kartiyla acildi");
        Kontrol(await Otomatik(Alan("Evrakı veren cari"), "Liman", "Liman Market"), "Cari secildi");
        await KartDoldur(0, "Çek no", "100201");
        await KartDoldur(0, "Vade", "15.12.2026");
        await KartDoldur(0, "Tutar", "12500");
        await KartDugmesi(0, "Yeni");
        await Bekle("!!document.querySelectorAll('.evrak-karti')[0].querySelector('input') && [...document.querySelectorAll('.evrak-karti')[0].querySelectorAll('label')].some(l => l.textContent.startsWith('VKN'))", 5);
        await KartDoldur(0, "Keşideci adı", "Ahmet Yilmaz");
        await KartDoldur(0, "VKN / TCKN", "10000000146");
        Kontrol(await Bekle($"document.querySelector('.evrak-karti.riskli') && {Govde("tarihli 4.500,00 TRY evrakı karşılıksız çıktı")}", 10),
            "Kimlik yazilinca kayitli kesideci bulundu, risk uyarisi kartta");
        Kontrol(await Bekle("[...document.querySelectorAll('.evrak-karti')[0].querySelectorAll('input')].some(i => i.value === 'Halkbank')", 5),
            "Kesidecinin kayitli cek hesabi otomatik dolduruldu");
        await Tikla("Senet ekle");
        await Bekle("document.querySelectorAll('.evrak-karti').length === 2");
        await KartDoldur(1, "Senet no", "S-55");
        await KartDoldur(1, "Vade", "20.01.2027");
        await KartDoldur(1, "Tutar", "3000");
        await KartDugmesi(1, "Yeni");
        await Bekle("[...document.querySelectorAll('.evrak-karti')[1].querySelectorAll('label')].some(l => l.textContent.startsWith('Borçlu adı'))", 5);
        await KartDoldur(1, "Borçlu adı", "Mehmet Kaya");
        Kontrol(await Bekle(Govde("15.500,00")), "Canli bordro toplami 15.500");
        await Ekran("2-bordro-formu");
        await Tikla("Bordroyu kaydet");
        Kontrol(await Bekle("!!document.querySelector('.mud-dialog') && document.querySelector('.mud-dialog').innerText.includes('Yine de kabul')", 10),
            "Kayitta risk onayi soruldu");
        await DiyalogDugmesi("Yine de kabul et");
        Kontrol(await Bekle($"{Govde("Çek/Senet Giriş Bordrosu")} && {Govde("BR00003")} && {Govde("AC00002")} && {Govde("AS00001")}", 15),
            "Bordro kaydedildi, yazdirilabilir bordro acildi");
        await Ekran("3-bordro");

        await Git("/cek-senet");
        await Bekle(Govde("AC00002"));
        await Js("[...document.querySelectorAll('.veri-tablosu tbody tr')].find(t => t.textContent.includes('AC00002')).querySelector('input[type=checkbox]').click()");
        Kontrol(await Bekle($"{Govde("1 seçili")} && [...document.querySelectorAll('.toplu-cubuk button')].some(b => b.textContent.includes('Tahsile ver'))"),
            "Secimde ortak islemler gorundu");
        await Js("[...document.querySelectorAll('.toplu-cubuk button')].find(b => b.textContent.includes('Tahsile ver')).click()");
        Kontrol(await Bekle("!!document.querySelector('.mud-dialog') && document.querySelector('.mud-dialog').innerText.includes('Ziraat TL')"), "Tahsile verme diyalogu banka secili");
        await Ekran("4-islem-diyalogu");
        await Js("[...document.querySelectorAll('.mud-dialog button')].find(b => b.textContent.trim() === 'Tahsile ver' && !b.disabled).click()");
        Kontrol(await Bekle($"!document.querySelector('.mud-dialog') && {Govde("işlendi")}"), "Toplu islem uygulandi");
        Kontrol(await Bekle("(() => { const t = [...document.querySelectorAll('.veri-tablosu tbody tr')].find(t => t.textContent.includes('AC00002')); return t && t.textContent.includes('Tahsilde') && t.textContent.includes('Ziraat TL'); })()"),
            "Listede durum tahsilde, konum banka");

        await SatirTikla("AC00002");
        Kontrol(await Bekle($"!!document.querySelector('.evrak-gorseli.cek') && {Govde("12.500,00 TL")} && {Govde("Risk onayıyla kabul edildi")}"),
            "Evrak detayi: cek gorseli ve risk onayi bilgisi");
        await Js("[...document.querySelectorAll('.evrak-islem-dugmesi')].find(b => b.textContent.includes('Tahsil edildi')).click()");
        await Bekle("!!document.querySelector('.mud-dialog')");
        await Js("[...document.querySelectorAll('.mud-dialog button')].find(b => b.textContent.trim() === 'Tahsil edildi' && !b.disabled).click()");
        Kontrol(await Bekle($"!document.querySelector('.mud-dialog') && document.querySelectorAll('.zc-ogesi').length === 3 && {Govde("Tahsil edildi")}"),
            "Tahsil edildi; zaman cizelgesinde 3 hareket");
        await Ekran("5-evrak-detayi");
        await Tikla("Son hareketi geri al");
        await Bekle("!!document.querySelector('.mud-dialog')");
        await DiyalogDugmesi("Geri al");
        Kontrol(await Bekle("document.querySelectorAll('.zc-ogesi.geri-alindi').length === 1 && [...document.querySelectorAll('.evrak-islem-dugmesi')].some(b => b.textContent.includes('Tahsil edildi'))"),
            "Geri alindi: hareket ustu cizili, evrak tekrar tahsilde");

        await Git("/cek-senet");
        await Bekle(Govde("AS00001"));
        await SatirTikla("AS00001");
        await Bekle("!!document.querySelector('.evrak-gorseli.senet')");
        await Js("[...document.querySelectorAll('.evrak-islem-dugmesi')].find(b => b.textContent.includes('Karşılıksız')).click()");
        Kontrol(await Bekle("!!document.querySelector('.mud-dialog') && document.querySelector('.mud-dialog').innerText.includes('yeniden borçlandırılır')"),
            "Karsiliksiz diyalogu cari etkisini acikliyor");
        await Js("[...document.querySelectorAll('.mud-dialog button')].find(b => b.textContent.trim().startsWith('Karşılıksız') && !b.disabled).click()");
        Kontrol(await Bekle($"!!document.querySelector('.eg-damga') && {Govde("KARŞILIKSIZ")}"), "Senet karsiliksiz damgasi");
        Kontrol(await Js($"!{Govde("Risk onayıyla")} && !document.querySelector('.vade-kalan')") == "true",
            "Risksiz borclunun senedinde risk onayi ve karsiliksizda vade geri sayimi yok");
        await Ekran("6-karsiliksiz");

        await Git("/cek-senet/kesideciler");
        Kontrol(await Bekle($"{Govde("Ahmet Yilmaz")} && {Govde("Mehmet Kaya")}"), "Kesideci listesi");
        await Tikla("Riskliler");
        Kontrol(await Bekle($"{Govde("Ahmet Yilmaz")} && {Govde("Mehmet Kaya")}"), "Riskliler: iki kesideci (karsiliksiz gecmisli)");
        await SatirTikla("Ahmet Yilmaz");
        Kontrol(await Bekle("!!document.querySelector('.risk-bandi') && document.querySelector('.mud-dialog').innerText.includes('AC00001')"), "Kesideci kartinda karsiliksiz gecmisi");
        await Js("[...document.querySelectorAll('.mud-dialog .mud-switch')].find(s => s.textContent.includes('Kara listeye al'))?.querySelector('input').click()");
        await Task.Delay(300);
        await DiyalogDugmesi("Kaydet");
        Kontrol(await Bekle($"!document.querySelector('.mud-dialog') && {Govde("Kara listede")}"), "Kesideci kara listeye alindi");

        await Git("/cek-senet/vade-takvimi");
        Kontrol(await Bekle($"{Govde("Vade takvimi")} && {Govde("Tahsil edilecek")} && document.querySelectorAll('.veri-tablosu tbody tr').length === 1 && document.querySelectorAll('.sutun-grubu').length > 4", 15), "Vade takvimi: grafikte tum haftalar, tabloda yalniz vadeli hafta");
        await Ekran("7-vade-takvimi");

        await Git($"/cari/{musteri}");
        Kontrol(await Bekle($"{Govde("Çek/senet al")} && !!document.querySelector('.cek-riski') && {Govde("Portföyde")}", 15), "Cari kartinda cek riski ve cek/senet al");

        await Git("/ayarlar");
        Kontrol(await Bekle($"{Govde("Çekte keşideci VKN/TCKN zorunlu")} && {Govde("Uyar ve onay iste")}"), "Ayarlarda cek/senet bolumu");
    }
}
