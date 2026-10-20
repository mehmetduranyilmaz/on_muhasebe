using System.Net;
using OnMuhasebe.Modules.Doviz.Domain;

namespace OnMuhasebe.Modules.Doviz.Infrastructure;

internal sealed class TcmbIstemcisi(HttpClient httpClient)
{
    public static readonly Uri Adres = new("https://www.tcmb.gov.tr/kurlar/");

    // Bülten yoksa (hafta sonu, resmi tatil, henüz yayımlanmamış) null; ağ hatasında HttpRequestException.
    public async Task<IReadOnlyList<TcmbKuru>?> BultenGetirAsync(DateOnly tarih, CancellationToken cancellationToken)
    {
        using var cevap = await httpClient.GetAsync($"{tarih:yyyyMM}/{tarih:ddMMyyyy}.xml", cancellationToken);
        if (cevap.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        cevap.EnsureSuccessStatusCode();
        return KurKurallari.BulteniOku(await cevap.Content.ReadAsStringAsync(cancellationToken));
    }
}
