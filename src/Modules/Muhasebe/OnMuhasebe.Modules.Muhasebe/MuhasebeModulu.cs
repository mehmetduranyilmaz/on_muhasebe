using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnMuhasebe.Modules.Muhasebe.Contracts;
using OnMuhasebe.Modules.Muhasebe.Features;
using OnMuhasebe.Modules.Muhasebe.Infrastructure;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Modules;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Muhasebe;

// Mali müşavir köprüsü (kiralanabilir modül): belgeler Tek Düzen Hesap Planı fişlerine çevrilir ve paket programlara aktarılır.
// Kendi verisi yalnız hesap planı eşleştirmesi ve aktarım kayıtlarıdır; belgeleri kaynak modüllerin sözleşmelerinden okur.
public sealed class MuhasebeModulu : IModul
{
    public string Ad => "Muhasebe";

    public IReadOnlyList<YetkiKaynagi> YetkiKaynaklari => MuhasebeYetkileri.Tumu;

    public void ServisleriEkle(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModulDbContext<MuhasebeDbContext>(configuration);
        services.AddValidatorsFromAssemblyContaining<MuhasebeModulu>(includeInternalTypes: true);
        services.AddScoped<HesapPlaniServisi>();
    }

    public void EndpointleriEkle(IEndpointRouteBuilder endpoints)
    {
        var grup = endpoints.MapGroup("/api/muhasebe").WithTags("Muhasebe").RequireModul(ModulKodlari.Muhasebe);
        HesapPlaniIslemleri.Map(grup);
        FisIslemleri.Map(grup);
    }
}
