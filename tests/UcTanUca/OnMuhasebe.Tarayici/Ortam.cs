namespace OnMuhasebe.Tarayici;

// Test ortamı ortam değişkenlerinden okunur; varsayılanlar yerel geliştirme (dotnet run, docker compose) içindir.
internal static class Ortam
{
    public static string Web { get; } = Deger("OM_WEB", "http://localhost:5167").TrimEnd('/');
    public static string Api { get; } = Deger("OM_API", "http://localhost:5128").TrimEnd('/') + "/";
    public static string SistemEposta { get; } = Deger("OM_SISTEM_EPOSTA", "sistem@onmuhasebe.local");

    public static string SistemSifre => Environment.GetEnvironmentVariable("OM_SISTEM_SIFRE")
        ?? throw new InvalidOperationException("OM_SISTEM_SIFRE ortam değişkeni tanımlı değil (API'deki SistemYoneticisi:Sifre).");

    public static string Dosyalar { get; } = Deger("OM_DOSYALAR", Path.Combine(Kok, "Dosyalar"));
    public static string Cikti { get; } = Deger("OM_CIKTI", Path.Combine(Kok, "cikti"));
    public static string Chrome { get; } = Deger("OM_CHROME", ChromeBul());
    public static double BeklemeCarpani { get; } = double.Parse(Deger("OM_BEKLEME_CARPANI", "1"), System.Globalization.CultureInfo.InvariantCulture);

    // tests/UcTanUca klasörü: çalışan derlemenin üst klasörlerinde Dosyalar klasörünü içeren ilk yer.
    private static string Kok
    {
        get
        {
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            {
                if (Directory.Exists(Path.Combine(d.FullName, "Dosyalar")) && Directory.Exists(Path.Combine(d.FullName, "Senaryolar")))
                {
                    return d.FullName;
                }
            }

            return Directory.GetCurrentDirectory();
        }
    }

    private static string Deger(string ad, string varsayilan) =>
        Environment.GetEnvironmentVariable(ad) is { Length: > 0 } d ? d : varsayilan;

    private static string ChromeBul()
    {
        string[] adaylar = OperatingSystem.IsWindows()
            ? [@"C:\Program Files\Google\Chrome\Application\chrome.exe", @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"]
            : ["/usr/bin/google-chrome", "/usr/bin/google-chrome-stable", "/usr/bin/chromium", "/usr/bin/chromium-browser"];
        return adaylar.FirstOrDefault(File.Exists) ?? throw new InvalidOperationException("Chrome bulunamadı; OM_CHROME ile yolunu verin.");
    }
}
