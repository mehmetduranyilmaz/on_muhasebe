using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnMuhasebe.Modules.Muhasebe.Domain;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Muhasebe.Infrastructure;

internal sealed class MuhasebeDbContext(DbContextOptions<MuhasebeDbContext> options, IMevcutKullanici mevcutKullanici)
    : ModulDbContext(options, mevcutKullanici)
{
    protected override string Sema => "muhasebe";

    public DbSet<MuhasebeAyari> Ayarlar => Set<MuhasebeAyari>();
    public DbSet<HesapKodu> HesapKodlari => Set<HesapKodu>();
    public DbSet<KodEslemesi> KodEslemeleri => Set<KodEslemesi>();
    public DbSet<MuhasebeAktarimi> Aktarimlar => Set<MuhasebeAktarimi>();
    public DbSet<AktarilanFis> AktarilanFisler => Set<AktarilanFis>();
}

internal sealed class MuhasebeAyariConfiguration : IEntityTypeConfiguration<MuhasebeAyari>
{
    public void Configure(EntityTypeBuilder<MuhasebeAyari> builder)
    {
        builder.ToTable("muhasebe_ayari");
        builder.Property(x => x.Ayirac).HasMaxLength(1);
        builder.Property(x => x.CariAltGrup).HasMaxLength(10);
        builder.HasIndex(x => x.FirmaId).IsUnique();
    }
}

internal sealed class HesapKoduConfiguration : IEntityTypeConfiguration<HesapKodu>
{
    public void Configure(EntityTypeBuilder<HesapKodu> builder)
    {
        builder.ToTable("hesap_kodu");
        builder.Property(x => x.Rol).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Kod).HasMaxLength(30);
        builder.HasIndex(x => new { x.FirmaId, x.Rol }).IsUnique();
    }
}

internal sealed class KodEslemesiConfiguration : IEntityTypeConfiguration<KodEslemesi>
{
    public void Configure(EntityTypeBuilder<KodEslemesi> builder)
    {
        builder.ToTable("kod_eslemesi");
        builder.Property(x => x.KartTuru).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Kod).HasMaxLength(30);
        builder.HasIndex(x => new { x.FirmaId, x.KartTuru, x.KartId }).IsUnique();
    }
}

internal sealed class MuhasebeAktarimiConfiguration : IEntityTypeConfiguration<MuhasebeAktarimi>
{
    public void Configure(EntityTypeBuilder<MuhasebeAktarimi> builder)
    {
        builder.ToTable("muhasebe_aktarimi");
        builder.Property(x => x.ToplamBorc).HasPrecision(18, 2);
        builder.Property(x => x.Aciklama).HasMaxLength(500);
        builder.Property(x => x.AktaranAdi).HasMaxLength(256);
        builder.HasIndex(x => new { x.FirmaId, x.Baslangic });
    }
}

internal sealed class AktarilanFisConfiguration : IEntityTypeConfiguration<AktarilanFis>
{
    public void Configure(EntityTypeBuilder<AktarilanFis> builder)
    {
        builder.ToTable("aktarilan_fis");
        builder.Property(x => x.Kaynak).HasMaxLength(20);
        builder.Property(x => x.BelgeTuru).HasMaxLength(40);
        builder.Property(x => x.BelgeNo).HasMaxLength(100);
        builder.Property(x => x.Ozet).HasMaxLength(32);
        builder.HasOne<MuhasebeAktarimi>().WithMany().HasForeignKey(x => x.AktarimId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.FirmaId, x.Kaynak, x.KaynakId }).IsUnique();
        builder.HasIndex(x => new { x.FirmaId, x.Tarih });
        builder.HasIndex(x => x.AktarimId);
    }
}
