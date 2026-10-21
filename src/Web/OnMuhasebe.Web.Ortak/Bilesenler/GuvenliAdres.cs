namespace OnMuhasebe.Web.Bilesenler;

// Adres çubuğundan gelen dönüş adresleri (?donus=) yalnızca uygulama içi göreli yol olabilir: "https://kotu.site",
// "//kotu.site" ya da "javascript:" gibi değerlerle kullanıcı başka siteye (oltalama) yönlendirilemesin.
public static class GuvenliAdres
{
    public static string? UygulamaIci(string? adres)
    {
        if (string.IsNullOrWhiteSpace(adres))
        {
            return null;
        }

        var yol = adres.Trim();
        return yol.StartsWith("//", StringComparison.Ordinal) || yol.StartsWith('\\') || yol.StartsWith(@"/\", StringComparison.Ordinal)
               || yol.Contains(':', StringComparison.Ordinal) || yol.Any(char.IsControl)
            ? null
            : yol.TrimStart('/');
    }
}
