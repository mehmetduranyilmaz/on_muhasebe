using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Shared.Infrastructure.Kimlik;

namespace OnMuhasebe.Modules.Cari.Infrastructure;

// C00001, C00002... Firmanın "C + rakam" biçimindeki en büyük kodunun bir fazlası; silinmiş cariler de sayılır.
// Eşzamanlı iki kayıt aynı kodu alırsa benzersiz index ikincisini 409 ile reddeder.
internal sealed class CariKodUretici(CariDbContext db, IMevcutKullanici mevcutKullanici)
{
    private const string Onek = "C";

    public async Task<string> SonrakiAsync(CancellationToken cancellationToken)
    {
        var firmaId = mevcutKullanici.FirmaId!.Value;
        var enBuyuk = await db.Database
            .SqlQuery<int?>($$"""
                SELECT MAX(CAST(SUBSTRING(kod FROM 2) AS integer)) AS "Value"
                FROM cari.cari_hesap
                WHERE firma_id = {{firmaId}} AND kod ~ '^C[0-9]{1,9}$'
                """)
            .SingleAsync(cancellationToken);

        return $"{Onek}{(enBuyuk ?? 0) + 1:D5}";
    }
}
