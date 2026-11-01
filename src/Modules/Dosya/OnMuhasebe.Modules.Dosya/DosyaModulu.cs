using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnMuhasebe.Modules.Dosya.Features;
using OnMuhasebe.Modules.Dosya.Infrastructure;
using OnMuhasebe.Shared.Contracts.Dosyalar;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Modules;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Dosya;

// Lisanssız çekirdek modül: kayıtlara dosya eki. Sınırı paketin depolama kotasıdır; kayıt türlerini diğer modüller IDosyaKaynagi ile bildirir.
public sealed class DosyaModulu : IModul
{
    public string Ad => "Dosya";

    public IReadOnlyList<YetkiKaynagi> YetkiKaynaklari => DosyaYetkileri.Tumu;

    public void ServisleriEkle(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModulDbContext<DosyaDbContext>(configuration);
        services.AddSingleton<IDosyaDeposu, DiskDosyaDeposu>();
        services.AddHostedService<DosyaTemizleyici>();
    }

    public void EndpointleriEkle(IEndpointRouteBuilder endpoints) => DosyaIslemleri.Map(endpoints);
}
