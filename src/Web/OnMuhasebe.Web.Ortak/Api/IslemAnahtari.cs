namespace OnMuhasebe.Web.Api;

// Bir formun kayıt işleminin anahtarı. Form açıkken aynı kalır: cevabı kaybolan kayıt yeniden gönderilirse sunucu ikinci kez
// işlemez, ilk cevabı döner. Başarılı kayıttan sonra (aynı formda yeni kayıt kasıtlı olabilir) ya da sunucu anahtarın başka bir
// istekle kullanıldığını bildirince yenilenir.
public sealed class IslemAnahtari
{
    public string Deger { get; private set; } = Yeni();

    public void Sonuc(ApiHatasi? hata)
    {
        if (hata is null || hata.Kod == "tekrar_anahtari_uyusmuyor")
        {
            Deger = Yeni();
        }
    }

    private static string Yeni() => Guid.NewGuid().ToString("N");
}
