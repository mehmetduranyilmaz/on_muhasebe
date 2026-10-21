using Microsoft.Extensions.Configuration;

namespace OnMuhasebe.Shared.Infrastructure;

// Web uygulamasının dış adresi (e-postalardaki bağlantılar). İstekteki Host/Origin başlığından türetilmez: sahte başlıkla
// şifre sıfırlama bağlantısı saldırganın sitesine yönlendirilebilirdi.
public static class UygulamaAdresi
{
    public const string AyarAdi = "Uygulama:WebAdresi";

    public static string Oku(IConfiguration configuration) =>
        configuration[AyarAdi] is { Length: > 0 } adres
            ? adres.TrimEnd('/') + "/"
            : throw new InvalidOperationException($"{AyarAdi} tanımlı değil (ör. https://muhasebe.firma.com).");
}
