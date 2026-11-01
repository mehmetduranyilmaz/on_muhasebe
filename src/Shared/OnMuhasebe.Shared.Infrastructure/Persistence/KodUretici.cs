using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace OnMuhasebe.Shared.Infrastructure.Persistence;

// Kart kodları: önek + 5 hane (C00001, S00001...). Firmanın "önek + rakam" biçimindeki en büyük kodunun bir fazlası;
// silinmiş kayıtlar da sayılır. Eşzamanlı iki kayıt aynı kodu alırsa benzersiz index ikincisini 409 ile reddeder.
public static partial class KodUretici
{
    public static async Task<string> SonrakiAsync(
        DbContext db, string tablo, string onek, Guid firmaId, CancellationToken cancellationToken)
    {
        // Tablo adı parametre olamaz; SQL'e girmeden önce katı biçimde doğrulanır.
        if (!TabloDeseni().IsMatch(tablo) || !OnekDeseni().IsMatch(onek))
        {
            throw new ArgumentException("Geçersiz tablo adı veya kod öneki.");
        }

        var sql = "SELECT MAX(CAST(SUBSTRING(kod FROM " + (onek.Length + 1) + ") AS integer)) AS \"Value\" FROM " + tablo
                  + " WHERE firma_id = @firma AND kod ~ @desen";
        var enBuyuk = await db.Database
            .SqlQueryRaw<int?>(sql,
                new NpgsqlParameter("firma", firmaId),
                new NpgsqlParameter("desen", "^" + onek + "[0-9]{1,9}$"))
            .SingleAsync(cancellationToken);

        return $"{onek}{(enBuyuk ?? 0) + 1:D5}";
    }

    // Toplu kayıtta sıradaki kod: SonrakiAsync'in verdiği koddan sayarak devam edilir.
    public static string Artir(string kod, string onek) =>
        $"{onek}{int.Parse(kod[onek.Length..], System.Globalization.CultureInfo.InvariantCulture) + 1:D5}";

    [GeneratedRegex("^[a-z_]+\\.[a-z_]+$")]
    private static partial Regex TabloDeseni();

    [GeneratedRegex("^[A-Z]{1,3}$")]
    private static partial Regex OnekDeseni();
}
