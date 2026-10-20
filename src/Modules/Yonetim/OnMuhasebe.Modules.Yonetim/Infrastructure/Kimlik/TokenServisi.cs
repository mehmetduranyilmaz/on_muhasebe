using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OnMuhasebe.Modules.Yonetim.Domain;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Kimlik;

namespace OnMuhasebe.Modules.Yonetim.Infrastructure.Kimlik;

internal sealed class TokenServisi(IOptions<JwtAyarlari> ayarlar, TimeProvider zaman)
{
    private static readonly JsonWebTokenHandler _handler = new();

    public (string Token, DateTime BitisTarihi) AccessTokenOlustur(
        Kullanici kullanici, FirmaKullanicisi? uyelik, IReadOnlyList<string> moduller)
    {
        var ayar = ayarlar.Value;
        var simdi = zaman.GetUtcNow().UtcDateTime;
        var bitis = simdi.AddMinutes(ayar.AccessTokenDakika);

        List<Claim> claims =
        [
            new(ClaimTipleri.KullaniciId, kullanici.Id.ToString()),
            new(ClaimTipleri.Ad, $"{kullanici.Ad} {kullanici.Soyad}")
        ];

        if (uyelik is not null)
        {
            claims.Add(new(ClaimTipleri.FirmaId, uyelik.FirmaId.ToString()));
            claims.Add(new(ClaimTipleri.Rol, uyelik.Rol));
            claims.AddRange(moduller.Select(m => new Claim(ClaimTipleri.Modul, m)));
        }

        if (kullanici.SistemYoneticisiMi)
        {
            claims.Add(new(ClaimTipleri.Rol, Roller.SistemYoneticisi));
        }

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = ayar.Yayinci,
            Audience = ayar.Hedef,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = simdi,
            NotBefore = simdi,
            Expires = bitis,
            SigningCredentials = new SigningCredentials(ImzaAnahtari(ayar), SecurityAlgorithms.HmacSha256)
        });

        return (token, bitis);
    }

    public (string Token, RefreshToken Kayit) RefreshTokenOlustur(Guid kullaniciId, Guid? firmaId)
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(64));
        var bitis = zaman.GetUtcNow().UtcDateTime.AddDays(ayarlar.Value.RefreshTokenGun);
        return (token, RefreshToken.Olustur(kullaniciId, firmaId, Ozetle(token), bitis));
    }

    public static string Ozetle(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static SymmetricSecurityKey ImzaAnahtari(JwtAyarlari ayar) => new(Encoding.UTF8.GetBytes(ayar.Anahtar));
}
