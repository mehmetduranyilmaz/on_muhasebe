using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace OnMuhasebe.Web.Kimlik;

// Tarayıcının refresh token çerezini farklı adresteki API'ye göndermesi için gerekli.
internal sealed class CerezHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return base.SendAsync(request, cancellationToken);
    }
}

// Her isteğe access token ekler. 401 gelirse bir kez yenileyip isteği tekrarlar.
internal sealed class ApiYetkiHandler(OturumYoneticisi oturum) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Tekrar gönderimde gövde yeniden okunamayacağı için önceden belleğe alınır.
        var govde = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        var govdeBasliklari = request.Content?.Headers.ToList();

        var token = await oturum.GecerliTokenAsync();
        TokenEkle(request, token);
        var cevap = await base.SendAsync(request, cancellationToken);

        if (cevap.StatusCode != HttpStatusCode.Unauthorized || token is null || !await oturum.YenileAsync(token))
        {
            return cevap;
        }

        cevap.Dispose();
        using var tekrar = new HttpRequestMessage(request.Method, request.RequestUri);
        if (govde is not null)
        {
            tekrar.Content = new ByteArrayContent(govde);
            foreach (var baslik in govdeBasliklari!)
            {
                tekrar.Content.Headers.TryAddWithoutValidation(baslik.Key, baslik.Value);
            }
        }

        TokenEkle(tekrar, await oturum.GecerliTokenAsync());
        return await base.SendAsync(tekrar, cancellationToken);
    }

    private static void TokenEkle(HttpRequestMessage request, string? token)
    {
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
}
