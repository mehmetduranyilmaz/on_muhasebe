using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnMuhasebe.Modules.Stok.Domain;

namespace OnMuhasebe.Modules.Stok.Infrastructure.Configurations;

internal sealed class StokKartiConfiguration : IEntityTypeConfiguration<StokKarti>
{
    public void Configure(EntityTypeBuilder<StokKarti> builder)
    {
        builder.ToTable("stok_karti");
        builder.Property(x => x.Izleme).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Kod).HasMaxLength(30);
        builder.Property(x => x.Ad).HasMaxLength(250);
        builder.Property(x => x.Tur).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.TemelBirim).HasMaxLength(10);
        builder.Property(x => x.Barkod).HasMaxLength(50);
        builder.Property(x => x.AlisFiyati).HasPrecision(18, 4);
        builder.Property(x => x.SatisFiyati).HasPrecision(18, 4);
        builder.Property(x => x.PerakendeFiyati).HasPrecision(18, 4);
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

        // Lot bakiyesi (kart + depo + lot) ve SKT raporu bu index'ten okunur.
        builder.Property(x => x.LotNo).HasMaxLength(50);
        builder.HasIndex(x => new { x.FirmaId, x.StokKartiId, x.LotNo }).HasFilter("lot_no IS NOT NULL");
        builder.HasMany(x => x.Seriler).WithOne().HasForeignKey(x => x.HareketId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Seriler).HasField("_seriler");
    }
}

internal sealed class StokHareketSeriConfiguration : IEntityTypeConfiguration<StokHareketSeri>
{
    public void Configure(EntityTypeBuilder<StokHareketSeri> builder)
    {
        builder.ToTable("stok_hareket_seri");
        builder.Property(x => x.SeriNo).HasMaxLength(100);
        builder.HasIndex(x => new { x.StokKartiId, x.SeriNo });
        builder.HasIndex(x => x.SeriNo);
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
        builder.Property(x => x.Adres).HasMaxLength(500);
        builder.Property(x => x.Il).HasMaxLength(50);
        builder.Property(x => x.Ilce).HasMaxLength(50);
        builder.HasIndex(x => new { x.FirmaId, x.IsyeriId });
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

internal sealed class FiyatListesiConfiguration : IEntityTypeConfiguration<FiyatListesi>
{
    public void Configure(EntityTypeBuilder<FiyatListesi> builder)
    {
        builder.ToTable("fiyat_listesi");
        builder.Property(x => x.Ad).HasMaxLength(100);
        builder.Property(x => x.Tur).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.ParaBirimi).HasMaxLength(3);
        builder.Property(x => x.Aciklama).HasMaxLength(500);
        builder.HasIndex(x => new { x.FirmaId, x.Ad }).IsUnique();
        builder.HasMany(x => x.Satirlar).WithOne().HasForeignKey(x => x.FiyatListesiId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Satirlar).HasField("_satirlar");
        builder.HasMany(x => x.Atamalar).WithOne().HasForeignKey(x => x.FiyatListesiId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Atamalar).HasField("_atamalar");
    }
}

internal sealed class FiyatListesiSatiriConfiguration : IEntityTypeConfiguration<FiyatListesiSatiri>
{
    public void Configure(EntityTypeBuilder<FiyatListesiSatiri> builder)
    {
        builder.ToTable("fiyat_listesi_satiri");
        builder.Property(x => x.MinMiktar).HasPrecision(18, StokHareket.MiktarOndalik);
        builder.Property(x => x.Fiyat).HasPrecision(18, 4);
        builder.Property(x => x.Iskonto).HasPrecision(5, 2);
        builder.HasIndex(x => new { x.FiyatListesiId, x.StokKartiId, x.MinMiktar }).IsUnique();
        builder.HasIndex(x => x.StokKartiId);
        builder.HasOne<StokKarti>().WithMany().HasForeignKey(x => x.StokKartiId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FiyatListesiAtamasiConfiguration : IEntityTypeConfiguration<FiyatListesiAtamasi>
{
    public void Configure(EntityTypeBuilder<FiyatListesiAtamasi> builder)
    {
        builder.ToTable("fiyat_listesi_atamasi");
        builder.Property(x => x.Tur).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Ad).HasMaxLength(250);
        builder.HasIndex(x => new { x.FiyatListesiId, x.Tur, x.HedefId }).IsUnique();
        builder.HasIndex(x => x.HedefId);
    }
}
