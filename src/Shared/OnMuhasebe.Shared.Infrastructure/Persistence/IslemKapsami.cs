using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Shared.Infrastructure.Persistence;

// Birden çok modülün yazdığı işlemi tek transaction'da çalıştırır (ör. fatura + cari hareket + stok hareketleri).
// Sonuç başarısızsa ya da hata fırlarsa tüm yazılanlar geri alınır.
public interface IIslemKapsami
{
    Task<Sonuc<T>> CalistirAsync<T>(Func<CancellationToken, Task<Sonuc<T>>> islem, CancellationToken cancellationToken);
}

// Context'ler istek başına ortak bağlantıyı kullandığından transaction bağlantı üzerinde açılır; kapsam içinde
// SaveChanges yapan her context IslemKatilimInterceptor ile bu transaction'a katılır. TransactionScope kullanılmaz:
// ayrı bağlantılarda dağıtık transaction'a dönüşür ve Npgsql bunu desteklemez.
internal sealed class IslemKapsami(NpgsqlConnection baglanti) : IIslemKapsami
{
    private readonly List<DbContext> _katilanlar = [];

    public NpgsqlTransaction? Aktif { get; private set; }

    public async Task<Sonuc<T>> CalistirAsync<T>(Func<CancellationToken, Task<Sonuc<T>>> islem, CancellationToken cancellationToken)
    {
        // İç içe çağrı dıştaki işlemin parçasıdır.
        if (Aktif is not null)
        {
            return await islem(cancellationToken);
        }

        var baglantiyiBizActi = baglanti.State != ConnectionState.Open;
        if (baglantiyiBizActi)
        {
            await baglanti.OpenAsync(cancellationToken);
        }

        try
        {
            await using var transaction = await baglanti.BeginTransactionAsync(cancellationToken);
            Aktif = transaction;

            var sonuc = await islem(cancellationToken);
            if (sonuc.BasariliMi)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            return sonuc;
        }
        finally
        {
            Aktif = null;
            foreach (var context in _katilanlar)
            {
                await context.Database.UseTransactionAsync(null, CancellationToken.None);
            }

            _katilanlar.Clear();
            if (baglantiyiBizActi)
            {
                await baglanti.CloseAsync();
            }
        }
    }

    internal async ValueTask KatilAsync(DbContext context, CancellationToken cancellationToken)
    {
        if (Aktif is null || context.Database.CurrentTransaction is not null)
        {
            return;
        }

        await context.Database.UseTransactionAsync(Aktif, cancellationToken);
        _katilanlar.Add(context);
    }
}

// SaveChanges'ın kendi transaction'ını açmaması için context'i aktif işleme katar; sorgu komutlarını da aynı
// transaction'a bağlar (kapsam içindeki okumalar, yazılmış ama henüz onaylanmamış kayıtları görür).
internal sealed class IslemKatilimInterceptor(IslemKapsami kapsam) : SaveChangesInterceptor, IDbCommandInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
        {
            await kapsam.KatilAsync(context, cancellationToken);
        }

        return result;
    }

    public DbCommand CommandInitialized(CommandEndEventData eventData, DbCommand result)
    {
        if (kapsam.Aktif is { } aktif && result.Transaction is null)
        {
            result.Transaction = aktif;
        }

        return result;
    }
}
