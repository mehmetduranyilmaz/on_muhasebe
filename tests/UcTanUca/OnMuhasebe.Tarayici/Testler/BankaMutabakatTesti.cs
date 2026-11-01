using System.Net.Http.Headers;

namespace OnMuhasebe.Tarayici.Testler;

// Ekstrede açık fatura ve çek eşleşmesi önerisi, satır düzenleme, aktarım ve banka mutabakatı ekranı.
internal sealed class BankaMutabakatTesti : TarayiciTesti
{
    public override string Ad => "mutabakat";

    protected override async Task CalistirAsync()
    {
        await Komut("Page.enable");
        var eposta = $"mutabakat{DateTime.UtcNow.Ticks}@ornek.com";
        var kayit = await KayitOlAsync(new { firmaUnvani = "Mutabakat Ltd", ad = "Mert", soyad = "Banka", eposta, sifre = "Sifre1234" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", kayit["accessToken"]!.GetValue<string>());
        var anadolu = (await Post("api/cari/hesaplar", new { unvan = "Anadolu Gida Ltd", tur = "Musteri", kisiTipi = "Tuzel", paraBirimi = "TRY" }))["id"]!.GetValue<string>();
        var cekli = (await Post("api/cari/hesaplar", new { unvan = "Cekli Musteri", tur = "Musteri", kisiTipi = "Tuzel", paraBirimi = "TRY" }))["id"]!.GetValue<string>();
        var banka = (await Post("api/kasabanka/hesaplar", new { ad = "Ana Banka", tur = "Banka", paraBirimi = "TRY", iban = "TR330006100519786457841326" }))["id"]!.GetValue<string>();
        await Post("api/kasabanka/islemler/acilis", new { hesapId = banka, tarih = "2026-10-01", tutar = 10000 });
        await Post("api/fatura/faturalar", new
        {
            tur = "Satis", cariHesapId = anadolu, tarih = "2026-10-10", genelIskontoTuru = "Yuzde", genelIskonto = 0,
            satirlar = new[] { new { ad = "Hizmet", birim = "ADET", miktar = 1, birimFiyat = 1000, kdvOrani = 20, kdvDahilMi = false, iskonto1 = 0, iskonto2 = 0, iskonto3 = 0 } }
        });
        var giris = await Post("api/kasabanka/cek-senet/giris", new
        {
            cariHesapId = cekli, tarih = "2026-10-05", riskOnaylandi = true,
            evraklar = new[] { new { tur = "Cek", seriNo = "7001", vadeTarihi = "2026-10-20", tutar = 5000, paraBirimi = "TRY", borclu = new { ad = "Ahmet Yilmaz", kimlikNo = "10000000146" }, bankaAdi = "Ziraat", sube = "Merkez", hesapNo = "123" } }
        });
        await Post("api/kasabanka/cek-senet/islemler", new { islem = "TahsileVerme", tarih = "2026-10-12", evrakIdleri = new[] { giris["evrakIdleri"]![0]!.GetValue<string>() }, hesapId = banka });

        await GirisYap(eposta, "Sifre1234");
        await Git($"/kasabanka/{banka}");
        Kontrol(await Bekle($"{Govde("Ekstre içe aktar")} && {Govde("Mutabakat")}", 20), "Hesap sayfasinda mutabakat dugmesi");

        await Git($"/kasabanka/{banka}/ekstre-aktar");
        await Bekle(Govde("Ekstre dosyasını sürükleyin"), 20);
        await DosyaSec("eslesme.sta");
        Kontrol(await Bekle($"{Govde("Fatura tutarı")} && {Govde("Çek/senet no")}", 20), "Fatura ve cek onerileri gorundu");
        Kontrol(await Bekle($"{Govde("Fatura TPT")} && {Govde("No 7001")}", 5), "Satirda fatura ve cek numarasi");
        await Ekran("1-oneriler");

        await SatirTikla("TAKAS TAHSIL");
        Kontrol(await Bekle($"!!document.querySelector('.mud-dialog') && {Govde("Çek / senet")}", 10), "Duzenlemede cek secimi");
        await Ekran("2-cek-duzenle");
        await DiyalogDugmesi("Vazgeç");
        await Bekle("!document.querySelector('.mud-dialog')", 5);
        await SatirTikla("EFT GELEN");
        Kontrol(await Bekle($"!!document.querySelector('.mud-dialog') && {Govde("Kapatılacak fatura")}", 10), "Duzenlemede kapatilacak fatura");
        await Ekran("3-fatura-duzenle");
        await DiyalogDugmesi("Vazgeç");
        await Bekle("!document.querySelector('.mud-dialog')", 5);

        await Git($"/kasabanka/{banka}/mutabakat");
        Kontrol(await Bekle($"{Govde("eslesme.sta")} && {Govde("Ekstrede var, programda yok (2)")} && {Govde("6.200,00")}", 20), "Aktarim oncesi: dosya bakiyesi, fark 6.200 ve aktarilmamis satirlar");
        await Ekran("4-mutabakat-once");

        await Git($"/kasabanka/{banka}/ekstre-aktar");
        await Bekle(Govde("eslesme.sta"), 20);
        await SatirTikla("eslesme.sta");
        await Bekle(Govde("Onayla ve aktar"), 20);
        await Tikla("Onayla ve aktar");
        await Bekle("!!document.querySelector('.mud-dialog')", 10);
        await DiyalogDugmesi("Aktar");
        Kontrol(await Bekle(Govde("Ekstre aktarıldı"), 20), "Fatura ve cek satirlari aktarildi");

        await Git($"/kasabanka/{banka}/mutabakat");
        Kontrol(await Bekle(Govde("Mutabakat tamam"), 20), "Aktarim sonrasi mutabakat tamam");
        await Ekran("5-mutabakat-sonra");
    }
}
