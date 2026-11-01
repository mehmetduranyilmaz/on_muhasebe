using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnMuhasebe.Modules.Bakim.Contracts;
using OnMuhasebe.Modules.Bakim.Features;
using OnMuhasebe.Modules.Bakim.Infrastructure;
using OnMuhasebe.Shared.Contracts.Dosyalar;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Infrastructure.Bildirimler;
using OnMuhasebe.Shared.Infrastructure.Dosyalar;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Modules;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Bakim;

// Kiralanan modül: ekipman kartı, periyodik bakım ve yasal kontrol planı, arıza ve bakım iş emirleri, duruş ve maliyet takibi.
public sealed class BakimModulu : IModul
{
    public string Ad => "Bakım Takibi";

    public IReadOnlyList<YetkiKaynagi> YetkiKaynaklari => BakimYetkileri.Tumu;

    public void ServisleriEkle(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModulDbContext<BakimDbContext>(configuration);
        services.AddValidatorsFromAssemblyContaining<BakimModulu>(includeInternalTypes: true);
        services.AddScoped<IBildirimKaynagi, BakimBildirimKaynagi>();
        services.AddDosyaKaynagi<BakimDbContext>(DosyaKayitTurleri.Ekipman, ModulKodlari.Bakim, BakimYetkileri.Ekipman,
            (db, id, ct) => db.Ekipmanlar.AnyAsync(e => e.Id == id, ct));
        services.AddDosyaKaynagi<BakimDbContext>(DosyaKayitTurleri.IsEmri, ModulKodlari.Bakim, BakimYetkileri.IsEmri,
            (db, id, ct) => db.IsEmirleri.AnyAsync(i => i.Id == id && db.Ekipmanlar.Any(e => e.Id == i.EkipmanId), ct));
    }

    public void EndpointleriEkle(IEndpointRouteBuilder endpoints)
    {
        var grup = endpoints.MapGroup("/api/bakim").WithTags("Bakım Takibi").RequireModul(ModulKodlari.Bakim);
        grup.MapDegisiklikGecmisi<BakimDbContext>();
        EkipmanIslemleri.Map(grup);
        IsEmriIslemleri.Map(grup);
        BakimRaporlari.Map(grup);
    }
}
