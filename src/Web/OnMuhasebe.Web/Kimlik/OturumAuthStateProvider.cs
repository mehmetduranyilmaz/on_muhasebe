using Microsoft.AspNetCore.Components.Authorization;

namespace OnMuhasebe.Web.Kimlik;

internal sealed class OturumAuthStateProvider : AuthenticationStateProvider, IDisposable
{
    private readonly OturumYoneticisi _oturum;

    public OturumAuthStateProvider(OturumYoneticisi oturum)
    {
        _oturum = oturum;
        _oturum.Degisti += Bildir;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        Task.FromResult(new AuthenticationState(_oturum.Kullanici));

    public void Dispose() => _oturum.Degisti -= Bildir;

    private void Bildir() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}
