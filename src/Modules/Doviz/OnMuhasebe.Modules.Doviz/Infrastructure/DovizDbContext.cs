using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnMuhasebe.Modules.Doviz.Domain;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Doviz.Infrastructure;

internal sealed class DovizDbContext(DbContextOptions<DovizDbContext> options, IMevcutKullanici mevcutKullanici)
    : ModulDbContext(options, mevcutKullanici)
{
    protected override string Sema => "doviz";

    public DbSet<Kur> Kurlar => Set<Kur>();
    public DbSet<KurBulteni> KurBultenleri => Set<KurBulteni>();
}

internal sealed class KurConfiguration : IEntityTypeConfiguration<Kur>
{
    public void Configure(EntityTypeBuilder<Kur> builder)
    {
        builder.ToTable("kur");
        builder.Property(x => x.ParaBirimi).HasMaxLength(3);
        builder.Property(x => x.DovizAlis).HasPrecision(19, 6);
        builder.Property(x => x.DovizSatis).HasPrecision(19, 6);
        builder.Property(x => x.EfektifAlis).HasPrecision(19, 6);
        builder.Property(x => x.EfektifSatis).HasPrecision(19, 6);
        builder.HasIndex(x => new { x.Tarih, x.ParaBirimi }).IsUnique();
    }
}

internal sealed class KurBulteniConfiguration : IEntityTypeConfiguration<KurBulteni>
{
    public void Configure(EntityTypeBuilder<KurBulteni> builder)
    {
        builder.ToTable("kur_bulteni");
        builder.HasIndex(x => x.Tarih).IsUnique();
    }
}
