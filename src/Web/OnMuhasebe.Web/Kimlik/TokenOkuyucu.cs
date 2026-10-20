using System.Buffers.Text;
using System.Security.Claims;
using System.Text.Json;

namespace OnMuhasebe.Web.Kimlik;

// Token yalnızca arayüzde göstermek için okunur; imza doğrulaması ve asıl yetki kontrolü API'dedir.
internal static class TokenOkuyucu
{
    public const string AdClaim = "name";
    public const string RolClaim = "rol";
    public const string ModulClaim = "modul";
    public const string FirmaClaim = "firma_id";

    public static ClaimsPrincipal Anonim { get; } = new(new ClaimsIdentity());

    public static (ClaimsPrincipal Kullanici, TimeSpan Omur) Oku(string token)
    {
        var yuk = JsonDocument.Parse(Base64Url.DecodeFromChars(token.Split('.')[1])).RootElement;
        var claims = new List<Claim>();

        foreach (var ozellik in yuk.EnumerateObject())
        {
            if (ozellik.Value.ValueKind == JsonValueKind.Array)
            {
                claims.AddRange(ozellik.Value.EnumerateArray().Select(d => new Claim(ozellik.Name, d.ToString())));
            }
            else
            {
                claims.Add(new Claim(ozellik.Name, ozellik.Value.ToString()));
            }
        }

        // Ömür tarayıcı saatinden bağımsız hesaplanır (exp - iat); istemci saati yanlış olsa da yenileme zamanında yapılır.
        var omur = TimeSpan.FromSeconds(yuk.GetProperty("exp").GetInt64() - yuk.GetProperty("iat").GetInt64());
        return (new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt", AdClaim, RolClaim)), omur);
    }
}
