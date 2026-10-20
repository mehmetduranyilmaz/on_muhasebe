using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnMuhasebe.Modules.KasaBanka.Domain;

namespace OnMuhasebe.Modules.KasaBanka.Infrastructure.Configurations;

internal sealed class EkstreAktarimiConfiguration : IEntityTypeConfiguration<EkstreAktarimi>
{
    public void Configure(EntityTypeBuilder<EkstreAktarimi> builder)
    {
        builder.ToTable("ekstre_aktarimi");
        builder.Property(x => x.DosyaAdi).HasMaxLength(255);
        builder.Property(x => x.Bicim).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Durum).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.BakiyeUyarisi).HasMaxLength(500);
        builder.HasIndex(x => new { x.FirmaId, x.HesapId });
        builder.HasOne<KasaHesabi>().WithMany().HasForeignKey(x => x.HesapId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Satirlar).WithOne().HasForeignKey(x => x.AktarimId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Satirlar).HasField("_satirlar");
    }
}

internal sealed class EkstreSatiriConfiguration : IEntityTypeConfiguration<EkstreSatiri>
{
    public void Configure(EntityTypeBuilder<EkstreSatiri> builder)
    {
        builder.ToTable("ekstre_satiri");
        builder.Property(x => x.Aciklama).HasMaxLength(500);
        builder.Property(x => x.Referans).HasMaxLength(50);
        builder.Property(x => x.ParmakIzi).HasMaxLength(64);
        builder.Property(x => x.Durum).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.IslemTuru).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.OneriKaynagi).HasMaxLength(30);
        builder.Property(x => x.Hata).HasMaxLength(500);
        builder.Ignore(x => x.GirisMi);

        // Bir ekstre satırı yalnızca bir kez aktarılır ya da eşleşir; aynı dosyanın tekrar yüklenmesi veritabanında da engellenir.
        builder.HasIndex(x => new { x.HesapId, x.ParmakIzi }).IsUnique().HasFilter("durum IN ('Aktarildi', 'ZatenKayitli')");
        builder.HasIndex(x => x.EslesenHareketId).IsUnique().HasFilter("eslesen_hareket_id IS NOT NULL AND durum = 'ZatenKayitli'");
    }
}

internal sealed class EkstreSablonuConfiguration : IEntityTypeConfiguration<EkstreSablonu>
{
    public void Configure(EntityTypeBuilder<EkstreSablonu> builder)
    {
        builder.ToTable("ekstre_sablonu");
        builder.HasIndex(x => new { x.FirmaId, x.HesapId }).IsUnique();
    }
}

internal sealed class EkstreKuraliConfiguration : IEntityTypeConfiguration<EkstreKurali>
{
    public void Configure(EntityTypeBuilder<EkstreKurali> builder)
    {
        builder.ToTable("ekstre_kurali");
        builder.Property(x => x.AnahtarKelime).HasMaxLength(100);
        builder.Property(x => x.Yon).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.IslemTuru).HasConversion<string>().HasMaxLength(10);
        builder.HasIndex(x => new { x.FirmaId, x.AnahtarKelime, x.Yon }).IsUnique();
    }
}
