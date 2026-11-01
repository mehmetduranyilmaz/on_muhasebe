namespace OnMuhasebe.Tarayici.Testler;

// Excel'den toplu aktarım: cari ve stok listesinden pencere açılır, dosya önizlenir, hatasız satırlar aktarılır, liste yenilenir.
internal sealed class AktarimTesti : TarayiciTesti
{
    public override string Ad => "aktarim";

    protected override async Task CalistirAsync()
    {
        await Komut("Page.enable");
        var eposta = $"aktarimarayuz{DateTime.UtcNow.Ticks}@ornek.com";
        await KayitOlAsync(new { firmaUnvani = "Aktarim Arayuz Ltd", ad = "Ayla", soyad = "Yon", eposta, sifre = "Sifre1234" });
        await GirisYap(eposta, "Sifre1234");

        await Git("/cari");
        Kontrol(await Bekle(Govde("Excel'den Aktar"), 20), "Cari listesinde aktarim dugmesi");
        await Tikla("Excel'den Aktar");
        Kontrol(await Bekle($"!!document.querySelector('.mud-dialog') && {Govde("Şablonu indir")}", 10), "Aktarim penceresi acildi");

        await Js("""
            window.__dosyalar = [];
            const nesneUrl = URL.createObjectURL;
            URL.createObjectURL = b => { window.__boyut = b.size; return nesneUrl.call(URL, b); };
            HTMLAnchorElement.prototype.click = function () { if (this.download) window.__dosyalar.push(this.download + '|' + window.__boyut); };
            """);
        await Tikla("Şablonu indir");
        Kontrol(await Bekle("window.__dosyalar.some(d => /cari_sablonu\\.xlsx\\|[1-9]/.test(d))", 10), "Sablon indirildi");

        await DosyaSec("cari_aktarim.xlsx");
        Kontrol(await Bekle($"{Govde("Eklenecek: 3")} && {Govde("Zaten var: 1")} && {Govde("Hatalı: 2")}", 20), "Onizleme sayilari");
        Kontrol(await Bekle($"{Govde("Bilinmeyen Sutun")} && {Govde("Hatali Cari")}", 5), "Taninmayan sutun ve hatali satir gorunuyor");
        await Ekran("1-cari-onizleme");

        await DiyalogDugmesi("3 kaydı aktar");
        Kontrol(await Bekle($"{Govde("3 kayıt aktarıldı.")} && {Govde("Eklendi: 3")}", 20), "Cariler aktarildi");
        await DiyalogDugmesi("Kapat");
        Kontrol(await Bekle($"!document.querySelector('.mud-dialog') && {Govde("Aktarim Musteri AS")} && {Govde("Euro Cari")}", 15), "Cari listesi yenilendi");
        await Ekran("2-cari-liste");

        await Git("/stok");
        Kontrol(await Bekle(Govde("Excel'den Aktar"), 20), "Stok listesinde aktarim dugmesi");
        await Tikla("Excel'den Aktar");
        await Bekle("!!document.querySelector('.mud-dialog')", 10);
        await DosyaSec("stok_aktarim.xlsx");
        Kontrol(await Bekle($"{Govde("Eklenecek: 3")} && {Govde("Hatalı: 4")} && {Govde("Yarim Kalem")}", 20), "Stok onizleme sayilari");
        await DiyalogDugmesi("3 kaydı aktar");
        Kontrol(await Bekle(Govde("3 kayıt aktarıldı."), 20), "Stok kartlari aktarildi");
        await DiyalogDugmesi("Kapat");
        Kontrol(await Bekle($"!document.querySelector('.mud-dialog') && {Govde("Aktarim Kalem")} && {Govde("Aktarim Hizmeti")}", 15), "Stok listesi yenilendi");
        await Ekran("3-stok-liste");
    }
}
