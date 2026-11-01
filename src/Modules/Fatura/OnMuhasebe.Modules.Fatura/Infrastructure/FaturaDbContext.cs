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

    public DbSet<TekrarPlani> TekrarPlanlari => Set<TekrarPlani>();
    public DbSet<TekrarUretimi> TekrarUretimleri => Set<TekrarUretimi>();
    public DbSet<FaturaBelgesi> Faturalar => Set<FaturaBelgesi>();
    public DbSet<FaturaSatiri> FaturaSatirlari => Set<FaturaSatiri>();
    public DbSet<FaturaAyari> FaturaAyarlari => Set<FaturaAyari>();
    public DbSet<FaturaSeriSayaci> SeriSayaclari => Set<FaturaSeriSayaci>();
    public DbSet<Irsaliye> Irsaliyeler => Set<Irsaliye>();
    public DbSet<IrsaliyeSatiri> IrsaliyeSatirlari => Set<IrsaliyeSatiri>();
    public DbSet<TeklifSiparis> TeklifSiparisler => Set<TeklifSiparis>();
    public DbSet<IsyeriSerisi> IsyeriSerileri => Set<IsyeriSerisi>();

    // Firma geneli tutarlılık hesapları (sipariş karşılama, faturalanan miktar, numara sırası ve mükerrer kontrolü) işyeri
    // kısıtlı kullanıcının işleminde de tüm işyerlerinin belgelerini görmelidir.
    public IQueryable<FaturaBelgesi> TumFaturalar => Faturalar.IgnoreQueryFilters([IsyeriFiltresi]);
    public IQueryable<Irsaliye> TumIrsaliyeler => Irsaliyeler.IgnoreQueryFilters([IsyeriFiltresi]);
    public IQueryable<TeklifSiparis> TumTeklifSiparisler => TeklifSiparisler.IgnoreQueryFilters([IsyeriFiltresi]);
    public DbSet<TeklifSiparisSatiri> TeklifSiparisSatirlari => Set<TeklifSiparisSatiri>();
}
