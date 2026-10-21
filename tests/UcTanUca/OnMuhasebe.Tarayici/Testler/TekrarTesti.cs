using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OnMuhasebe.Tarayici.Testler;

// Mükerrer kayıt korumasını dener.
internal sealed class TekrarTesti : TarayiciTesti
{
    public override string Ad => "tekrar";

    protected override async Task CalistirAsync()
    {
        // CDP olayı (Komut cevapları beklenirken gelenler kuyrukta tutulur).
        async Task<JsonNode?> Olay(string metot, int saniye = 15)
        {
            var son = DateTime.UtcNow.AddSeconds(saniye);
            while (DateTime.UtcNow < son)
            {
                var bulunan = olaylar.FirstOrDefault(o => o["method"]?.GetValue<string>() == metot);
                if (bulunan is not null)
                {
                    olaylar.Remove(bulunan);
                    return bulunan;
                }

                await Js("1");
                await Task.Delay(100);
            }

            return null;
        }

        async Task<decimal> KasaBakiyesi(string id) =>
            (await http.GetFromJsonAsync<JsonObject>(Api + $"api/kasabanka/hesaplar/{id}"))!["bakiye"]!.GetValue<decimal>();

        await Komut("Page.enable");
        var eposta = $"tekrarui{DateTime.UtcNow.Ticks}@ornek.com";
        await KayitOlAsync(new { firmaUnvani = "Tekrar Arayuz Ltd", ad = "Ece", soyad = "Ak", eposta, sifre = "Sifre1234" });
        var token = (await Post("api/yonetim/giris", new { eposta, sifre = "Sifre1234" }))["accessToken"]!.GetValue<string>();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var cari = (await Post("api/cari/hesaplar", new { unvan = "Liman Market", tur = "Musteri", kisiTipi = "Tuzel", paraBirimi = "TRY" }))["id"]!.GetValue<string>();
        var kasa = (await Post("api/kasabanka/hesaplar", new { ad = "Merkez Kasa", tur = "Kasa", paraBirimi = "TRY" }))["id"]!.GetValue<string>();

        await Git("/giris");
        await Bekle("document.querySelectorAll('input').length >= 2");
        await Js("performance.setResourceTimingBufferSize(5000)");
        await Doldur("E-posta", eposta);
        await Doldur("Şifre", "Sifre1234");
        await Tikla("Giriş Yap");
        await Bekle(Govde("Hoş"), 15);

        await Git($"/kasabanka/islem/tahsilat?hesap={kasa}&cari={cari}&donus=kasabanka/{kasa}");
        Kontrol(await Bekle($"!!{Alan("Tutar")} && {Govde("Liman Market")}", 20), "Tahsilat formu cari ve kasa secili acildi");
        await Doldur("Tutar", "300");
        await Task.Delay(400);

        // Cevap aşamasında yakalanan istek sunucuda işlenmiştir; tarayıcıya ise bağlantı hatası olarak döner.
        await Komut("Fetch.enable", new { patterns = new[] { new { urlPattern = "*islemler/tahsilat*", requestStage = "Response" } } });
        await Kaydet();
        var duraklayan = await Olay("Fetch.requestPaused");
        while (duraklayan?["params"]?["request"]?["method"]?.GetValue<string>() == "OPTIONS")
        {
            await Komut("Fetch.continueResponse", new { requestId = duraklayan["params"]!["requestId"]!.GetValue<string>() });
            duraklayan = await Olay("Fetch.requestPaused");
        }
        Kontrol(duraklayan is not null && duraklayan["params"]!["responseStatusCode"]?.GetValue<int>() == 201, "Ilk gonderim sunucuda islendi (201), cevap yolda kesiliyor");
        await Komut("Fetch.failRequest", new { requestId = duraklayan!["params"]!["requestId"]!.GetValue<string>(), errorReason = "ConnectionReset" });
        await Komut("Fetch.disable");
        Kontrol(await Bekle($"{Govde("Sunucuya ulaşılamadı")} && location.pathname.includes('/islem/tahsilat')", 10),
            "Kullanici baglanti hatasi goruyor, form acik kaldi");
        Kontrol(await KasaBakiyesi(kasa) == 300m, "Ilk gonderim kayda gecmisti (kasa 300)");
        await Ekran("1-baglanti-hatasi");

        await Kaydet();
        Kontrol(await Bekle($"location.pathname === '/kasabanka/{kasa}'", 15), "Tekrar Kaydet: basarili cevap alindi, hesaba donuldu");
        Kontrol(await KasaBakiyesi(kasa) == 300m, "Tekrar gonderim ikinci tahsilat yazmadi (kasa hala 300)");
        Kontrol(await Bekle("document.querySelectorAll('.veri-tablosu tbody tr').length === 1"), "Hesap hareketlerinde tek tahsilat");

        // Başarılı kayıttan sonra yeni form yeni anahtarla açılır: bilinçli ikinci tahsilat engellenmez.
        await Git($"/kasabanka/islem/tahsilat?hesap={kasa}&cari={cari}&donus=kasabanka/{kasa}");
        await Bekle("!!" + Alan("Tutar"), 15);
        await Doldur("Tutar", "300");
        await Task.Delay(400);
        await Kaydet();
        await Bekle($"location.pathname === '/kasabanka/{kasa}'", 15);
        Kontrol(await KasaBakiyesi(kasa) == 600m, "Yeni formdan ayni tutarli ikinci tahsilat kaydedildi (kasa 600)");
    }
}
