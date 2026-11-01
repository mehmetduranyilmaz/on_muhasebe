using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace OnMuhasebe.Tarayici.Testler;

// Roller ve yetkiler: rol sayfası ve yetki tablosu; kısıtlı kullanıcıda menü, düğme ve maliyet gizleme, yetkisiz adres.
internal sealed class YetkiTesti : TarayiciTesti
{
    public override string Ad => "yetki";

    protected override async Task CalistirAsync()
    {
        await Komut("Page.enable");
        var damga = DateTime.UtcNow.Ticks;
        var yonetici = $"yetkiyon{damga}@ornek.com";
        var satisci = $"yetkisatis{damga}@ornek.com";
        var kayit = await KayitOlAsync(new { firmaUnvani = "Yetki Arayuz Ltd", ad = "Yavuz", soyad = "Yon", eposta = yonetici, sifre = "Sifre1234" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", kayit["accessToken"]!.GetValue<string>());

        // Satış rolüyle bir kullanıcı (API üzerinden davet + kabul) ve maliyetli bir stok kartı.
        var roller = (await http.GetFromJsonAsync<JsonArray>(Api + "api/yonetim/roller"))!;
        var satisRolu = roller.First(r => r!["ad"]!.GetValue<string>() == "Satış")!["id"]!.GetValue<string>();
        await Post("api/yonetim/kullanicilar", new { eposta = satisci, rolId = satisRolu });
        (await YeniIstemci().PostAsJsonAsync(Api + "api/yonetim/davet-kabul",
            new { kod = await MailpitKoduAsync(satisci, "davet"), ad = "Selin", soyad = "Satis", sifre = "Satis1234" })).EnsureSuccessStatusCode();
        var kart = await Post("api/stok/kartlar", new { ad = "Maliyetli Kalem", tur = "Urun", temelBirim = "ADET", kdvOrani = 20, alisFiyati = 40, satisFiyati = 55, paraBirimi = "TRY" });

        // --- Yönetici: rol sayfası
        await GirisYap(yonetici, "Sifre1234");
        Kontrol(await Bekle(Govde("Roller ve Yetkiler"), 15), "Yonetici menusunde Roller ve Yetkiler var");
        await Git("/roller");
        Kontrol(await Bekle($"{Govde("Tam yetki")} && {Govde("Satış")} && {Govde("Salt Okur (Mali Müşavir)")}", 15), "Hazir roller listelendi");
        await SatirTikla("Satış");
        Kontrol(await Bekle($"!!document.querySelector('.mud-dialog') && document.querySelectorAll('.yetki-hucre').length > 50", 10),
            "Rol penceresinde yetki tablosu acildi");
        Kontrol(await Js("[...document.querySelectorAll('.yetki-grup-basligi')].map(g => g.textContent).join('|')") is var gruplar
                && gruplar.Contains("Cari") && gruplar.Contains("Fatura") && gruplar.Contains("Yönetim"), "Tablo modul gruplarina ayrildi");
        await Ekran("1-rol-yetki-tablosu");
        await Tikla("Vazgeç");
        await Bekle("!document.querySelector('.mud-dialog')", 10);

        // --- Yönetici: kullanıcıya özel yetki penceresi
        await Git("/kullanicilar");
        await Bekle(Govde("Selin Satis"), 15);
        await Js($"[...document.querySelectorAll('.veri-tablosu tbody tr')].find(t => t.textContent.includes({S(satisci)})).querySelector('button').click()");
        Kontrol(await Bekle($"!!document.querySelector('.mud-dialog') && {Govde("Rolden farklı seçimler")}", 10), "Kullanici rol ve yetki penceresi acildi");
        await Ekran("2-kullanici-yetkileri");
        await Tikla("Vazgeç");
        await Bekle("!document.querySelector('.mud-dialog')", 10);

        // --- Satış kullanıcısı: menü, düğme ve maliyet
        await GirisYap(satisci, "Satis1234");
        await Bekle(Govde("Hoş geldiniz, Selin"), 15);
        var menu = await Js("document.querySelector('.mud-navmenu')?.innerText ?? ''");
        Kontrol(menu.Contains("Cari Hesaplar") && menu.Contains("Fatura") && menu.Contains("Teklifler"), "Satisci menude cari, fatura ve teklifleri goruyor");
        Kontrol(!menu.Contains("Kullanıcılar") && !menu.Contains("Ayarlar") && !menu.Contains("Krediler") && !menu.Contains("Demirbaşlar"),
            "Yetkisi olmayan bolumler menude yok");

        await Git("/kullanicilar");
        Kontrol(await Bekle(Govde("Bu sayfaya erişim yetkiniz yok."), 15), "Yetkisiz adres kilitli sayfa gosteriyor");

        await Git("/fatura");
        Kontrol(await Bekle(Govde("Yeni Fatura"), 15) || await Bekle(Govde("Faturalar"), 5), "Fatura listesi acildi");
        Kontrol(!await Bekle(Govde("Kârlılık"), 2), "Maliyet yetkisi olmayan Karlilik dugmesini gormuyor");

        await Git($"/stok/{kart["id"]!.GetValue<string>()}");
        Kontrol(await Bekle(Govde("Maliyetli Kalem"), 15), "Stok karti acildi");
        Kontrol(!await Bekle($"{Govde("Stok değeri")} || {Govde("Alış fiyatı")}", 2), "Stok kartinda maliyet ve alis fiyati gizli");
        Kontrol(!await Bekle(Govde("Hareket Ekle"), 1), "Stok hareketi yetkisi olmayan Hareket Ekle gormuyor");
        await Ekran("3-satisci-stok-karti");
    }
}
