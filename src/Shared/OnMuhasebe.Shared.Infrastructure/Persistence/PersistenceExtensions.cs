using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Contracts.Sayfalama;

namespace OnMuhasebe.Shared.Infrastructure.Persistence;

public static class PersistenceExtensions
{
    public const string BaglantiAdi = "OnMuhasebe";

    // Tablo sahibi: migration ve satır güvenliği kurulumu bununla yapılır; uygulama çalışırken kullanılmaz.
    public const string YoneticiBaglantiAdi = "OnMuhasebeYonetici";

    public static IServiceCollection AddModulDbContext<TContext>(this IServiceCollection services, IConfiguration configuration)
        where TContext : ModulDbContext
    {
        var baglanti = configuration.GetConnectionString(BaglantiAdi)
            ?? throw new InvalidOperationException($"'{BaglantiAdi}' bağlantı dizesi tanımlı değil.");

        // Bir istekteki tüm modül context'leri aynı bağlantıyı paylaşır; böylece modüller arası işlemler
        // (fatura → cari + stok) tek veritabanı transaction'ında yürür (bkz. IslemKapsami).
        services.TryAddSingleton(_ => NpgsqlDataSource.Create(baglanti));
        services.TryAddScoped(sp => FirmaBaglantisi(sp.GetRequiredService<NpgsqlDataSource>(), sp.GetRequiredService<IMevcutKullanici>()));

        services.AddDbContext<TContext>((sp, options) => options
            .UseNpgsql(sp.GetRequiredService<NpgsqlConnection>())
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<DenetimInterceptor>(), sp.GetRequiredService<IslemKatilimInterceptor>()));

        services.AddSingleton(new ModulDbContextKaydi(typeof(TContext)));
        return services;
    }

    // Geliştirme ortamı içindir; canlıda migration'lar script/bundle ile kontrollü uygulanır.
    // Migration'lar tablo sahibi (yönetici) bağlantısıyla yürür; uygulama rolünün DDL yetkisi yoktur. Ardından satır güvenliği
    // ve uygulama rolünün yetkileri kurulur. Yönetici bağlantısı tanımlı değilse eski davranış: uygulama bağlantısıyla, RLS'siz.
    public static async Task MigrateModulDbContextsAsync(
        this IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var uygulama = configuration.GetConnectionString(BaglantiAdi)!;
        var yonetici = configuration.GetConnectionString(YoneticiBaglantiAdi) ?? uygulama;
        await using var scope = services.CreateAsyncScope();
        foreach (var kayit in scope.ServiceProvider.GetServices<ModulDbContextKaydi>())
        {
            var builder = (DbContextOptionsBuilder)Activator.CreateInstance(typeof(DbContextOptionsBuilder<>).MakeGenericType(kayit.ContextTipi))!;
            builder.UseNpgsql(yonetici).UseSnakeCaseNamingConvention();
            var secenekler = builder.GetType().GetProperty(nameof(DbContextOptionsBuilder.Options), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)!.GetValue(builder)!;
            await using var context = (DbContext)ActivatorUtilities.CreateInstance(scope.ServiceProvider, kayit.ContextTipi, secenekler);
            await context.Database.MigrateAsync(cancellationToken);
        }

        if (configuration.GetConnectionString(YoneticiBaglantiAdi) is not null)
        {
            await VeritabaniGuvenligi.UygulaAsync(yonetici, uygulama, cancellationToken);
        }
    }

    // Bağlantı istek boyunca açık kalır (EF kendi açmadığı bağlantıyı kapatmaz); firma kimliği oturuma bir kez yazılır ve
    // satır güvenliği bununla süzer. Oturumsuz isteklerde (giriş, kayıt, arka plan işi) boş kalır: iş tablolarında hiçbir satır görünmez.
    private static NpgsqlConnection FirmaBaglantisi(NpgsqlDataSource kaynak, IMevcutKullanici kullanici)
    {
        var baglanti = kaynak.OpenConnection();
        using var komut = new NpgsqlCommand($"SELECT set_config('{VeritabaniGuvenligi.FirmaAyari}', @firma, false)", baglanti);
        komut.Parameters.AddWithValue("firma", kullanici.FirmaId?.ToString() ?? "");
        komut.ExecuteNonQuery();
        return baglanti;
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
