namespace OnMuhasebe.Tarayici.Testler;

// Dosya eki: cari kartından pencere açılır, dosya yüklenir, gerçek indirme akışı çalışır, silinir, geri alınır; abonelikte kullanım görünür.
internal sealed class DosyaTesti : TarayiciTesti
{
    public override string Ad => "dosya";

    protected override async Task CalistirAsync()
    {
        await Komut("Page.enable");
        var eposta = $"dosyaarayuz{DateTime.UtcNow.Ticks}@ornek.com";
        await KayitOlAsync(new { firmaUnvani = "Dosya Arayuz Ltd", ad = "Defne", soyad = "Yon", eposta, sifre = "Sifre1234" });
        await GirisYap(eposta, "Sifre1234");

        await Git("/cari/yeni");
        await Bekle("document.querySelectorAll('input').length > 3", 20);
        await Doldur("Ünvan", "Dosyali Cari");
        await Task.Delay(300);
        await Kaydet();
        Kontrol(await Bekle("!!document.querySelector('.dosyalar-dugmesi')", 20) || await KartaGitAsync(), "Cari kartinda dosya dugmesi");

        await Js("document.querySelector('.dosyalar-dugmesi').click()");
        Kontrol(await Bekle($"!!document.querySelector('.mud-dialog') && {Govde("Henüz dosya eklenmemiş")} && {Govde("Depolama:")}", 15), "Dosya penceresi bos ve kullanim gorunuyor");

        await DosyaSec("ornek.pdf");
        Kontrol(await Bekle($"{Govde("ornek.pdf")} && {Govde("1 dosya eklendi.")}", 20), "Dosya yuklendi, listede");
        await Ekran("1-dosya-listesi");

        await Js("""
            window.__dosyalar = [];
            const nesneUrl = URL.createObjectURL;
            URL.createObjectURL = b => { window.__boyut = b.size; return nesneUrl.call(URL, b); };
            HTMLAnchorElement.prototype.click = function () { if (this.download) window.__dosyalar.push(this.download + '|' + window.__boyut); };
            document.querySelector('.dosya-listesi button[aria-label="ornek.pdf indir"]').click();
            """);
        Kontrol(await Bekle("window.__dosyalar.some(d => /^ornek\\.pdf\\|[1-9]/.test(d))", 15), "Dosya indirildi");

        await Js("document.querySelector('.dosya-listesi button[aria-label=\"ornek.pdf sil\"]').click()");
        await Bekle(Govde("Dosya silinsin mi?"), 5);
        await Js("[...document.querySelectorAll('.mud-dialog button')].filter(b => b.textContent.trim() === 'Sil').pop()?.click()");
        Kontrol(await Bekle(Govde("Henüz dosya eklenmemiş"), 10), "Dosya silindi");

        await Js("[...document.querySelectorAll('.mud-dialog .mud-switch input')].pop().click()");
        Kontrol(await Bekle($"{Govde("ornek.pdf")} && {Govde("Geri al")}", 10), "Silinenlerde gorunuyor");
        await Tikla("Geri al");
        Kontrol(await Bekle(Govde("Dosya geri alındı."), 10), "Dosya geri alindi");

        await Git("/abonelik");
        Kontrol(await Bekle($"{Govde("Depolama:")} && {Govde("500 MB")}", 20), "Abonelikte depolama kullanimi ve kota");
        await Ekran("2-abonelik");
    }

    // Kayıttan sonra form sayfası listeye dönerse kart satırdan açılır.
    private async Task<bool> KartaGitAsync()
    {
        await Git("/cari");
        await Bekle(Govde("Dosyali Cari"), 15);
        await SatirTikla("Dosyali Cari");
        return await Bekle("!!document.querySelector('.dosyalar-dugmesi')", 15);
    }
}
