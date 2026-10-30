using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnMuhasebe.Modules.KasaBanka.Domain;

namespace OnMuhasebe.Modules.KasaBanka.Infrastructure.Configurations;

internal sealed class KasaHesabiConfiguration : IEntityTypeConfiguration<KasaHesabi>
{
    public void Configure(EntityTypeBuilder<KasaHesabi> builder)
    {
        builder.ToTable("kasa_hesabi");
        builder.Property(x => x.Kod).HasMaxLength(20);
        builder.Property(x => x.Ad).HasMaxLength(150);
        builder.Property(x => x.Tur).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.ParaBirimi).HasMaxLength(3);
        builder.Property(x => x.BankaAdi).HasMaxLength(100);
        builder.Property(x => x.BankaHesapTuru).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Sube).HasMaxLength(100);
        builder.Property(x => x.HesapNo).HasMaxLength(50);
        builder.Property(x => x.Iban).HasMaxLength(34);
        builder.Property(x => x.Aciklama).HasMaxLength(500);
        builder.HasIndex(x => new { x.FirmaId, x.Kod }).IsUnique();
    }
}

internal sealed class TeminatConfiguration : IEntityTypeConfiguration<Teminat>
{
    public void Configure(EntityTypeBuilder<Teminat> builder)
    {
        builder.ToTable("teminat");
        builder.Property(x => x.Tur).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Yon).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Durum).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.TarafAdi).HasMaxLength(250);
        builder.Property(x => x.ReferansNo).HasMaxLength(100);
        builder.Property(x => x.ParaBirimi).HasMaxLength(3);
        builder.Property(x => x.Aciklama).HasMaxLength(1000);
        builder.HasIndex(x => new { x.FirmaId, x.Durum });
    }
}

internal sealed class KasaHareketiConfiguration : IEntityTypeConfiguration<KasaHareketi>
{
    public void Configure(EntityTypeBuilder<KasaHareketi> builder)
    {
        builder.ToTable("kasa_hareketi");
        builder.Property(x => x.Tur).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.ParaBirimi).HasMaxLength(3);
        builder.Property(x => x.Kur).HasPrecision(19, KasaHareketi.KurOndalik);
        builder.Property(x => x.BelgeNo).HasMaxLength(50);
        builder.Property(x => x.Aciklama).HasMaxLength(500);
        builder.Ignore(x => x.IptalEdildiMi);
        builder.Ignore(x => x.IptalKaydiMi);

        // Bakiye ve ekstre sorguları tabloya gitmeden yalnızca bu index'ten karşılanır (index-only scan).
        builder.HasIndex(x => new { x.FirmaId, x.HesapId, x.Tarih })
            .IncludeProperties(x => new { x.Giris, x.Cikis, x.GirisTl, x.CikisTl });
        builder.HasIndex(x => x.IslemId);
        builder.HasIndex(x => x.KaynakBelgeId);
        builder.HasIndex(x => x.EkstreSatiriId).IsUnique().HasFilter("ekstre_satiri_id IS NOT NULL");
        builder.HasOne<KasaHesabi>().WithMany().HasForeignKey(x => x.HesapId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class GelirGiderKaydiConfiguration : IEntityTypeConfiguration<GelirGiderKaydi>
{
    public void Configure(EntityTypeBuilder<GelirGiderKaydi> builder)
    {
        builder.ToTable("gelir_gider_kaydi");
        builder.Property(x => x.Tur).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Bicim).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.ParaBirimi).HasMaxLength(3);
        builder.Property(x => x.Kur).HasPrecision(19, KasaHareketi.KurOndalik);
        builder.Property(x => x.BelgeNo).HasMaxLength(50);
        builder.Property(x => x.Aciklama).HasMaxLength(500);
        builder.HasIndex(x => new { x.FirmaId, x.Tarih });
        builder.HasIndex(x => x.IslemId).IsUnique();
        builder.HasOne<GelirGiderKategorisi>().WithMany().HasForeignKey(x => x.KategoriId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class GelirGiderKategorisiConfiguration : IEntityTypeConfiguration<GelirGiderKategorisi>
{
    public void Configure(EntityTypeBuilder<GelirGiderKategorisi> builder)
    {
        builder.ToTable("gelir_gider_kategorisi");
        builder.Property(x => x.Ad).HasMaxLength(100);
        builder.Property(x => x.Tur).HasConversion<string>().HasMaxLength(10);
        builder.HasIndex(x => new { x.FirmaId, x.Tur, x.Ad }).IsUnique();
    }
}
