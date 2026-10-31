using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Stok.Domain;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Stok.Infrastructure;

internal sealed class StokDbContext(DbContextOptions<StokDbContext> options, IMevcutKullanici mevcutKullanici)
    : ModulDbContext(options, mevcutKullanici)
{
    protected override string Sema => "stok";

    public DbSet<StokKarti> StokKartlari => Set<StokKarti>();
    public DbSet<StokKartiBirimi> StokKartiBirimleri => Set<StokKartiBirimi>();
    public DbSet<StokHareket> StokHareketleri => Set<StokHareket>();
    public DbSet<StokGrubu> StokGruplari => Set<StokGrubu>();
    public DbSet<Depo> Depolar => Set<Depo>();
    public DbSet<StokAyari> StokAyarlari => Set<StokAyari>();
    public DbSet<FiyatListesi> FiyatListeleri => Set<FiyatListesi>();
    public DbSet<FiyatListesiSatiri> FiyatListesiSatirlari => Set<FiyatListesiSatiri>();
    public DbSet<FiyatListesiAtamasi> FiyatListesiAtamalari => Set<FiyatListesiAtamasi>();
}
