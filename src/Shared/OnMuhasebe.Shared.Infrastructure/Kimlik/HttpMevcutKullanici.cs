using Microsoft.AspNetCore.Http;

namespace OnMuhasebe.Shared.Infrastructure.Kimlik;

internal sealed class HttpMevcutKullanici(IHttpContextAccessor httpContextAccessor) : IMevcutKullanici
{
    public Guid? KullaniciId => ClaimGuid(ClaimTipleri.KullaniciId);

    public Guid? FirmaId => ClaimGuid(ClaimTipleri.FirmaId);

    private Guid? ClaimGuid(string claimTipi)
    {
        var deger = httpContextAccessor.HttpContext?.User.FindFirst(claimTipi)?.Value;
        return Guid.TryParse(deger, out var id) ? id : null;
    }
}
