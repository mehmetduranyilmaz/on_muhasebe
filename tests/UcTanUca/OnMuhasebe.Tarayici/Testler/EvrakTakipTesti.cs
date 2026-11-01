namespace OnMuhasebe.Tarayici.Testler;

// Evrak Takibi: menü, yeni evrak, süre durumu, yenileme ve geçmişi, pasifleştirme, dosya düğmesi.
internal sealed class EvrakTakipTesti : TarayiciTesti
{
    public override string Ad => "evraktakip";

    protected override async Task CalistirAsync()
    {
        await Komut("Page.enable");
        var eposta = $"evraktakiparayuz{DateTime.UtcNow.Ticks}@ornek.com";
        await KayitOlAsync(new { firmaUnvani = "Evrak Arayuz Ltd", ad = "Eda", soyad = "Yon", eposta, sifre = "Sifre1234" });
        await GirisYap(eposta, "Sifre1234");
        Kontrol(await Bekle(Govde("Evrak Takibi"), 15), "Menude Evrak Takibi var");

        await Git("/evrak-takibi");
        Kontrol(await Bekle($"{Govde("Yeni Evrak")} && {Govde("Süresi dolan")} && {Govde("Evrak yok")}", 20), "Bos sayfa ve ozet kartlari");

        await Tikla("Yeni Evrak");
        await Bekle("!!document.querySelector('.mud-dialog')", 10);
        await Bekle(Govde("Evrak adı"), 10);
        await Doldur("Evrak adı", "Trafik Sigortasi");
        await Doldur("Belge / poliçe no", "P-100");
        var bitis = DateTime.Today.AddDays(10).ToString("dd.MM.yyyy");
        await Doldur("Bitiş tarihi", bitis);
        await Task.Delay(400);
        await Ekran("1-yeni-evrak");
        await DiyalogDugmesi("Kaydet");
        Kontrol(await Bekle($"!document.querySelector('.mud-dialog') && {Govde("Trafik Sigortasi")} && {Govde("Süresi yaklaşıyor")} && {Govde("10 gün")}", 15), "Evrak eklendi, suresi yaklasiyor");
        await Ekran("2-liste");

        await SatirTikla("Trafik Sigortasi");
        await Bekle($"!!document.querySelector('.mud-dialog') && {Govde("Yeni bitiş")}", 15);
        await Doldur("Yeni bitiş", DateTime.Today.AddDays(375).ToString("dd.MM.yyyy"));
        await Doldur("Yeni belge no", "P-200");
        await Task.Delay(400);
        await DiyalogDugmesi("Yenile");
        Kontrol(await Bekle($"!document.querySelector('.mud-dialog') && {Govde("P-200")} && {Govde("Geçerli")}", 15), "Yenilendi, gecerli oldu");

        await SatirTikla("Trafik Sigortasi");
        Kontrol(await Bekle($"{Govde("Yenileme geçmişi")} && {Govde("P-100")} && {Govde("Eda Yon")}", 15), "Yenileme gecmisi gorunuyor");
        Kontrol(await Bekle("!!document.querySelector('.mud-dialog .dosyalar-dugmesi')", 5), "Evrak penceresinde dosya dugmesi");
        await Ekran("3-evrak-detay");
        await DiyalogDugmesi("Pasifleştir");
        Kontrol(await Bekle($"!document.querySelector('.mud-dialog') && {Govde("Evrak yok")}", 15), "Pasiflestirilen aktif listeden cikti");

        await Js("[...document.querySelectorAll('.tur-sekmesi')].find(b => b.textContent.trim() === 'Pasif').click()");
        Kontrol(await Bekle(Govde("Trafik Sigortasi"), 10), "Pasif sekmesinde gorunuyor");
        await Ekran("4-pasif");
    }
}
