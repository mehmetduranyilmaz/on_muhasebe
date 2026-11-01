using Microsoft.Extensions.DependencyInjection;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Kimlik;

namespace OnMuhasebe.Shared.Infrastructure.Dosyalar;

// Dosya eklenebilen kayıt türü: sahibi modül kaydın görülebilirliğini kendi sorgu filtreleriyle (firma, silinme, işyeri) yanıtlar.
public interface IDosyaKaynagi
{
    string Tur { get; }

    string ModulKodu { get; }

    string GorunumYetkisi { get; }

    Task<bool> KayitVarMiAsync(Guid kayitId, CancellationToken cancellationToken);

    // Varsayılan: kaynağın görüntüleme yetkisi. Kişinin kendi kaydına erişebildiği kaynaklar kayda göre karar verir.
    Task<bool> ErisebilirMiAsync(IYetkiDenetimi yetki, Guid kayitId, CancellationToken cancellationToken) =>
        yetki.VarMiAsync(GorunumYetkisi, cancellationToken);
}

public static class DosyaKaynagiExtensions
{
    public static IServiceCollection AddDosyaKaynagi<TDb>(
        this IServiceCollection services, string tur, string modulKodu, YetkiKaynagi kaynak, Func<TDb, Guid, CancellationToken, Task<bool>> kayitVarMi)
        where TDb : class =>
        services.AddScoped<IDosyaKaynagi>(sp =>
            new DosyaKaynagi<TDb>(sp.GetRequiredService<TDb>(), tur, modulKodu, kaynak.Yetki(YetkiIslemleri.Goruntule), kayitVarMi));

    private sealed class DosyaKaynagi<TDb>(TDb db, string tur, string modulKodu, string gorunumYetkisi, Func<TDb, Guid, CancellationToken, Task<bool>> kayitVarMi)
        : IDosyaKaynagi
    {
        public string Tur => tur;

        public string ModulKodu => modulKodu;

        public string GorunumYetkisi => gorunumYetkisi;

        public Task<bool> KayitVarMiAsync(Guid kayitId, CancellationToken cancellationToken) => kayitVarMi(db, kayitId, cancellationToken);
    }
}
