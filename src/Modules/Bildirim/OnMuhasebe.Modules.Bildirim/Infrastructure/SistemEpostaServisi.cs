using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Shared.Infrastructure.Bildirimler;

namespace OnMuhasebe.Modules.Bildirim.Infrastructure;

internal sealed class SistemEpostaServisi(BildirimDbContext db, IEpostaGonderici gonderici) : IEpostaServisi
{
    public async Task<bool> GonderAsync(string alici, string aliciAdi, string konu, string html, CancellationToken cancellationToken)
    {
        var ayar = await db.EpostaAyarlari.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (ayar is not { AktifMi: true, Sunucu: not null, GonderenAdres: not null })
        {
            return false;
        }

        await gonderici.GonderAsync(ayar, [new Eposta(alici, aliciAdi, konu, html)], cancellationToken);
        return true;
    }
}
