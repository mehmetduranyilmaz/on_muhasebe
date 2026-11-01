using System.Net.Http.Headers;

namespace OnMuhasebe.Tarayici.Testler;

// Tekrarlayan fatura: plan formu, kaçan dönem üretimi, taslak fatura, duraklatma, faturadan plan.
internal sealed class TekrarFaturaTesti : TarayiciTesti
{
    public override string Ad => "tekrar_fatura";

    protected override async Task CalistirAsync()
    {
        await Komut("Page.enable");
        var eposta = $"tekrararayuz{DateTime.UtcNow.Ticks}@ornek.com";
        var kayit = await KayitOlAsync(new { firmaUnvani = "Tekrar Arayuz Ltd", ad = "Tuna", soyad = "Plan", eposta, sifre = "Sifre1234" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", kayit["accessToken"]!.GetValue<string>());
        await Post("api/cari/hesaplar", new { unvan = "Kiraci Market", tur = "Musteri", kisiTipi = "Tuzel", paraBirimi = "TRY" });

        await GirisYap(eposta, "Sifre1234");
        Kontrol(await Bekle(Govde("Tekrarlayan Faturalar"), 15), "Menude Tekrarlayan Faturalar var");
        await Git("/tekrar-fatura");
        Kontrol(await Bekle($"{Govde("Yeni Plan")} && {Govde("Tekrar planı yok")}", 20), "Bos plan listesi");
        await Tikla("Yeni Plan");
        await Bekle($"location.pathname === '/tekrar-fatura/yeni' && {Govde("Yıllık artış")}", 15);
        await Doldur("Plan adı", "Depo kirasi");
        Kontrol(await Otomatik(Alan("Cari"), "Kiraci", "Kiraci Market"), "Cari secildi");

        var baslangic = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);
        await Doldur("Başlangıç", baslangic.ToString("dd.MM.yyyy"));
        await Doldur("Ayın günü", "1");
        await Doldur("Fatura açıklaması", "{ay} {yil} kira bedeli");
        await Tikla("Serbest satır");
        await Task.Delay(300);
        await SatirDoldur(0, "Ürün / hizmet", "Depo kira bedeli");
        await SatirDoldur(0, "Fiyat", "1000");
        await Task.Delay(400);
        await Ekran("1-plan-formu");
        await Tikla("Kaydet");
        Kontrol(await Bekle($"/\\/tekrar-fatura\\/[0-9a-f-]{{36}}/.test(location.pathname) && {Govde("Şimdi Üret")}", 20), "Plan kaydedildi, sira gelmis");

        await Tikla("Şimdi Üret");
        Kontrol(await Bekle($"{Govde("Kesilen dönemler")} && {Govde("Taslak fatura")} && !{Govde("Şimdi Üret")}", 20), "Kacan donemler taslak olustu");
        await Ekran("2-gecmis");

        await Tikla("Duraklat");
        Kontrol(await Bekle($"{Govde("Duraklatıldı")} && {Govde("Devam Et")}", 10), "Plan duraklatildi");
        await Tikla("Devam Et");
        Kontrol(await Bekle(Govde("Duraklat"), 10), "Plan devam etti");

        await Js("[...document.querySelectorAll('.tekrar-gecmisi a')][0].click()");
        var ay = DateTime.Today.ToString("MMMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
        Kontrol(await Bekle($"location.pathname.startsWith('/fatura/') && {Govde("Kiraci Market")} && {Govde(ay + " kira bedeli")}", 20), "Taslak fatura donem aciklamasiyla acildi");
        await Ekran("3-fatura");

        await Js("document.querySelector('a[href^=\"tekrar-fatura/yeni\"]').click()");
        Kontrol(await Bekle($"location.pathname === '/tekrar-fatura/yeni' && {Govde("Yıllık artış")}", 15), "Faturadan plan formu acildi");
        Kontrol(await Bekle("(" + Alan("Cari") + ")?.value.includes('Kiraci Market')", 10), "Cari faturadan geldi");

        await Git("/tekrar-fatura");
        Kontrol(await Bekle($"{Govde("Depo kirasi")} && {Govde("Her ay")}", 20), "Plan listede");
        await Ekran("4-liste");
    }
}
