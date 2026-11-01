using System.Net.Http.Headers;
using System.Text;

namespace OnMuhasebe.Tarayici.Testler;

// Gelen fatura: XML önizleme, cari kartı açma, taslak fatura, mükerrer uyarısı, PDF içindeki karekod.
internal sealed class BelgeOkumaTesti : TarayiciTesti
{
    public override string Ad => "belge_okuma";

    protected override async Task CalistirAsync()
    {
        await Komut("Page.enable");
        var eposta = $"belgearayuz{DateTime.UtcNow.Ticks}@ornek.com";
        var kayit = await KayitOlAsync(new { firmaUnvani = "Belge Arayuz Ltd", ad = "Bora", soyad = "Okur", eposta, sifre = "Sifre1234" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", kayit["accessToken"]!.GetValue<string>());
        await Post("api/stok/kartlar", new { ad = "Zeytinyagi", kod = "ZY-1", tur = "Urun", temelBirim = "KG", kdvOrani = 20, paraBirimi = "TRY" });

        await GirisYap(eposta, "Sifre1234");
        await Git("/fatura");
        await Bekle(Govde("Belgeden Oluştur"), 20);
        await Tikla("Belgeden Oluştur");
        Kontrol(await Bekle("!!document.querySelector('.mud-dialog input[type=file]')", 10), "Belge okuma penceresi acildi");

        await DosyaSec("efatura.xml");
        Kontrol(await Bekle($"{Govde("ABC2026000000042")} && {Govde("Tedarikci Gida Ltd")} && {Govde("1 tanesi stok kartına bağlandı")}", 20), "XML onizlemesi, stok eslesti");
        Kontrol(await Bekle("(" + Alan("Yeni cari unvanı") + ")?.value === 'Tedarikci Gida Ltd'", 10), "Yeni cari unvani belgeden geldi");
        await Ekran("1-onizleme");
        await Tikla("Cari kartı aç");
        Kontrol(await Bekle("(" + Alan("Cari") + ")?.value.includes('Tedarikci Gida Ltd')", 10), "Cari karti acildi ve secildi");
        await Task.Delay(300);
        await DiyalogDugmesi("Taslak Fatura Aç");
        Kontrol(await Bekle($"location.pathname.startsWith('/fatura/') && {Govde("ABC2026000000042")} && {Govde("Zeytinyagi")}", 20), "Taslak fatura acildi");
        await Ekran("2-taslak");

        await Git("/fatura");
        await Bekle(Govde("Belgeden Oluştur"), 20);
        await Tikla("Belgeden Oluştur");
        await Bekle("!!document.querySelector('.mud-dialog input[type=file]')", 10);
        await DosyaSec("efatura.xml");
        Kontrol(await Bekle(Govde("Bu fatura zaten kayıtlı"), 20), "Ayni fatura mukerrer uyarisi");
        await DiyalogDugmesi("Başka dosya");
        await Bekle("!!document.querySelector('.mud-dialog input[type=file]')", 10);

        var pdf = Path.Combine(Path.GetTempPath(), $"karekod-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(pdf, KarekodluPdf("""{"vkntckn":"1234567890","tip":"SATIS","tarih":"2026-10-20","no":"ABC2026000000043","ettn":"aa11","parabirimi":"TRY","malhizmettoplam":"1000","kdvmatrah(20)":"1000","vergidahil":"1200","odenecek":"1200"}"""));
        try
        {
            await DosyaSec(pdf);
            Kontrol(await Bekle($"{Govde("ABC2026000000043")} && {Govde("Karekod")} && {Govde("fatura toplamı")}", 40), "PDF icindeki karekod okundu");
            await Ekran("3-karekod");
            await Task.Delay(300);
            await DiyalogDugmesi("Taslak Fatura Aç");
            Kontrol(await Bekle($"location.pathname.startsWith('/fatura/') && {Govde("ABC2026000000043")} && {Govde("1.200,00")}", 20), "Karekoddan taslak acildi, toplam dogru");
        }
        finally
        {
            File.Delete(pdf);
        }
    }

    // Tek sayfalık PDF: A4 üzerinde gri tonlu karekod resmi.
    private static byte[] KarekodluPdf(string metin)
    {
        var matris = new ZXing.QrCode.QRCodeWriter().encode(metin, ZXing.BarcodeFormat.QR_CODE, 400, 400);
        var (en, boy) = (matris.Width, matris.Height);
        var resim = new byte[en * boy];
        for (var y = 0; y < boy; y++)
        {
            for (var x = 0; x < en; x++)
            {
                resim[y * en + x] = matris[x, y] ? (byte)0 : (byte)255;
            }
        }

        using var akis = new MemoryStream();
        var konumlar = new List<long>();
        void Yaz(string s) => akis.Write(Encoding.ASCII.GetBytes(s));
        void Nesne(string s)
        {
            konumlar.Add(akis.Position);
            Yaz(s);
        }

        Yaz("%PDF-1.4\n");
        Nesne("1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n");
        Nesne("2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj\n");
        Nesne("3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 595 842]/Resources<</XObject<</Im0 5 0 R>>>>/Contents 4 0 R>>endobj\n");
        var icerik = "q 150 0 0 150 400 650 cm /Im0 Do Q";
        Nesne($"4 0 obj<</Length {icerik.Length}>>stream\n{icerik}\nendstream endobj\n");
        konumlar.Add(akis.Position);
        Yaz($"5 0 obj<</Type/XObject/Subtype/Image/Width {en}/Height {boy}/ColorSpace/DeviceGray/BitsPerComponent 8/Length {resim.Length}>>stream\n");
        akis.Write(resim);
        Yaz("\nendstream endobj\n");
        var xref = akis.Position;
        Yaz($"xref\n0 {konumlar.Count + 1}\n0000000000 65535 f \n");
        foreach (var k in konumlar)
        {
            Yaz($"{k:D10} 00000 n \n");
        }

        Yaz($"trailer<</Size {konumlar.Count + 1}/Root 1 0 R>>\nstartxref\n{xref}\n%%EOF");
        return akis.ToArray();
    }
}
