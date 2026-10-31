using Microsoft.AspNetCore.Http;
using OnMuhasebe.Shared.Contracts.Kimlik;

namespace OnMuhasebe.Shared.Infrastructure.Kimlik;

// Firma: kod bir firma kapsamı açtıysa (ArkaPlanBaglami) o, yoksa oturumdaki firma.
internal sealed class HttpMevcutKullanici(IHttpContextAccessor httpContextAccessor, ArkaPlanBaglami arkaPlan, IsyeriKapsami isyeriKapsami)
    : IMevcutKullanici
{
    public Guid? KullaniciId => ClaimGuid(ClaimTipleri.KullaniciId);

    public Guid? FirmaId => arkaPlan.FirmaId ?? ClaimGuid(ClaimTipleri.FirmaId);

    public string? KullaniciAdi => httpContextAccessor.HttpContext?.User.FindFirst(ClaimTipleri.Ad)?.Value;

    public List<Guid>? IzinliIsyerleri => isyeriKapsami.Izinli;

    public bool SistemYoneticisiMi =>
        httpContextAccessor.HttpContext?.User.HasClaim(ClaimTipleri.Rol, Roller.SistemYoneticisi) ?? false;

    private Guid? ClaimGuid(string claimTipi)
    {
        var deger = httpContextAccessor.HttpContext?.User.FindFirst(claimTipi)?.Value;
        return Guid.TryParse(deger, out var id) ? id : null;
    }
}
