using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OnMuhasebe.Tarayici.Testler;

// Seri (ardışık) çek/senet girişini dener.
internal sealed class SeriGirisTesti : TarayiciTesti
{
    public override string Ad => "seri";

    protected override async Task CalistirAsync()
    {
        await Komut("Page.enable");
        var eposta = $"seriui{DateTime.UtcNow.Ticks}@ornek.com";
        var kayit = (await (await http.PostAsJsonAsync(Api + "api/yonetim/kayit",
            new { firmaUnvani = "Seri Arayuz", ad = "Ada", soyad = "Su", eposta, sifre = "Sifre1234" })).Content.ReadFromJsonAsync<JsonObject>())!;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", kayit["accessToken"]!.GetValue<string>());
        var musteri = (await Post("api/cari/hesaplar", new { unvan = "Taksitli Musteri", tur = "Musteri", kisiTipi = "Tuzel", paraBirimi = "TRY" }))["id"]!.GetValue<string>();
        var tedarikci = (await Post("api/cari/hesaplar", new { unvan = "Tedarikci Ltd", tur = "Tedarikci", kisiTipi = "Tuzel", paraBirimi = "TRY" }))["id"]!.GetValue<string>();
        var banka = (await Post("api/kasabanka/hesaplar", new { ad = "Is Bankasi", tur = "Banka", paraBirimi = "TRY" }))["id"]!.GetValue<string>();
        var karne = (await Post("api/kasabanka/cek-senet/karneler", new { tur = "Cek", kendiHesapId = banka, baslangicSeri = "K 500", yaprakSayisi = 10, tarih = "2026-10-01" }))["id"]!.GetValue<string>();
        await Post($"api/kasabanka/cek-senet/karneler/{karne}/yaprak-iptal", new { seriNo = "K501", neden = "Yirtildi" });

        await Git("/giris");
        await Bekle("document.querySelectorAll('input').length >= 2");
        await Doldur("E-posta", eposta);
        await Doldur("Şifre", "Sifre1234");
        await Tikla("Giriş Yap");
        await Bekle(Govde("Hoş"), 15);

        string Kartlar() => "[...document.querySelectorAll('.evrak-karti')]";
        string KartInput(int kart, int girdi) => $"{Kartlar()}[{kart}]?.querySelectorAll('input')[{girdi}]?.value";

        // --- Alınan: 12 taksitli senet, toplam 12.000 eşit bölünür
        await Git($"/cek-senet/giris?cari={musteri}");
        await Bekle($"{Kartlar()}.length === 1", 15);
        await Js($"{Kartlar()}[0].querySelector('button[aria-label=\"Sil\"]').click()");
        await Tikla("Senet ekle");
        await Bekle($"{Kartlar()}.length === 1", 5);
        await Doldur("Senet no", "S-0001");
        await Doldur("Vade", "31.01.2027");
        await Tikla("Yeni");
        await Bekle($"!!{Alan("Borçlu adı")}", 5);
        await Doldur("Borçlu adı", "Taksit Borclusu");
        await Task.Delay(300);
        await Js($"{Kartlar()}[0].querySelector('button[aria-label=\"Seri ekle\"]').click()");
        Kontrol(await Bekle($"!!document.querySelector('.mud-dialog') && {Alan("Adet")}?.value === '12'", 10), "Seri diyalogu senette 12 adetle acildi");
        await Doldur("Evrak başına tutar", "12000");
        await Js("[...document.querySelectorAll('.mud-dialog .mud-switch input')][0].click()");
        Kontrol(await Bekle("document.querySelectorAll('.seri-onizleme tbody tr').length === 12 && document.querySelector('.seri-onizleme tfoot').textContent.includes('12.000,00')", 5),
            "Onizleme: 12 satir, toplam 12.000");
        Kontrol(await Js("document.querySelectorAll('.seri-onizleme tbody tr')[1].textContent") is var ikinci && ikinci.Contains("S-0002") && ikinci.Contains("28.02.2027") && ikinci.Contains("1.000,00"),
            "Ikinci senet S-0002, 28.02.2027 (ay sonu kaymaz), 1.000,00");
        await Ekran("1-seri-dialog");
        await DiyalogDugmesi("12 evrak oluştur");
        Kontrol(await Bekle($"{Kartlar()}.length === 12", 5), "12 senet karti olustu");
        Kontrol(await Js(KartInput(11, 0)) == "S-0012" && await Js(KartInput(11, 1)) == "31.12.2027", "Son senet S-0012, vade 31.12.2027");
        await Ekran("2-seri-kartlar");
        await Tikla("Bordroyu kaydet");
        Kontrol(await Bekle("location.pathname.startsWith('/cek-senet/bordro/')", 15), "Giris bordrosu kaydedildi");
        var evraklar = await http.GetFromJsonAsync<JsonObject>(Api + "api/kasabanka/cek-senet/evraklar?yon=Alinan&boyut=50");
        Kontrol(evraklar!["toplamKayit"]!.GetValue<int>() == 12, "API'de 12 alinan senet");

        // --- Verilen: karneden 3 çek, iptal edilen yaprak atlanır
        await Git($"/cek-senet/cikis?cari={tedarikci}");
        Kontrol(await Bekle($"{KartInput(0, 0)} === 'K500'", 15), "Cikista karneden ilk yaprak K500");
        await Doldur("Vade", "15.11.2026");
        await Doldur("Tutar", "2500");
        await Task.Delay(300);
        await Js($"{Kartlar()}[0].querySelector('button[aria-label=\"Seri ekle\"]').click()");
        Kontrol(await Bekle($"{Govde("Numaralar karneden sırayla")} && {Alan("Adet")}?.value === '3'", 10), "Karneli seri: numaralar karneden, 3 adet");
        await DiyalogDugmesi("3 evrak oluştur");
        Kontrol(await Bekle($"{Kartlar()}.length === 3 && {KartInput(1, 0)} === 'K502' && {KartInput(2, 0)} === 'K503'", 5),
            "Karneden sirayla K500, K502, K503 (K501 iptal atlandi)");
        Kontrol(await Js(KartInput(2, 1)) == "15.01.2027", "Ucuncu cek vadesi 15.01.2027");
        await Ekran("3-karneli-seri");
        await Tikla("Bordroyu kaydet");
        Kontrol(await Bekle("location.pathname.startsWith('/cek-senet/bordro/')", 15), "Cikis bordrosu kaydedildi");
    }
}
