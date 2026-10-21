using System.Net.Http.Headers;

namespace OnMuhasebe.Tarayici.Testler;

// Kullanıcı daveti: yönetici davet eder, bekleyenler listesi, davet edilenin bağlantıyla hesap kurup katılması, iptal.
internal sealed class DavetTesti : TarayiciTesti
{
    public override string Ad => "davet";

    protected override async Task CalistirAsync()
    {
        await Komut("Page.enable");
        var damga = DateTime.UtcNow.Ticks;
        var yonetici = $"davetyon{damga}@ornek.com";
        var davetli = $"davetli{damga}@ornek.com";
        var iptalEdilecek = $"davetiptal{damga}@ornek.com";
        var kayit = await KayitOlAsync(new { firmaUnvani = "Davet Arayuz Ltd", ad = "Yasemin", soyad = "Yon", eposta = yonetici, sifre = "Sifre1234" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", kayit["accessToken"]!.GetValue<string>());

        await GirisYap(yonetici, "Sifre1234");
        await Git("/kullanicilar");
        Kontrol(await Bekle(Govde("Kullanıcı Davet Et"), 15), "Kullanicilar sayfasinda davet dugmesi");

        foreach (var eposta in new[] { davetli, iptalEdilecek })
        {
            await Tikla("Kullanıcı Davet Et");
            await Bekle("!!document.querySelector('.mud-dialog') && " + Govde("davet e-postası gönderilir"), 10);
            await Doldur("E-posta", eposta);
            await Task.Delay(300);
            if (eposta == davetli)
            {
                await Ekran("1-davet-diyalogu");
            }

            await DiyalogDugmesi("Davet gönder");
            await Bekle("!document.querySelector('.mud-dialog')", 10);
        }

        Kontrol(await Bekle($"{Govde("Bekleyen davetler")} && {Govde(davetli)} && {Govde(iptalEdilecek)}", 10), "Iki davet bekleyenler listesinde");
        await Ekran("2-bekleyen-davetler");

        // --- İptal
        var iptalKodu = await MailpitKoduAsync(iptalEdilecek, "davet");
        await Js($"[...document.querySelectorAll('.veri-tablosu tbody tr')].find(t => t.textContent.includes({S(iptalEdilecek)})).querySelectorAll('button')[1].click()");
        await Bekle(Govde("Devam edilsin mi?"), 5);
        await DiyalogDugmesi("İptal et");
        Kontrol(await Bekle($"{Govde("Davet iptal edildi.")} && !{Govde(iptalEdilecek)}", 10), "Davet iptal edildi, listeden kalkti");

        // --- Davetlinin tarafı: oturumu kapat, bağlantıyı aç
        await Komut("Network.clearBrowserCookies");
        await Js("localStorage.clear(); sessionStorage.clear();");
        await Git($"/davet#kod={iptalKodu}");
        Kontrol(await Bekle(Govde("Davet bağlantısı geçersiz"), 15), "Iptal edilen davetin baglantisi gecersiz");

        await Git($"/davet#kod={await MailpitKoduAsync(davetli, "davet")}");
        Kontrol(await Bekle($"{Govde("Davet Arayuz Ltd")} && {Govde("hesabınızı oluşturun")}", 15), "Davet sayfasi: firma adi, yeni hesap formu");
        Kontrol(await Js("location.hash") == "", "Davet kodu adres cubugundan silindi");
        await Doldur("Ad", "Deniz");
        await Doldur("Soyad", "Davetli");
        await Doldur("Şifre", "Davetli123");
        await Doldur("Şifre (tekrar)", "Davetli123");
        await Task.Delay(300);
        await Ekran("3-davet-kabul");
        await Tikla("Daveti kabul et");
        Kontrol(await Bekle($"location.pathname === '/' && {Govde("Hoş geldiniz, Deniz")} && {Govde("Davet Arayuz Ltd")}", 15), "Davet kabul edildi, firmada oturum acildi");

        await GirisYap(yonetici, "Sifre1234");
        await Git("/kullanicilar");
        Kontrol(await Bekle($"{Govde("Deniz Davetli")} && {Govde(davetli)} && !{Govde("Bekleyen davetler")}", 15), "Davetli kullanicilar listesinde, bekleyen davet kalmadi");
    }
}
