using System.ComponentModel.DataAnnotations;

namespace OnMuhasebe.Modules.Yonetim.Infrastructure.Kimlik;

internal sealed class JwtAyarlari
{
    public const string Bolum = "Jwt";

    [Required]
    public string Yayinci { get; init; } = null!;

    [Required]
    public string Hedef { get; init; } = null!;

    // HMAC-SHA256 için en az 256 bit. Repoya yazılmaz: geliştirmede user-secrets, canlıda ortam değişkeni.
    [Required, MinLength(32)]
    public string Anahtar { get; init; } = null!;

    [Range(1, 60)]
    public int AccessTokenDakika { get; init; } = 15;

    [Range(1, 90)]
    public int RefreshTokenGun { get; init; } = 14;
}
