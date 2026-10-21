using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OnMuhasebe.Tarayici.Testler;

// WhatsApp paylaşım bağlantılarını dener.
internal sealed class WhatsappTesti : TarayiciTesti
{
    public override string Ad => "whatsapp";

    protected override async Task CalistirAsync()
    {
        await Komut("Page.enable");
        var eposta = $"waui{DateTime.UtcNow.Ticks}@ornek.com";
        var kayit = await KayitOlAsync(new { firmaUnvani = "Mesaj Ticaret", ad = "Ece", soyad = "Su", eposta, sifre = "Sifre1234" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", kayit["accessToken"]!.GetValue<string>());
        var cariId = (await Post("api/cari/hesaplar", new { unvan = "Telefonlu Musteri", tur = "Musteri", kisiTipi = "Tuzel", paraBirimi = "TRY", telefon = "0532 111 22 33" }))["id"]!.GetValue<string>();
        var telefonsuzId = (await Post("api/cari/hesaplar", new { unvan = "Telefonsuz Musteri", tur = "Musteri", kisiTipi = "Tuzel", paraBirimi = "TRY" }))["id"]!.GetValue<string>();
        var bugun = DateTime.Today.ToString("yyyy-MM-dd");
        await Post($"api/cari/hesaplar/{cariId}/hareketler", new { tur = "Dekont", yon = "Borc", tarih = bugun, tutar = 1250.5 });
        var fatura = await Post("api/fatura/faturalar", new
        {
            tur = "Satis", cariHesapId = cariId, tarih = bugun, vadeTarihi = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd"), genelIskontoTuru = "Yuzde", genelIskonto = 0,
            satirlar = new[] { new { stokKartiId = (string?)null, ad = "Danismanlik", birim = "ADET", miktar = 1, birimFiyat = 1000, kdvOrani = 20, kdvDahilMi = false, iskonto1 = 0, iskonto2 = 0, iskonto3 = 0 } }
        });
        var faturaId = fatura["fatura"]!["id"]!.GetValue<string>();

        await Git("/giris");
        await Bekle("document.querySelectorAll('input').length >= 2");
        await Doldur("E-posta", eposta);
        await Doldur("Şifre", "Sifre1234");
        await Tikla("Giriş Yap");
        await Bekle(Govde("Hoş"), 15);

        string Href() => "document.querySelector('a.whatsapp-dugmesi')?.href ?? ''";
        string Metin() => "decodeURIComponent((document.querySelector('a.whatsapp-dugmesi')?.href ?? '').split('?text=')[1] ?? '')";

        await Git($"/cari/{cariId}/ekstre");
        Kontrol(await Bekle($"{Href()}.startsWith('https://wa.me/905321112233?text=')", 15), "Ekstrede telefonlu cariye wa.me baglantisi (90 ile)");
        var ekstre = await Js(Metin());
        Kontrol(ekstre.Contains("Sayın Telefonlu Musteri") && ekstre.Contains("Borç bakiyeniz: 2.450,50") && ekstre.EndsWith("Mesaj Ticaret"),
            $"Ekstre mesaji unvan, bakiye ve firma imzasiyla ({ekstre.Replace('\n', '|')})");
        Kontrol(await Js("document.querySelector('a.whatsapp-dugmesi').target") == "_blank", "Yeni sekmede acilir");
        await Ekran("1-ekstre");

        await Git($"/cari/{telefonsuzId}/ekstre");
        Kontrol(await Bekle($"{Href()}.startsWith('https://wa.me/?text=')", 15), "Telefonsuz caride kisi secimli baglanti");
        Kontrol((await Js(Metin())).Contains("Hesabınız kapalıdır"), "Bakiyesiz cari mesaji");

        await Git($"/fatura/{faturaId}");
        Kontrol(await Bekle($"{Href()}.startsWith('https://wa.me/905321112233?text=')", 15), "Faturada cari telefonuyla baglanti");
        var faturaMetni = await Js(Metin());
        Kontrol(faturaMetni.Contains("satış faturası") && faturaMetni.Contains("Genel toplam: 1.200,00") && faturaMetni.Contains("Vade:"),
            $"Fatura mesaji tur, toplam ve vadeyle ({faturaMetni.Replace('\n', '|')})");
        await Ekran("2-fatura");
    }
}
