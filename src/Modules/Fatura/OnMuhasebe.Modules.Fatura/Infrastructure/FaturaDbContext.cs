using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Fatura.Domain;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Fatura.Infrastructure;

internal sealed class FaturaDbContext(DbContextOptions<FaturaDbContext> options, IMevcutKullanici mevcutKullanici)
    : ModulDbContext(options, mevcutKullanici)
{
    public const string SemaAdi = "fatura";

    protected override string Sema => SemaAdi;

    public DbSet<FaturaBelgesi> Faturalar => Set<FaturaBelgesi>();
    public DbSet<FaturaSatiri> FaturaSatirlari => Set<FaturaSatiri>();
    public DbSet<FaturaAyari> FaturaAyarlari => Set<FaturaAyari>();
    public DbSet<FaturaSeriSayaci> SeriSayaclari => Set<FaturaSeriSayaci>();
    public DbSet<Irsaliye> Irsaliyeler => Set<Irsaliye>();
    public DbSet<IrsaliyeSatiri> IrsaliyeSatirlari => Set<IrsaliyeSatiri>();
    public DbSet<TeklifSiparis> TeklifSiparisler => Set<TeklifSiparis>();
    public DbSet<TeklifSiparisSatiri> TeklifSiparisSatirlari => Set<TeklifSiparisSatiri>();
}
