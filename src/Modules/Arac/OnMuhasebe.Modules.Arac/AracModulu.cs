using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnMuhasebe.Modules.Arac.Contracts;
using OnMuhasebe.Modules.Arac.Features;
using OnMuhasebe.Modules.Arac.Infrastructure;
using OnMuhasebe.Shared.Contracts.Dosyalar;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Infrastructure.Bildirimler;
using OnMuhasebe.Shared.Infrastructure.Dosyalar;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Modules;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Arac;

// Kiralanan modül: araç kartı, sigorta/muayene/MTV süreleri, yakıt ve masraf takibi, hasar. Masraf takip kaydıdır, muhasebe kaydı üretmez.
public sealed class AracModulu : IModul
{
    public string Ad => "Araç Takibi";

    public IReadOnlyList<YetkiKaynagi> YetkiKaynaklari => AracYetkileri.Tumu;

    public void ServisleriEkle(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModulDbContext<AracDbContext>(configuration);
        services.AddValidatorsFromAssemblyContaining<AracModulu>(includeInternalTypes: true);
        services.AddScoped<IBildirimKaynagi, AracBildirimKaynagi>();
        services.AddDosyaKaynagi<AracDbContext>(DosyaKayitTurleri.Arac, ModulKodlari.Arac, AracYetkileri.Kart,
            (db, id, ct) => db.Araclar.AnyAsync(a => a.Id == id, ct));
        services.AddDosyaKaynagi<AracDbContext>(DosyaKayitTurleri.AracYukumluluk, ModulKodlari.Arac, AracYetkileri.Islem,
            (db, id, ct) => db.Yukumlulukler.AnyAsync(y => y.Id == id && db.Araclar.Any(a => a.Id == y.AracId), ct));
    }

    public void EndpointleriEkle(IEndpointRouteBuilder endpoints)
    {
        var grup = endpoints.MapGroup("/api/arac").WithTags("Araç Takibi").RequireModul(ModulKodlari.Arac);
        grup.MapDegisiklikGecmisi<AracDbContext>();
        AracIslemleri.Map(grup);
        AracKayitIslemleri.Map(grup);
        AracRaporlari.Map(grup);
    }
}
