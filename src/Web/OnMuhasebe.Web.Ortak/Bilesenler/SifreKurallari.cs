namespace OnMuhasebe.Web.Bilesenler;

// API'deki şifre kuralının aynısı; kullanıcı formu göndermeden hatayı görsün diye istemcide de uygulanır.
public static class SifreKurallari
{
    public static IEnumerable<string> Kontrol(string? sifre)
    {
        if (string.IsNullOrEmpty(sifre))
        {
            yield break;
        }

        if (sifre.Length < 8)
        {
            yield return "Şifre en az 8 karakter olmalıdır.";
        }

        if (!sifre.Any(char.IsLetter))
        {
            yield return "Şifre en az bir harf içermelidir.";
        }

        if (!sifre.Any(char.IsDigit))
        {
            yield return "Şifre en az bir rakam içermelidir.";
        }
    }
}
