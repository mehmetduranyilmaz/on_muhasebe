using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Yonetim.Domain;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Yonetim.Infrastructure;

internal sealed class YonetimDbContext(DbContextOptions<YonetimDbContext> options, IMevcutKullanici mevcutKullanici)
    : ModulDbContext(options, mevcutKullanici)
{
    protected override string Sema => "yonetim";

    public DbSet<Firma> Firmalar => Set<Firma>();
    public DbSet<Kullanici> Kullanicilar => Set<Kullanici>();
    public DbSet<FirmaKullanicisi> FirmaKullanicilari => Set<FirmaKullanicisi>();
    public DbSet<Paket> Paketler => Set<Paket>();
    public DbSet<FirmaLisansi> FirmaLisanslari => Set<FirmaLisansi>();
    public DbSet<RefreshToken> RefreshTokenlar => Set<RefreshToken>();
    public DbSet<FirmaZorunluAlani> ZorunluAlanlar => Set<FirmaZorunluAlani>();
    public DbSet<IstekKaydi> IstekKayitlari => Set<IstekKaydi>();
    public DbSet<DonemKilidi> DonemKilitleri => Set<DonemKilidi>();
    public DbSet<OdemeBildirimi> OdemeBildirimleri => Set<OdemeBildirimi>();
    public DbSet<SistemAyari> SistemAyarlari => Set<SistemAyari>();
}
