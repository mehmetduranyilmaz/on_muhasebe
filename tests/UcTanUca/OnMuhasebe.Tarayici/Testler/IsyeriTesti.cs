namespace OnMuhasebe.Tarayici.Testler;

// Çoklu işyeri: tek işyerinde seçici görünmez; Ayarlar'dan şube ve seri eklenir, üst çubukta işyeri seçilir, yeni belge seçili işyerine önerilir.
internal sealed class IsyeriTesti : TarayiciTesti
{
    public override string Ad => "isyeri";

    protected override async Task CalistirAsync()
    {
        await Komut("Page.enable");
        var eposta = $"isyeriarayuz{DateTime.UtcNow.Ticks}@ornek.com";
        await KayitOlAsync(new { firmaUnvani = "Isyeri Arayuz Ltd", ad = "Irem", soyad = "Yon", eposta, sifre = "Sifre1234" });

        await GirisYap(eposta, "Sifre1234");
        Kontrol(await Bekle("!document.querySelector('.isyeri-secici')", 5), "Tek isyerli firmada ust cubukta isyeri secici yok");

        await Git("/ayarlar");
        Kontrol(await Bekle($"{Govde("İşyerleri")} && {Govde("MRK")} && {Govde("Merkez")}", 15), "Ayarlar'da isyerleri paneli ve merkez");
        await Tikla("Yeni işyeri");
        await Bekle("!!document.querySelector('.mud-dialog') && " + Govde("SGK işyeri sicil no"), 10);
        await Doldur("Kod", "ANK");
        await Doldur("Ad", "Ankara Sube");
        await Doldur("İl", "Ankara");
        await Doldur("Toptan satış", "AN1");
        await Task.Delay(300);
        await Ekran("1-isyeri-diyalogu");
        await DiyalogDugmesi("Kaydet");
        Kontrol(await Bekle($"!document.querySelector('.mud-dialog') && {Govde("Ankara Sube")} && {Govde("İşyeri kaydedildi.")}", 10), "Sube eklendi");
        Kontrol(await Bekle("!!document.querySelector('.isyeri-secici')", 10), "Ikinci isyeriyle ust cubukta secici belirdi");

        await Js("document.querySelector('.isyeri-secici button').click()");
        await Bekle(Govde("Tüm işyerleri"), 5);
        await Js("[...document.querySelectorAll('.mud-menu-item, .mud-list-item')].find(e => e.textContent.trim() === 'Ankara Sube')?.click()");
        Kontrol(await Bekle("document.querySelector('.isyeri-secici')?.textContent.includes('Ankara Sube')", 10), "Ust cubukta Ankara secildi");

        await Git("/fatura/yeni/satis");
        Kontrol(await Bekle($"[...document.querySelectorAll('.mud-input-control')].some(c => c.textContent.includes('İşyeri') && c.innerText.includes('Ankara'))", 15),
            "Yeni faturada isyeri alani secili isyeriyle dolu");
        await Ekran("2-fatura-isyeri-alani");
    }
}
