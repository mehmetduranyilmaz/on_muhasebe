using Microsoft.Extensions.DependencyInjection;

namespace OnMuhasebe.Shared.Infrastructure.Kimlik;

// Oturumsuz işlerde (arka plan görevi, sistem yöneticisinin bir firma adına tetiklediği iş) firmayı belirler. Yalnızca kodla atanır;
// atandığı kapsamda IMevcutKullanici.FirmaId bu firmadır, böylece sorgu filtreleri ve veritabanı satır güvenliği o firmaya göre çalışır.
public sealed class ArkaPlanBaglami
{
    public Guid? FirmaId { get; private set; }

    public void FirmaAta(Guid firmaId) => FirmaId = firmaId;
}

public sealed class FirmaKapsami(IServiceScopeFactory scopeFactory)
{
    // Firma, kapsamdaki ilk veritabanı bağlantısı açılmadan atanır (bağlantı açılışta firmayı satır güvenliğine bildirir).
    public async Task<T> CalistirAsync<T>(Guid firmaId, Func<IServiceProvider, Task<T>> islem)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ArkaPlanBaglami>().FirmaAta(firmaId);
        return await islem(scope.ServiceProvider);
    }
}

// İsteğin işyeri kapsamı: yetki filtresi kullanıcının izinli işyerlerini yazar, sorgu filtresi okur. Boş (null): kısıt yok.
public sealed class IsyeriKapsami
{
    public List<Guid>? Izinli { get; private set; }

    public void Ata(IReadOnlyCollection<Guid>? izinli) => Izinli = izinli is { Count: > 0 } ? [.. izinli] : null;
}
