using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using OnMuhasebe.Shared.Contracts.Sayfalama;

namespace OnMuhasebe.Shared.Infrastructure.Persistence;

public static class PersistenceExtensions
{
    public const string BaglantiAdi = "OnMuhasebe";

    public static IServiceCollection AddModulDbContext<TContext>(this IServiceCollection services, IConfiguration configuration)
        where TContext : ModulDbContext
    {
        var baglanti = configuration.GetConnectionString(BaglantiAdi)
            ?? throw new InvalidOperationException($"'{BaglantiAdi}' bağlantı dizesi tanımlı değil.");

        // Bir istekteki tüm modül context'leri aynı bağlantıyı paylaşır; böylece modüller arası işlemler
        // (fatura → cari + stok) tek veritabanı transaction'ında yürür (bkz. IslemKapsami).
        services.TryAddSingleton(_ => NpgsqlDataSource.Create(baglanti));
        services.TryAddScoped(sp => sp.GetRequiredService<NpgsqlDataSource>().CreateConnection());

        services.AddDbContext<TContext>((sp, options) => options
            .UseNpgsql(sp.GetRequiredService<NpgsqlConnection>())
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<DenetimInterceptor>(), sp.GetRequiredService<IslemKatilimInterceptor>()));

        services.AddSingleton(new ModulDbContextKaydi(typeof(TContext)));
        return services;
    }

    // Geliştirme ortamı içindir; canlıda migration'lar script/bundle ile kontrollü uygulanır.
    public static async Task MigrateModulDbContextsAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        foreach (var kayit in scope.ServiceProvider.GetServices<ModulDbContextKaydi>())
        {
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(kayit.ContextTipi);
            await context.Database.MigrateAsync(cancellationToken);
        }
    }

    public static async Task<SayfaliSonuc<T>> SayfalaAsync<T>(
        this IQueryable<T> sorgu, SayfalamaIstegi istek, CancellationToken cancellationToken = default)
    {
        var toplam = await sorgu.CountAsync(cancellationToken);
        var kayitlar = await sorgu
            .Skip((istek.GecerliSayfa - 1) * istek.GecerliBoyut)
            .Take(istek.GecerliBoyut)
            .ToListAsync(cancellationToken);

        return new SayfaliSonuc<T>(kayitlar, istek.GecerliSayfa, istek.GecerliBoyut, toplam);
    }

    private sealed record ModulDbContextKaydi(Type ContextTipi);
}
