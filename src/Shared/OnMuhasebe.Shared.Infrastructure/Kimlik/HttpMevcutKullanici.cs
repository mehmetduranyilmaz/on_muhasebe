using Microsoft.AspNetCore.Http;
using OnMuhasebe.Shared.Contracts.Kimlik;

namespace OnMuhasebe.Shared.Infrastructure.Kimlik;

internal sealed class HttpMevcutKullanici(IHttpContextAccessor httpContextAccessor) : IMevcutKullanici
{
    public Guid? KullaniciId => ClaimGuid(ClaimTipleri.KullaniciId);

    public Guid? FirmaId => ClaimGuid(ClaimTipleri.FirmaId);

    public string? KullaniciAdi => httpContextAccessor.HttpContext?.User.FindFirst(ClaimTipleri.Ad)?.Value;

    public bool SistemYoneticisiMi =>
        httpContextAccessor.HttpContext?.User.HasClaim(ClaimTipleri.Rol, Roller.SistemYoneticisi) ?? false;

    private Guid? ClaimGuid(string claimTipi)
    {
        var deger = httpContextAccessor.HttpContext?.User.FindFirst(claimTipi)?.Value;
        return Guid.TryParse(deger, out var id) ? id : null;
    }
}
