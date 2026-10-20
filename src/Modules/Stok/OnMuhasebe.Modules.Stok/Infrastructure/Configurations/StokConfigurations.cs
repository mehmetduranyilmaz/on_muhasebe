using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnMuhasebe.Modules.Stok.Domain;

namespace OnMuhasebe.Modules.Stok.Infrastructure.Configurations;

internal sealed class StokKartiConfiguration : IEntityTypeConfiguration<StokKarti>
{
    public void Configure(EntityTypeBuilder<StokKarti> builder)
    {
        builder.ToTable("stok_karti");
        builder.Property(x => x.Kod).HasMaxLength(30);
        builder.Property(x => x.Ad).HasMaxLength(250);
        builder.Property(x => x.Tur).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.TemelBirim).HasMaxLength(10);
        builder.Property(x => x.Barkod).HasMaxLength(50);
        builder.Property(x => x.AlisFiyati).HasPrecision(18, 4);
        builder.Property(x => x.SatisFiyati).HasPrecision(18, 4);
        builder.Property(x => x.ParaBirimi).HasMaxLength(3);
        builder.Property(x => x.KritikSeviye).HasPrecision(18, StokHareket.MiktarOndalik);
        builder.Property(x => x.Aciklama).HasMaxLength(1000);

        builder.HasIndex(x => new { x.FirmaId, x.Kod }).IsUnique();
        builder.HasIndex(x => new { x.FirmaId, x.Ad });
        builder.HasIndex(x => new { x.FirmaId, x.Barkod });
        builder.HasOne(x => x.Grup).WithMany().HasForeignKey(x => x.GrupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Birimler).WithOne().HasForeignKey(x => x.StokKartiId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Birimler).HasField("_birimler");
    }
}

internal sealed class StokKartiBirimiConfiguration : IEntityTypeConfiguration<StokKartiBirimi>
{
    public void Configure(EntityTypeBuilder<StokKartiBirimi> builder)
    {
        builder.ToTable("stok_karti_birimi");
        builder.Property(x => x.BirimKodu).HasMaxLength(10);
        builder.Property(x => x.Katsayi).HasPrecision(18, 6);
        builder.Property(x => x.Barkod).HasMaxLength(50);
        builder.HasIndex(x => new { x.StokKartiId, x.BirimKodu }).IsUnique();
        builder.HasIndex(x => x.Barkod);
    }
}

internal sealed class StokHareketConfiguration : IEntityTypeConfiguration<StokHareket>
{
    public void Configure(EntityTypeBuilder<StokHareket> builder)
    {
        builder.ToTable("stok_hareket");
        builder.Property(x => x.Tur).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.GirisMiktari).HasPrecision(18, StokHareket.MiktarOndalik);
        builder.Property(x => x.CikisMiktari).HasPrecision(18, StokHareket.MiktarOndalik);
        builder.Property(x => x.GirilenBirim).HasMaxLength(10);
        builder.Property(x => x.GirilenMiktar).HasPrecision(18, StokHareket.MiktarOndalik);
        builder.Property(x => x.BirimFiyat).HasPrecision(18, 4);
        builder.Property(x => x.BelgeNo).HasMaxLength(50);
        builder.Property(x => x.Aciklama).HasMaxLength(500);
        builder.Ignore(x => x.IptalEdildiMi);
        builder.Ignore(x => x.IptalKaydiMi);

        // Miktar ve maliyet hesapları yalnızca bu index'ten karşılanır.
        builder.HasIndex(x => new { x.FirmaId, x.StokKartiId, x.Tarih })
            .IncludeProperties(x => new { x.DepoId, x.GirisMiktari, x.CikisMiktari, x.BirimFiyat, x.Tur });
        builder.HasIndex(x => new { x.FirmaId, x.DepoId });
        builder.HasIndex(x => x.TransferId);
        builder.HasIndex(x => x.KaynakBelgeId);
        builder.HasOne<StokKarti>().WithMany().HasForeignKey(x => x.StokKartiId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Depo>().WithMany().HasForeignKey(x => x.DepoId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StokGrubuConfiguration : IEntityTypeConfiguration<StokGrubu>
{
    public void Configure(EntityTypeBuilder<StokGrubu> builder)
    {
        builder.ToTable("stok_grubu");
        builder.Property(x => x.Ad).HasMaxLength(100);
        builder.HasIndex(x => new { x.FirmaId, x.Ad }).IsUnique();
    }
}

internal sealed class DepoConfiguration : IEntityTypeConfiguration<Depo>
{
    public void Configure(EntityTypeBuilder<Depo> builder)
    {
        builder.ToTable("depo");
        builder.Property(x => x.Ad).HasMaxLength(100);
        builder.HasIndex(x => new { x.FirmaId, x.Ad }).IsUnique();
    }
}

internal sealed class StokAyariConfiguration : IEntityTypeConfiguration<StokAyari>
{
    public void Configure(EntityTypeBuilder<StokAyari> builder)
    {
        builder.ToTable("stok_ayari");
        builder.Property(x => x.MaliyetYontemi).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.EksiStokDavranisi).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => x.FirmaId).IsUnique();
    }
}
