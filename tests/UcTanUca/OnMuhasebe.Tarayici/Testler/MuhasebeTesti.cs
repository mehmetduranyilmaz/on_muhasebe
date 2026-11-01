using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace OnMuhasebe.Tarayici.Testler;

// Muhasebe aktarımı: menü, hesap planı ve kart kodu, fiş önizleme, dosya indirme, aktarıldı işareti ve geri alma, mali müşavir rolü işareti.
internal sealed class MuhasebeTesti : TarayiciTesti
{
    public override string Ad => "muhasebe";

    protected override async Task CalistirAsync()
    {
        await Komut("Page.enable");
        var eposta = $"muhasebearayuz{DateTime.UtcNow.Ticks}@ornek.com";
        var kayit = await KayitOlAsync(new { firmaUnvani = "Muhasebe Arayuz Ltd", ad = "Mine", soyad = "Yon", eposta, sifre = "Sifre1234" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", kayit["accessToken"]!.GetValue<string>());

        // Geçen ayın faturası: sayfa varsayılan olarak geçen ayı açar.
        var gecenAy = DateTime.Today.AddDays(1 - DateTime.Today.Day).AddMonths(-1).AddDays(14).ToString("yyyy-MM-dd");
        var cari = await Post("api/cari/hesaplar", new { unvan = "Fis Musterisi", tur = "Musteri", kisiTipi = "Tuzel", paraBirimi = "TRY" });
        var fatura = await http.PostAsJsonAsync(Api + "api/fatura/faturalar", new
        {
            tur = "Satis", cariHesapId = cari["id"]!.GetValue<string>(), tarih = gecenAy, genelIskontoTuru = "Yuzde", genelIskonto = 0,
            satirlar = new[] { new { stokKartiId = (string?)null, ad = "Danismanlik", birim = "ADET", miktar = 1, birimFiyat = 1000, kdvOrani = 20, kdvDahilMi = false, iskonto1 = 0, iskonto2 = 0, iskonto3 = 0 } }
        });
        Kontrol(fatura.IsSuccessStatusCode, "Hazirlik: gecen aya satis faturasi kesildi");

        await GirisYap(eposta, "Sifre1234");
        Kontrol(await Bekle(Govde("Muhasebe Aktarımı"), 15), "Menude Muhasebe Aktarimi var");

        await Git("/hesap-plani");
        Kontrol(await Bekle($"{Govde("Ana hesaplar")} && {Govde("Alıcılar (müşteriler)")} && {Govde("120.01.00012")}", 20), "Hesap plani varsayilanlarla acildi");
        await Ekran("1-hesap-plani");
        await Tikla("Cariler");
        Kontrol(await Bekle($"{Govde("Fis Musterisi")} && {Govde("120.01.")}", 15), "Cari sekmesinde otomatik hesap kodu");
        await Js("""
            (() => { const i = document.querySelector('.kart-eslesme input');
              Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(i, '120.99.7');
              i.dispatchEvent(new Event('input', { bubbles: true })); i.dispatchEvent(new Event('change', { bubbles: true })); i.blur(); })()
            """);
        Kontrol(await Bekle(Govde("hesap kodu 120.99.7"), 10), "Cariye ozel hesap kodu kaydedildi");

        await Git("/muhasebe");
        Kontrol(await Bekle($"{Govde("Fis Musterisi")} && {Govde("Yeni")} && {Govde("Aktarılacaklar (1)")}", 20), "Gecen ayin fisi aktarilacaklar listesinde");
        await Js("[...document.querySelectorAll('.nakit-tablosu tbody tr')].find(t => t.textContent.includes('Fis Musterisi'))?.click()");
        Kontrol(await Bekle($"{Govde("120.99.7")} && {Govde("600")} && {Govde("391")}", 10), "Satira tiklayinca fis satirlari acildi, ozel kod gorunuyor");
        await Ekran("2-fis-onizleme");

        // Gerçek indirme akışı çalışır; yalnız tıklama anında dosya adı ve boyutu kaydedilir.
        await Js("""
            window.__dosyalar = [];
            const nesneUrl = URL.createObjectURL;
            URL.createObjectURL = b => { window.__boyut = b.size; return nesneUrl.call(URL, b); };
            HTMLAnchorElement.prototype.click = function () { if (this.download) window.__dosyalar.push(this.download + '|' + window.__boyut); };
            """);
        await Tikla("Excel");
        Kontrol(await Bekle("window.__dosyalar.some(d => /\\.xlsx\\|[1-9]/.test(d))", 15), "Excel dosyasi indirildi");
        await Tikla("CSV");
        Kontrol(await Bekle("window.__dosyalar.some(d => /\\.csv\\|[1-9]/.test(d))", 15), "CSV dosyasi indirildi");

        await Tikla("Aktarıldı işaretle");
        await Bekle("!!document.querySelector('.mud-dialog')", 5);
        await DiyalogDugmesi("Aktarıldı işaretle");
        Kontrol(await Bekle($"{Govde("Dönem aktarıldı olarak işaretlendi.")} && {Govde("Aktarılanlar (1)")} && {Govde("Aktarım geçmişi")}", 15), "Donem aktarildi, gecmiste gorunuyor");
        await Ekran("3-aktarildi");

        await Tikla("Geri al");
        await Bekle("!!document.querySelector('.mud-dialog')", 5);
        await DiyalogDugmesi("Geri al");
        Kontrol(await Bekle($"{Govde("Aktarım geri alındı.")} && {Govde("Aktarılacaklar (1)")} && {Govde("İptal")}", 15), "Aktarim geri alindi, fis yeniden aktarilacak");

        await Git("/roller");
        Kontrol(await Bekle($"{Govde("Salt Okur (Mali Müşavir)")} && {Govde("Mali müşavir")}", 15), "Rol listesinde mali musavir isareti");
        await SatirTikla("Salt Okur (Mali Müşavir)");
        await Bekle("!!document.querySelector('.mud-dialog')", 5);

        Kontrol(await Bekle("[...document.querySelectorAll('.mud-dialog .mud-switch')].some(s => s.textContent.includes('Mali müşavir rolü') && s.querySelector('input').checked)", 10),
            "Rol diyalogunda mali musavir anahtari acik");
        await Ekran("4-mali-musavir-rolu");
    }
}
