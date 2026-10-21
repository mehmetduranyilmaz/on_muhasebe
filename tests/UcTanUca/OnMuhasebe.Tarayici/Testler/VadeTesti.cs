using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OnMuhasebe.Tarayici.Testler;

// Büyük veride vade analizi ekranını dener.
internal sealed class VadeTesti : TarayiciTesti
{
    public override string Ad => "vade";

    protected override async Task CalistirAsync()
    {
        await Komut("Page.enable");
        await Git("/giris");
        await Bekle("document.querySelectorAll('input').length >= 2");
        await Doldur("E-posta", "perfb1792539588@ornek.com");
        await Doldur("Şifre", "Sifre1234");
        await Tikla("Giriş Yap");
        await Bekle(Govde("Hoş"), 15);

        var once = DateTime.UtcNow;
        await Git("/cari/vade-analizi");
        Kontrol(await Bekle("document.querySelectorAll('.veri-tablosu tbody tr').length === 50", 20), "20 bin carilik firmada ilk sayfa 50 satir");
        Console.WriteLine($"   ilk yukleme {(DateTime.UtcNow - once).TotalMilliseconds:F0} ms");
        Kontrol(await Bekle("!!document.querySelector('.mud-pagination')", 5), "Sayfalama gorunuyor");
        Kontrol(await Bekle(Govde("90 günden fazla"), 5), "Ozet kartlari");
        await Ekran("1-vade");
        var ilk = await Js("document.querySelector('.veri-tablosu tbody tr').innerText");
        await Js("[...document.querySelectorAll('.mud-pagination button')].find(b => b.textContent.trim() === '2')?.click()");
        Kontrol(await Bekle($"document.querySelector('.veri-tablosu tbody tr')?.innerText !== {S(ilk)} && document.querySelectorAll('.veri-tablosu tbody tr').length === 50", 10), "Ikinci sayfa yuklendi");
        await Tikla("Borçlarımız");
        Kontrol(await Bekle("document.querySelectorAll('.veri-tablosu tbody tr').length === 50", 10), "Borclarimiz sekmesi sunucudan geldi");
        await Js($$"""
            (() => { const i = document.querySelector('.vade-arama input');
              Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(i, 'P000777');
              i.dispatchEvent(new Event('input', { bubbles: true })); })()
            """);
        Kontrol(await Bekle("document.querySelectorAll('.veri-tablosu tbody tr').length === 1 && document.body.innerText.includes('777')", 10), "Arama tek cariye indirdi");
        await Ekran("2-arama");
    }
}
