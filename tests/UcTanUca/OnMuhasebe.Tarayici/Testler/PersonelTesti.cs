using System.Net.Http.Headers;

namespace OnMuhasebe.Tarayici.Testler;

// Personel: kart, maaş dönemi, avans düşümü, onay, ödeme, masraf onayı, kendi masrafı, zimmet ve işten çıkış uyarısı.
internal sealed class PersonelTesti : TarayiciTesti
{
    private static readonly string[] CalisanYetkileri = ["personel.kendi.goruntule", "personel.kendi.ekle"];

    public override string Ad => "personel";

    protected override async Task CalistirAsync()
    {
        await Komut("Page.enable");
        var damga = DateTime.UtcNow.Ticks;
        var eposta = $"personelarayuz{damga}@ornek.com";
        var calisan = $"pcalisan{damga}@ornek.com";
        var kayit = await KayitOlAsync(new { firmaUnvani = "Personel Arayuz Ltd", ad = "Pelin", soyad = "Patron", eposta, sifre = "Sifre1234" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", kayit["accessToken"]!.GetValue<string>());
        var banka = (await Post("api/kasabanka/hesaplar", new { ad = "Ana Banka", tur = "Banka", paraBirimi = "TRY" }))["id"]!.GetValue<string>();
        await Post("api/kasabanka/islemler/acilis", new { hesapId = banka, tarih = "2026-01-01", tutar = 100000 });
        var rol = await Post("api/yonetim/roller", new { ad = "Calisan", yetkiler = CalisanYetkileri });
        await Post("api/yonetim/kullanicilar", new { eposta = calisan, rolId = rol["id"]!.GetValue<string>() });
        var kod = await MailpitKoduAsync(calisan, "davet");
        await Post("api/yonetim/davet-kabul", new { kod, ad = "Can", soyad = "Calisan", sifre = "Sifre1234" });

        await GirisYap(eposta, "Sifre1234");
        Kontrol(await Bekle(Govde("Personel"), 15), "Menude Personel var");
        await Git("/personel");
        Kontrol(await Bekle($"{Govde("Yeni Personel")} && {Govde("Personel yok")}", 20), "Bos personel listesi");
        await Tikla("Yeni Personel");
        await Bekle($"!!document.querySelector('.mud-dialog') && {Govde("Net maaş")}", 10);
        await Doldur("Ad", "Ayse");
        await Doldur("Soyad", "Kaya");
        await Doldur("Görev", "Muhasebeci");
        await Doldur("TC kimlik", "10000000146");
        await Doldur("Net maaş", "20000");
        await Sec("Program kullanıcısı", "Can Calisan");
        await Task.Delay(400);
        await Ekran("1-yeni-personel");
        await DiyalogDugmesi("Kaydet");
        Kontrol(await Bekle($"/\\/personel\\/[0-9a-f-]{{36}}/.test(location.pathname) && {Govde("Ayse Kaya")} && {Govde("20.000,00")}", 20), "Personel karti acildi, maas gorunuyor");

        await Tikla("Avans Ver");
        await Bekle($"!!document.querySelector('.mud-dialog') && {Govde("Taksit")}", 10);
        await Doldur("Tutar", "2000");
        await Doldur("Taksit", "2");
        await Sec("Kasa / banka hesabı", "Ana Banka");
        await Task.Delay(300);
        await DiyalogDugmesi("Avans Kaydet");
        Kontrol(await Bekle($"!document.querySelector('.mud-dialog') && {Govde("Avanslar")} && {Govde("2.000,00")}", 15), "Avans verildi");

        await Git("/personel/maas");
        await Bekle(Govde("Dönem Aç"), 20);
        await Tikla("Dönem Aç");
        Kontrol(await Bekle($"location.pathname.startsWith('/personel/maas/') && {Govde("Ayse Kaya")} && {Govde("19.000,00")}", 20), "Donem acildi: 20000 - 1000 avans taksiti");
        await Ekran("2-maas-donemi");
        await Tikla("Onayla");
        Kontrol(await Bekle($"{Govde("Banka Dosyası")} && {Govde("Tümünü Öde")}", 15), "Donem onaylandi");
        await Tikla("Tümünü Öde");
        await Bekle($"!!document.querySelector('.mud-dialog') && {Govde("Kasa / banka hesabı")}", 10);
        await Sec("Kasa / banka hesabı", "Ana Banka");
        await Task.Delay(300);
        await DiyalogDugmesi("Ödemeyi Kaydet");
        Kontrol(await Bekle($"!document.querySelector('.mud-dialog') && !{Govde("Tümünü Öde")}", 15), "Maas odendi");
        await Ekran("3-odendi");

        await Git("/personel/zimmetler");
        await Bekle(Govde("Yeni Zimmet"), 20);
        await Tikla("Yeni Zimmet");
        await Bekle($"!!document.querySelector('.mud-dialog') && {Govde("Eşya")}", 10);
        Kontrol(await Otomatik(Alan("Personel"), "Ayse", "Ayse Kaya"), "Zimmette personel secildi");
        await Doldur("Eşya", "Dizustu bilgisayar");
        await Task.Delay(300);
        await DiyalogDugmesi("Kaydet");
        Kontrol(await Bekle($"!document.querySelector('.mud-dialog') && {Govde("Dizustu bilgisayar")} && {Govde("Zimmette")}", 15), "Zimmet verildi");

        await GirisYap(calisan, "Sifre1234");
        Kontrol(await Bekle(Govde("Masraflarım"), 15), "Calisanda Masraflarim menusu var");
        await Git("/masraflarim");
        Kontrol(await Bekle($"{Govde("Masraf Ekle")} && {Govde("Ayse Kaya")}", 20), "Calisan kendi sayfasini goruyor");
        await Tikla("Masraf Ekle");
        await Bekle($"!!document.querySelector('.mud-dialog') && {Govde("Kategori")}", 10);
        await Doldur("Tutar", "450");
        await Doldur("Açıklama", "Musteri ziyareti");
        await Task.Delay(300);
        await DiyalogDugmesi("Kaydet");
        Kontrol(await Bekle($"!document.querySelector('.mud-dialog') && {Govde("Musteri ziyareti")} && {Govde("Onay bekliyor")}", 15), "Calisan masraf girdi, onay bekliyor");
        await SatirTikla("Musteri ziyareti");
        await Bekle("!!document.querySelector('.mud-dialog')", 10);
        Kontrol(!await Bekle(Govde("Karar notu"), 2), "Kendi masrafinda onay alani yok");
        await DiyalogDugmesi("Kapat");

        await GirisYap(eposta, "Sifre1234");
        await Git("/personel/masraflar");
        await Bekle(Govde("Musteri ziyareti"), 20);
        await SatirTikla("Musteri ziyareti");
        Kontrol(await Bekle($"!!document.querySelector('.mud-dialog') && {Govde("Karar notu")}", 10), "Yonetici onay alanini goruyor");
        await Task.Delay(300);
        await DiyalogDugmesi("Onayla");
        Kontrol(await Bekle($"!document.querySelector('.mud-dialog') && {Govde("Onaylandı")}", 15), "Masraf onaylandi");
        await SatirTikla("Musteri ziyareti");
        await Bekle("!!document.querySelector('.mud-dialog')", 10);
        await Task.Delay(300);
        await DiyalogDugmesi("Öde");
        await Bekle($"{Govde("Masraf Ödemesi")}", 10);
        await Sec("Kasa / banka hesabı", "Ana Banka");
        await Task.Delay(300);
        await DiyalogDugmesi("Ödemeyi Kaydet");
        Kontrol(await Bekle($"!document.querySelector('.mud-dialog') && {Govde("Ödendi")}", 15), "Masraf odendi");
        await Ekran("4-masraflar");

        await Git("/personel");
        await Bekle(Govde("Ayse Kaya"), 20);
        await SatirTikla("Ayse Kaya");
        await Bekle(Govde("Personel hesabı"), 20);
        await Ekran("5-kart");
        await Tikla("İşten Çıkış");
        await Bekle("!!document.querySelector('.mud-dialog')", 10);
        await Task.Delay(300);
        await DiyalogDugmesi("İşten çıkış Kaydet");
        Kontrol(await Bekle(Govde("üzerinde 1 zimmet var"), 15), "Isten cikista zimmet uyarisi");
    }
}
