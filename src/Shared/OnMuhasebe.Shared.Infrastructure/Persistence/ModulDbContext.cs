using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Shared.Domain.Abstractions;
using OnMuhasebe.Shared.Domain.Entities;
using OnMuhasebe.Shared.Infrastructure.Kimlik;

namespace OnMuhasebe.Shared.Infrastructure.Persistence;

public abstract class ModulDbContext(DbContextOptions options, IMevcutKullanici mevcutKullanici) : DbContext(options)
{
    public const string FirmaFiltresi = "Firma";
    public const string SilinmeFiltresi = "Silinme";

    protected abstract string Sema { get; }

    // Sorgu filtresi bu değeri her sorguda yeniden okur; context örneği ifadeye sabit olarak gömülür.
    private Guid? MevcutFirmaId => mevcutKullanici.FirmaId;

    private static readonly PropertyInfo _mevcutFirmaIdOzelligi =
        typeof(ModulDbContext).GetProperty(nameof(MevcutFirmaId), BindingFlags.Instance | BindingFlags.NonPublic)!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseNpgsql(o => o.MigrationsHistoryTable("__ef_migrations_history", Sema));

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Sema);
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(t => t.BaseType is null))
        {
            var tip = entityType.ClrType;
            var builder = modelBuilder.Entity(tip);

            if (typeof(BaseEntity).IsAssignableFrom(tip))
            {
                builder.Property(nameof(BaseEntity.Surum)).IsRowVersion();

                // Id'ler uygulamada (Guid v7) üretilir. Aksi halde EF, var olan bir kayda eklenen yeni alt kaydı
                // (ör. stok kartına yeni birim) "dolu anahtarlı = mevcut" sanıp INSERT yerine UPDATE dener.
                builder.Property(nameof(BaseEntity.Id)).ValueGeneratedNever();
            }

            if (typeof(IFirmaVarligi).IsAssignableFrom(tip))
            {
                builder.HasQueryFilter(FirmaFiltresi, FirmaFiltresiOlustur(tip));
            }

            if (typeof(ISilinebilir).IsAssignableFrom(tip))
            {
                builder.HasQueryFilter(SilinmeFiltresi, SilinmeFiltresiOlustur(tip));
            }
        }
    }

    // e => (Guid?)e.FirmaId == this.MevcutFirmaId
    private LambdaExpression FirmaFiltresiOlustur(Type tip)
    {
        var e = Expression.Parameter(tip, "e");
        var firmaId = Expression.Convert(Expression.Property(e, nameof(IFirmaVarligi.FirmaId)), typeof(Guid?));
        var mevcutFirmaId = Expression.Property(Expression.Constant(this, typeof(ModulDbContext)), _mevcutFirmaIdOzelligi);
        return Expression.Lambda(Expression.Equal(firmaId, mevcutFirmaId), e);
    }

    // e => !e.SilindiMi
    private static LambdaExpression SilinmeFiltresiOlustur(Type tip)
    {
        var e = Expression.Parameter(tip, "e");
        return Expression.Lambda(Expression.Not(Expression.Property(e, nameof(ISilinebilir.SilindiMi))), e);
    }
}
