using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnMuhasebe.Modules.Personel.Contracts;
using OnMuhasebe.Modules.Personel.Features;
using OnMuhasebe.Modules.Personel.Infrastructure;
using OnMuhasebe.Shared.Contracts.Dosyalar;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Infrastructure.Bildirimler;
using OnMuhasebe.Shared.Infrastructure.Dosyalar;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Modules;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Personel;

// Kiralanan modül: hafif personel takibi. Bordro hesabı yapmaz; net maaş, avans, masraf ve zimmet tutulur.
public sealed class PersonelModulu : IModul
{
    public string Ad => "Personel";

    public IReadOnlyList<YetkiKaynagi> YetkiKaynaklari => PersonelYetkileri.Tumu;

    public void ServisleriEkle(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModulDbContext<PersonelDbContext>(configuration);
        services.AddValidatorsFromAssemblyContaining<PersonelModulu>(includeInternalTypes: true);
        services.AddScoped<PersonelOdemeServisi>();
        services.AddScoped<IPersonelMuhasebeSorgusu, PersonelMuhasebeSorgusu>();
        services.AddScoped<IBildirimKaynagi, PersonelBildirimKaynagi>();
        services.AddDosyaKaynagi<PersonelDbContext>(DosyaKayitTurleri.Personel, ModulKodlari.Personel, PersonelYetkileri.Maas,
            (db, id, ct) => db.Personeller.AnyAsync(p => p.Id == id, ct));
        services.AddScoped<IDosyaKaynagi, MasrafDosyaKaynagi>();
        services.AddDosyaKaynagi<PersonelDbContext>(DosyaKayitTurleri.Zimmet, ModulKodlari.Personel, PersonelYetkileri.Zimmet,
            (db, id, ct) => db.Zimmetler.AnyAsync(z => z.Id == id, ct));
    }

    public void EndpointleriEkle(IEndpointRouteBuilder endpoints)
    {
        var grup = endpoints.MapGroup("/api/personel").WithTags("Personel").RequireModul(ModulKodlari.Personel);
        grup.MapDegisiklikGecmisi<PersonelDbContext>();
        PersonelIslemleri.Map(grup);
        MaasIslemleri.Map(grup);
        AvansIslemleri.Map(grup);
        MasrafIslemleri.Map(grup);
        ZimmetIslemleri.Map(grup);
    }
}
