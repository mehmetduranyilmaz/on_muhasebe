using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace OnMuhasebe.Tarayici.Testler;

// Satın Alma: onay kuralı, talep formu, onaya gönderme, kendi talebini onaylayamama, başka kullanıcının onayı, red ve yeniden gönderim, siparişe dönüşüm.
internal sealed class SatinalmaTesti : TarayiciTesti
{
    private static readonly string[] TalepYetkileri = ["satinalma.talep.goruntule", "satinalma.talep.ekle", "satinalma.talep.duzenle"];

    public override string Ad => "satinalma";

    protected override async Task CalistirAsync()
    {
        await Komut("Page.enable");
        var damga = DateTime.UtcNow.Ticks;
        var patron = $"satinalmaarayuz{damga}@ornek.com";
        var talepEden = $"sattalep{damga}@ornek.com";
        var kayit = await KayitOlAsync(new { firmaUnvani = "Satinalma Arayuz Ltd", ad = "Sena", soyad = "Patron", eposta = patron, sifre = "Sifre1234" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", kayit["accessToken"]!.GetValue<string>());
        await Post("api/cari/hesaplar", new { unvan = "Tedarikci Bir", tur = "Tedarikci", kisiTipi = "Tuzel", paraBirimi = "TRY" });

        var rol = await Post("api/yonetim/roller", new { ad = "Talep Eden", yetkiler = TalepYetkileri });
        await Post("api/yonetim/kullanicilar", new { eposta = talepEden, rolId = rol["id"]!.GetValue<string>() });
        var kod = await MailpitKoduAsync(talepEden, "davet");
        Kontrol(kod is not null, "Davet e-postasi geldi");
        await Post("api/yonetim/davet-kabul", new { kod, ad = "Tuna", soyad = "Talep", sifre = "Sifre1234" });

        await GirisYap(patron, "Sifre1234");
        Kontrol(await Bekle(Govde("Satın Alma"), 15), "Menude Satin Alma var");

        await Git("/satinalma/onay-kurallari");
        Kontrol(await Bekle(Govde("Basamak yok"), 20), "Kural yokken yonetici onaylar bilgisi");
        await Tikla("Basamak Ekle");
        await Bekle(Alan("Onaylayan") + " != null", 10);
        await Sec("Onaylayan", "Rol: Yönetici");
        await Task.Delay(300);
        await Tikla("Kuralları Kaydet");
        Kontrol(await Bekle(Govde("Onay kuralları kaydedildi"), 10), "Onay basamagi kaydedildi");
        await Git("/ayarlar");
        Kontrol(await Bekle(Govde("Satın Alma Onayı"), 20), "Ayarlarda onay kurallari paneli");

        await GirisYap(talepEden, "Sifre1234");
        await Git("/satinalma");
        Kontrol(await Bekle($"{Govde("Yeni Talep")} && {Govde("Talep yok")}", 20), "Bos talep listesi");
        await Tikla("Yeni Talep");
        await Bekle($"location.pathname === '/satinalma/yeni' && {Govde("Kalemler")}", 15);
        await Doldur("Gerekçe", "Atolye icin kaynak makinesi");
        await Tikla("Elle kalem");
        await Bekle("!!document.querySelector('.talep-kalemleri tbody tr input')", 10);
        await Js("""
            (() => { const girdiler = [...document.querySelectorAll('.talep-kalemleri tbody tr input')];
              const yaz = (i, v) => { const e = girdiler[i]; Object.getOwnPropertyDescriptor(Object.getPrototypeOf(e), 'value').set.call(e, v);
                e.dispatchEvent(new Event('input', { bubbles: true })); e.dispatchEvent(new Event('change', { bubbles: true })); e.dispatchEvent(new Event('blur', { bubbles: true })); };
              yaz(0, 'Kaynak makinesi'); yaz(3, '4000'); })()
            """);
        await Task.Delay(500);
        await Ekran("1-yeni-talep");
        await Tikla("Onaya Gönder");
        Kontrol(await Bekle($"/\\/satinalma\\/[0-9a-f-]{{36}}/.test(location.pathname) && {Govde("Onay geçmişi")} && {Govde("Bekliyor")}", 20), "Talep onaya gitti, gecmis gorunuyor");
        Kontrol(await Bekle($"{Govde("4.000,00")} && !{Govde("Onayınız bekleniyor")}", 10), "Tutar dogru, talep eden onay paneli gormuyor");
        var adres = await Js("location.pathname");
        await Ekran("2-onayda");

        await GirisYap(patron, "Sifre1234");
        await Git("/satinalma");
        await Bekle($"{Govde("Onayımı bekleyenler")}", 20);
        await Js("[...document.querySelectorAll('.tur-sekmesi')].find(b => b.textContent.includes('Onayımı bekleyenler')).click()");
        Kontrol(await Bekle(Govde("Atolye icin kaynak makinesi"), 15), "Yoneticinin bekleyenlerinde");
        await Ekran("3-bekleyenler");
        await SatirTikla("Atolye icin kaynak makinesi");
        Kontrol(await Bekle(Govde("Onayınız bekleniyor"), 20), "Yonetici onay panelini goruyor");

        await Tikla("Reddet");
        Kontrol(await Bekle(Govde("Reddetmek için gerekçe yazın"), 10), "Gerekcesiz red olmuyor");
        await Doldur("Not", "Fiyat yuksek");
        await Task.Delay(400);
        await Tikla("Reddet");
        Kontrol(await Bekle($"{Govde("Reddetti")} && {Govde("Fiyat yuksek")}", 15), "Reddedildi, gerekce gecmiste");

        await GirisYap(talepEden, "Sifre1234");
        await Git(adres);
        Kontrol(await Bekle($"{Govde("Onaya Gönder")} && {Govde("Reddetti")}", 20), "Talep eden yeniden gonderebilir");
        await Task.Delay(400);
        await Tikla("Onaya Gönder");
        Kontrol(await Bekle($"!{Govde("Onaya Gönder")} && {Govde("Bekliyor")}", 15), "Yeniden gonderildi");

        await GirisYap(patron, "Sifre1234");
        await Git(adres);
        await Bekle(Govde("Onayınız bekleniyor"), 20);
        await Task.Delay(400);
        await Tikla("Onayla");
        Kontrol(await Bekle($"{Govde("Siparişe dönüştür")} && {Govde("Onayladı")}", 15), "Onaylandi, siparis paneli acildi");
        await Otomatik(Alan("Tedarikçi"), "Tedarikci", "Tedarikci Bir");
        await Task.Delay(400);
        await Ekran("4-onaylandi");
        await Tikla("Sipariş Aç");
        Kontrol(await Bekle($"{Govde("Bağlı sipariş")} && {Govde("Siparişe dönüştü")}", 20), "Siparise donustu, bagli siparis gorunuyor");
        await Ekran("5-siparis");

        await Js("[...document.querySelectorAll('a')].find(a => a.getAttribute('href')?.startsWith('siparis/')).click()");
        Kontrol(await Bekle($"location.pathname.startsWith('/siparis/') && {Govde("Tedarikci Bir")} && {Govde("Satın alma talebi")}", 20), "Taslak siparis acildi, talebe referans var");

        await Git("/bildirimler");
        Kontrol(await Bekle($"{Govde("Onayınızı bekliyor")}", 15), "Yoneticiye onay bildirimi geldi");
    }
}
