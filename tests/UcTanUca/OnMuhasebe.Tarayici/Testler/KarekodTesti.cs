using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace OnMuhasebe.Tarayici.Testler;

// Çek karekodu okutmayı fotoğrafla (galeri) dener. Görüntüler Data Matrix olarak üretilir, PNG ve sahte kamera videosu ffmpeg ile yapılır.
// İçerik ayraçlı örnek biçimdedir: banka kodu | şube | hesap | seri | VKN (gerçek format netleşince güncellenecek).
internal class KarekodTesti : TarayiciTesti
{
    private const string Vkn = "9876543217";
    private const string KarekodMetni = $"0064|1234|0012345|7654321|{Vkn}";
    private const string IkinciMetin = $"0062|0335|6299431|1111111|{Vkn}";

    public override string Ad => "karekod";

    // Kamera modunda Chrome'un sahte kamerası ikinci çeki (ortada karekod) gösterir; yoksa varsayılan (kodsuz) test deseni.
    protected virtual bool KameraModu => false;

    private static string Klasor => Path.Combine(Ortam.Cikti, "karekod-goruntu");
    private static string Fotograf => Path.Combine(Klasor, "cek1.png");
    private static string KameraVideo => Path.Combine(Klasor, "kamera.y4m");

    protected override string ChromeArgumanlari =>
        "--use-fake-ui-for-media-stream --use-fake-device-for-media-stream " + (KameraModu ? $"--use-file-for-fake-video-capture=\"{KameraVideo}\" " : "");

    protected override async Task HazirlaAsync()
    {
        Directory.CreateDirectory(Klasor);
        var pgm1 = Uret("cek1", KarekodMetni, 8);
        var pgm2 = Uret("cek2", IkinciMetin, 10, ortada: true);
        await FfmpegAsync($"-i \"{pgm1}\" \"{Fotograf}\"");
        await FfmpegAsync($"-loop 1 -i \"{pgm2}\" -t 3 -r 10 -pix_fmt yuv420p \"{KameraVideo}\"");
    }

    private static string Uret(string ad, string metin, int olcek, bool ortada = false)
    {
        var matris = new ZXing.Datamatrix.DataMatrixWriter().encode(metin, ZXing.BarcodeFormat.DATA_MATRIX, 0, 0);
        const int en = 1280, boy = 720;
        var piksel = Enumerable.Repeat((byte)235, en * boy).ToArray();
        // Çek gövdesi: açık zemin üzerinde birkaç koyu "yazı" çizgisi, sağ altta karekod.
        for (var y = 120; y < 600; y += 60)
        {
            for (var x = 80; x < 760; x++)
            {
                piksel[y * en + x] = 90;
            }
        }

        // Fotoğrafta karekod çekin sağ alt köşesinde; kamerada kullanıcı onu çerçevenin ortasına getirir.
        var (ox, oy) = ortada
            ? ((en - matris.Width * olcek) / 2, (boy - matris.Height * olcek) / 2)
            : (en - 80 - matris.Width * olcek, boy - 80 - matris.Height * olcek);
        for (var y = -olcek * 2; y < (matris.Height + 2) * olcek; y++)
        {
            for (var x = -olcek * 2; x < (matris.Width + 2) * olcek; x++)
            {
                var (mx, my) = (Math.DivRem(x, olcek).Quotient, Math.DivRem(y, olcek).Quotient);
                var siyah = x >= 0 && y >= 0 && mx < matris.Width && my < matris.Height && matris[mx, my];
                piksel[(oy + y) * en + ox + x] = siyah ? (byte)15 : (byte)255;
            }
        }

        var pgm = Path.Combine(Klasor, ad + ".pgm");
        using (var akis = File.Create(pgm))
        {
            akis.Write(System.Text.Encoding.ASCII.GetBytes($"P5\n{en} {boy}\n255\n"));
            akis.Write(piksel);
        }

        return pgm;
    }

    private static async Task FfmpegAsync(string argumanlar)
    {
        using var p = Process.Start(new ProcessStartInfo("ffmpeg", "-y -loglevel error " + argumanlar) { UseShellExecute = false })!;
        await p.WaitForExitAsync();
        if (p.ExitCode != 0)
        {
            throw new InvalidOperationException("ffmpeg başarısız: " + argumanlar);
        }
    }

    protected override async Task CalistirAsync()
    {
        await Komut("Page.enable");
        var eposta = $"karekodui{DateTime.UtcNow.Ticks}@ornek.com";
        var kayit = await KayitOlAsync(new { firmaUnvani = "Karekod Arayuz", ad = "Ece", soyad = "Kar", eposta, sifre = "Sifre1234" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", kayit["accessToken"]!.GetValue<string>());
        var cariId = (await Post("api/cari/hesaplar", new { unvan = "Cekli Musteri", tur = "Musteri", kisiTipi = "Tuzel", paraBirimi = "TRY" }))["id"]!.GetValue<string>();

        await Git("/giris");
        await Bekle("document.querySelectorAll('input').length >= 2");
        await Doldur("E-posta", eposta);
        await Doldur("Şifre", "Sifre1234");
        await Tikla("Giriş Yap");
        await Bekle(Govde("Hoş"), 15);

        if (KameraModu)
        {
            await KameraAsync(cariId);
        }
        else
        {
            await FotografAsync(cariId);
        }
    }

    private async Task FotografSec(string yol)
    {
        var kok = await Komut("DOM.getDocument", new { depth = 0 });
        var dugum = await Komut("DOM.querySelector", new { nodeId = kok!["root"]!["nodeId"]!.GetValue<int>(), selector = ".karekod-dosya input[type=file]" });
        await Komut("DOM.setFileInputFiles", new { files = new[] { yol }, nodeId = dugum!["nodeId"]!.GetValue<int>() });
    }

    private async Task FotografAsync(string cariId)
    {
        await Git($"/cek-senet/giris?cari={cariId}");
        Kontrol(await Bekle("[...document.querySelectorAll('.evrak-karti button')].some(b => b.textContent.includes('Karekod okut'))", 15), "Alinan cek kartinda Karekod okut dugmesi");
        await Tikla("Karekod okut");
        Kontrol(await Bekle("!!document.querySelector('.karekod-alani')", 10), "Okuyucu acildi");
        Kontrol(await Bekle(Govde("Karekodu çerçevenin içine getirin"), 10), "Kamera acildi (kodsuz goruntu), yonlendirme metni");
        await Tikla("Fotoğraf / galeri");
        await Bekle("!!document.querySelector('.karekod-dosya input')", 5);
        await Ekran("1-okuyucu-fotograf");
        await FotografSec(Fotograf);
        Kontrol(await Bekle($"{Alan("Çek no")}?.value === '7654321'", 20), "Fotograftan seri no okundu (7654321)");
        Kontrol(await Js($"{Alan("Banka")}.value") == "Türkiye İş Bankası", "Banka kodundan banka adi (Turkiye Is Bankasi)");
        Kontrol(await Js($"{Alan("Şube")}.value") == "1234" && await Js($"{Alan("Hesap no")}.value") == "0012345", "Sube ve hesap no dolduruldu");
        Kontrol(await Js($"{Alan("VKN / TCKN")}?.value") == Vkn, "Kesideci VKN yeni kesideci alanina yazildi");
        Kontrol(await Bekle(Govde("Karekodlu"), 5), "Kartta Karekodlu isareti");
        await Doldur("Keşideci adı/unvanı", "Karekod Kesidecisi");
        await Doldur("Vade", "30.12.2026");
        await Doldur("Tutar", "4500");
        await Task.Delay(400);
        await Ekran("2-dolu-kart");
        await Tikla("Bordroyu kaydet");
        Kontrol(await Bekle("location.pathname.startsWith('/cek-senet/bordro/')", 15), "Karekodlu cek kaydedildi");

        // --- Aynı çek ikinci kez
        await Git($"/cek-senet/giris?cari={cariId}");
        await Bekle("[...document.querySelectorAll('.evrak-karti button')].some(b => b.textContent.includes('Karekod okut'))", 15);
        await Tikla("Karekod okut");
        await Bekle("!!document.querySelector('.karekod-alani')", 10);
        await Tikla("Fotoğraf / galeri");
        await Bekle("!!document.querySelector('.karekod-dosya input')", 5);
        await FotografSec(Fotograf);
        Kontrol(await Bekle(Govde("Bu çek zaten kayıtlı"), 20), "Ayni karekod ikinci kez: zaten kayitli uyarisi");
        Kontrol(await Js($"{Alan("Çek no")}.value") == "", "Mukerrer okutmada alanlar doldurulmadi");
    }

    private async Task KameraAsync(string cariId)
    {
        await Git($"/cek-senet/giris?cari={cariId}");
        await Bekle("[...document.querySelectorAll('.evrak-karti button')].some(b => b.textContent.includes('Karekod okut'))", 15);
        await Tikla("Karekod okut");
        Kontrol(await Bekle($"{Alan("Çek no")}?.value === '1111111'", 25), "Kameradan okundu, dialog kendiliginden kapandi (seri 1111111)");
        Kontrol(await Js($"{Alan("Banka")}.value") == "Türkiye Garanti Bankası", "Kamerayla okunan cekin bankasi Garanti");
        Kontrol(await Js("!!document.querySelector('.mud-dialog .karekod-alani')") == "false", "Okuyucu kapandi");
        await Ekran("3-kamera-sonrasi");
    }
}

// Aynı okuyucuyu Chrome'un sahte kamerasıyla dener (kamera ikinci çeki gösterir).
internal sealed class KarekodKameraTesti : KarekodTesti
{
    public override string Ad => "karekod-kamera";

    protected override bool KameraModu => true;
}
