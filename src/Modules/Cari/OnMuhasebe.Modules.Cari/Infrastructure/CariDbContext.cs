using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Cari.Domain;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Cari.Infrastructure;

internal sealed class CariDbContext(DbContextOptions<CariDbContext> options, IMevcutKullanici mevcutKullanici)
    : ModulDbContext(options, mevcutKullanici)
{
    public const string SemaAdi = "cari";

    protected override string Sema => SemaAdi;

    public DbSet<CariHesap> CariHesaplar => Set<CariHesap>();
    public DbSet<CariHareket> CariHareketler => Set<CariHareket>();
    public DbSet<CariGrup> CariGruplari => Set<CariGrup>();
    public DbSet<CariKapama> CariKapamalari => Set<CariKapama>();
}
