using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        services.AddDbContext<TContext>((sp, options) => options
            .UseNpgsql(baglanti)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<DenetimInterceptor>()));

        return services;
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
}
