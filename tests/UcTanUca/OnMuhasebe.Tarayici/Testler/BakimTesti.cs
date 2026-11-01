using System.Net.Http.Headers;

namespace OnMuhasebe.Tarayici.Testler;

// Bakım Takibi: ekipman, plan, yaklaşan paneli, arıza iş emri, stoktan parça, kapanışta stok düşmesi, maliyet raporu.
internal sealed class BakimTesti : TarayiciTesti
{
    public override string Ad => "bakim";

    protected override async Task CalistirAsync()
    {
        await Komut("Page.enable");
        var eposta = $"bakimarayuz{DateTime.UtcNow.Ticks}@ornek.com";
        var kayit = await KayitOlAsync(new { firmaUnvani = "Bakim Arayuz Ltd", ad = "Beren", soyad = "Yon", eposta, sifre = "Sifre1234" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", kayit["accessToken"]!.GetValue<string>());
        var kart = await Post("api/stok/kartlar", new { ad = "Hidrolik Filtre", tur = "Urun", temelBirim = "ADET", kdvOrani = 20, paraBirimi = "TRY" });
        await Post($"api/stok/kartlar/{kart["id"]!.GetValue<string>()}/hareketler", new { tur = "Acilis", tarih = "2026-01-01", birim = "ADET", miktar = 10, birimFiyat = 100 });

        await GirisYap(eposta, "Sifre1234");
        Kontrol(await Bekle(Govde("Bakım Takibi"), 15), "Menude Bakim Takibi var");

        await Git("/bakim");
        Kontrol(await Bekle($"{Govde("Yeni Ekipman")} && {Govde("Ekipman yok")}", 20), "Bos ekipman listesi");
        await Tikla("Yeni Ekipman");
        await Bekle($"!!document.querySelector('.mud-dialog') && {Govde("Ekipman adı")}", 10);
        await Doldur("Ekipman adı", "Hidrolik Pres");
        await Doldur("Konum", "Atolye");
        await Task.Delay(400);
        await Ekran("1-yeni-ekipman");
        await DiyalogDugmesi("Kaydet");
        Kontrol(await Bekle($"location.pathname.startsWith('/bakim/') && {Govde("Hidrolik Pres")} && {Govde("Plan yok")}", 20), "Ekipman karti acildi");

        await Tikla("Yeni Plan");
        await Bekle($"!!document.querySelector('.mud-dialog') && {Govde("Plan adı")}", 10);
        await Doldur("Plan adı", "Aylik yaglama");
        await Doldur("Periyot (gün)", "30");
        await Doldur("Sonraki tarih", DateTime.Today.AddDays(3).ToString("dd.MM.yyyy"));
        await Task.Delay(400);
        await DiyalogDugmesi("Kaydet");
        Kontrol(await Bekle($"!document.querySelector('.mud-dialog') && {Govde("Aylik yaglama")} && {Govde("Yaklaşıyor")}", 15), "Plan eklendi, yaklasiyor");

        await Git("/bakim");
        Kontrol(await Bekle($"{Govde("Yaklaşan ve geciken bakımlar")} && {Govde("Aylik yaglama")}", 20), "Listede yaklasanlar paneli");
        await Ekran("2-liste");
        await SatirTikla("Hidrolik Pres");
        await Bekle($"location.pathname.startsWith('/bakim/') && {Govde("Arıza Bildir")}", 20);

        await Tikla("Arıza Bildir");
        await Bekle($"!!document.querySelector('.mud-dialog') && {Govde("Yedek parçalar")}", 10);
        await Doldur("Başlık", "Hidrolik kacak");
        await Doldur("İşçilik", "500");
        await Otomatik(Alan("Stoktan parça ekle"), "Hidrolik", "Hidrolik Filtre");
        Kontrol(await Bekle($"{Govde("Hidrolik Filtre")} && {Govde("(stoktan)")}", 10), "Stoktan parca eklendi");
        await Task.Delay(400);
        await Ekran("3-is-emri");
        await DiyalogDugmesi("Kaydet");
        Kontrol(await Bekle($"!document.querySelector('.mud-dialog') && {Govde("Hidrolik kacak")} && {Govde("Arızalı")}", 15), "Ariza is emri acildi, ekipman arizali");

        await Js("[...document.querySelectorAll('.is-emirleri tbody tr')].find(t => t.textContent.includes('Hidrolik kacak'))?.click()");
        await Bekle($"!!document.querySelector('.mud-dialog') && {Govde("İşi kapat")}", 15);
        await Task.Delay(400);
        await Js("[...document.querySelectorAll('.mud-dialog .panel-kart button')].find(b => b.textContent.trim() === 'Kapat').click()");
        Kontrol(await Bekle($"!document.querySelector('.mud-dialog') && {Govde("Kapandı")} && {Govde("Çalışıyor")}", 20), "Is emri kapandi, ekipman calisiyor");
        await Ekran("4-kapandi");

        await Git("/stok");
        Kontrol(await Bekle($"[...document.querySelectorAll('tr')].some(t => t.textContent.includes('Hidrolik Filtre') && t.textContent.includes('9'))", 20), "Kapanista stok 10dan 9a dustu");

        await Git("/bakim/rapor");
        Kontrol(await Bekle($"{Govde("Bakım Maliyet ve Duruş Raporu")} && {Govde("Hidrolik Pres")} && {Govde("600,00")}", 20), "Raporda parca ve iscilik toplami");
        await Ekran("5-rapor");
    }
}
