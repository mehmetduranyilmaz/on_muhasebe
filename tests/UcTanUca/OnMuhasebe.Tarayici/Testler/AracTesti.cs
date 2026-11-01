namespace OnMuhasebe.Tarayici.Testler;

// Araç Takibi: araç kartı, süre kaydı ve yaklaşanlar paneli, yenileme, MTV, yakıt masrafı ve km, hasar, maliyet raporu.
internal sealed class AracTesti : TarayiciTesti
{
    public override string Ad => "arac";

    private async Task Sekme(string ad) =>
        await Js($"[...document.querySelectorAll('.tur-sekmesi')].find(b => b.textContent.trim() === {S(ad)}).click()");

    private async Task KartDugmesi(string ad) =>
        await Js($"[...document.querySelectorAll('.panel-kart button')].find(b => b.textContent.trim() === {S(ad)}).click()");

    protected override async Task CalistirAsync()
    {
        await Komut("Page.enable");
        var eposta = $"aracarayuz{DateTime.UtcNow.Ticks}@ornek.com";
        await KayitOlAsync(new { firmaUnvani = "Arac Arayuz Ltd", ad = "Arda", soyad = "Yon", eposta, sifre = "Sifre1234" });
        await GirisYap(eposta, "Sifre1234");
        Kontrol(await Bekle(Govde("Araç Takibi"), 15), "Menude Arac Takibi var");

        await Git("/arac");
        Kontrol(await Bekle($"{Govde("Yeni Araç")} && {Govde("Araç yok")}", 20), "Bos arac listesi");
        await Tikla("Yeni Araç");
        await Bekle($"!!document.querySelector('.mud-dialog') && {Govde("Zimmetli sürücü")}", 10);
        await Doldur("Plaka", "34 abc 123");
        await Doldur("Marka", "Ford");
        await Doldur("Model", "Transit");
        await Doldur("Zimmetli sürücü", "Ali Veli");
        await Doldur("Güncel km", "10000");
        await Task.Delay(400);
        await Ekran("1-yeni-arac");
        await DiyalogDugmesi("Kaydet");
        Kontrol(await Bekle($"location.pathname.startsWith('/arac/') && {Govde("34ABC123")} && {Govde("Ford Transit")}", 20), "Arac karti acildi, plaka normallesti");

        await Tikla("Yeni Süre");
        await Bekle($"!!document.querySelector('.mud-dialog') && {Govde("Poliçe / belge no")}", 10);
        await Doldur("Bitiş tarihi", DateTime.Today.AddDays(10).ToString("dd.MM.yyyy"));
        await Doldur("Poliçe / belge no", "T-1");
        await Task.Delay(400);
        await DiyalogDugmesi("Kaydet");
        Kontrol(await Bekle($"!document.querySelector('.mud-dialog') && {Govde("T-1")} && {Govde("Yaklaşıyor")}", 15), "Trafik sigortasi yaklasiyor");
        await Ekran("2-sureler");

        await Git("/arac");
        Kontrol(await Bekle($"{Govde("Süresi yaklaşan ve dolan yükümlülükler")} && {Govde("Trafik sigortası")} && {Govde("10 gün")}", 20), "Listede yaklasanlar paneli");
        await Ekran("3-liste");
        await SatirTikla("34ABC123");
        await Bekle($"location.pathname.startsWith('/arac/') && {Govde("T-1")}", 20);

        await KartDugmesi("Yenile");
        await Bekle($"!!document.querySelector('.mud-dialog') && {Govde("Poliçe / belge no")}", 10);
        await Doldur("Bitiş tarihi", DateTime.Today.AddDays(375).ToString("dd.MM.yyyy"));
        await Doldur("Poliçe / belge no", "T-2");
        await Task.Delay(400);
        await DiyalogDugmesi("Kaydet");
        Kontrol(await Bekle($"!document.querySelector('.mud-dialog') && {Govde("T-2")} && {Govde("Yenilendi")} && {Govde("Geçerli")}", 15), "Yenilendi, eski kayit gecmiste");

        await Sekme("MTV");
        await Bekle(Govde("Yeni Taksit"), 10);
        await Tikla("Yeni Taksit");
        await Bekle($"!!document.querySelector('.mud-dialog') && {Govde("Son ödeme")}", 10);
        await Doldur("Tutar", "3000");
        await Task.Delay(400);
        await DiyalogDugmesi("Kaydet");
        Kontrol(await Bekle($"!document.querySelector('.mud-dialog') && {Govde("Ödenmedi")}", 15), "MTV taksiti eklendi");
        await KartDugmesi("Ödendi");
        Kontrol(await Bekle(Govde("Geri al"), 10), "MTV odendi isaretlendi");

        await Sekme("Yakıt ve masraf");
        Kontrol(await Bekle($"{Govde("Yeni Masraf")} && {Govde("muhasebeye yazılmaz")}", 10), "Masraf sekmesi takip kaydi notuyla");
        await Tikla("Yeni Masraf");
        await Bekle($"!!document.querySelector('.mud-dialog') && {Govde("Litre")}", 10);
        await Doldur("Tutar", "1000");
        await Doldur("Km", "12000");
        await Doldur("Litre", "50");
        await Task.Delay(400);
        await Ekran("4-masraf");
        await DiyalogDugmesi("Kaydet");
        Kontrol(await Bekle($"!document.querySelector('.mud-dialog') && {Govde("12.000 km")} && {Govde("Yakıt")}", 15), "Masraf eklendi, arac km'si ilerledi");

        await Sekme("Hasar");
        await Bekle(Govde("Yeni Hasar"), 10);
        await Tikla("Yeni Hasar");
        await Bekle($"!!document.querySelector('.mud-dialog') && {Govde("Hasar dosya no")}", 10);
        await Doldur("Onarım tutarı", "5000");
        await Doldur("Sigorta ödemesi", "4000");
        await Task.Delay(400);
        await DiyalogDugmesi("Kaydet");
        Kontrol(await Bekle($"!document.querySelector('.mud-dialog') && {Govde("5.000,00")}", 15), "Hasar eklendi");

        await Git("/arac/maliyet");
        Kontrol(await Bekle($"{Govde("Araç Maliyet Raporu")} && {Govde("34ABC123")} && {Govde("1.000,00")}", 20), "Maliyet raporu");
        await Ekran("5-maliyet");
    }
}
