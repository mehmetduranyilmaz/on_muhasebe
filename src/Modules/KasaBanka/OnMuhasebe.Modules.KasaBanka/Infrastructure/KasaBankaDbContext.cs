using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.KasaBanka.Domain;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.KasaBanka.Infrastructure;

internal sealed class KasaBankaDbContext(DbContextOptions<KasaBankaDbContext> options, IMevcutKullanici mevcutKullanici)
    : ModulDbContext(options, mevcutKullanici)
{
    public const string SemaAdi = "kasabanka";

    protected override string Sema => SemaAdi;

    public DbSet<KasaHesabi> Hesaplar => Set<KasaHesabi>();
    public DbSet<KasaHareketi> KasaHareketleri => Set<KasaHareketi>();
    public DbSet<GelirGiderKaydi> GelirGiderKayitlari => Set<GelirGiderKaydi>();
    public DbSet<GelirGiderKategorisi> Kategoriler => Set<GelirGiderKategorisi>();
}
