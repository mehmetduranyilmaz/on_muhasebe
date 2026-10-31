using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OnMuhasebe.Modules.KasaBanka.Domain;

namespace OnMuhasebe.Modules.KasaBanka.Infrastructure.Configurations;

internal sealed class KasaHesabiConfiguration : IEntityTypeConfiguration<KasaHesabi>
{
    public void Configure(EntityTypeBuilder<KasaHesabi> builder)
    {
        builder.ToTable("kasa_hesabi");
        builder.HasIndex(x => new { x.FirmaId, x.IsyeriId });
        builder.Property(x => x.Kod).HasMaxLength(20);
        builder.Property(x => x.Ad).HasMaxLength(150);
        builder.Property(x => x.Tur).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.ParaBirimi).HasMaxLength(3);
        builder.Property(x => x.BankaAdi).HasMaxLength(100);
        builder.Property(x => x.BankaHesapTuru).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Sube).HasMaxLength(100);
        builder.Property(x => x.HesapNo).HasMaxLength(50);
        builder.Property(x => x.Iban).HasMaxLength(34);
        builder.Property(x => x.PosCalismaSekli).HasConversion<string>().HasMaxLength(12);
        builder.Property(x => x.PosGunTipi).HasConversion<string>().HasMaxLength(12);
        builder.Property(x => x.PosKomisyonOrani).HasPrecision(9, 4);
        builder.Property(x => x.PosTerminalNo).HasMaxLength(30);
        builder.Property(x => x.PosBatchNo).HasMaxLength(30);
        builder.Property(x => x.Aciklama).HasMaxLength(500);
        builder.HasIndex(x => new { x.FirmaId, x.Kod }).IsUnique();
        builder.HasOne<Sube>().WithMany().HasForeignKey(x => x.SubeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<KasaHesabi>().WithMany().HasForeignKey(x => x.PosAktarilacakHesapId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SubeConfiguration : IEntityTypeConfiguration<Sube>
{
    public void Configure(EntityTypeBuilder<Sube> builder)
    {
        builder.ToTable("sube");
        builder.Property(x => x.BankaAdi).HasMaxLength(100);
        builder.Property(x => x.Kod).HasMaxLength(10);
        builder.Property(x => x.Ad).HasMaxLength(150);
        builder.Property(x => x.PortfoyYoneticisi).HasMaxLength(150);
        builder.Property(x => x.Telefon).HasMaxLength(30);
        builder.Property(x => x.Faks).HasMaxLength(30);
        builder.HasIndex(x => new { x.FirmaId, x.BankaAdi });
    }
}

internal sealed class TeminatConfiguration : IEntityTypeConfiguration<Teminat>
{
    private static readonly ValueConverter<Dictionary<string, string>, string> DetayDonusturucu = new(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => string.IsNullOrEmpty(v) ? new() : JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new());

    private static readonly ValueComparer<Dictionary<string, string>> DetayKarsilastirici = new(
        (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
        v => JsonSerializer.Deserialize<Dictionary<string, string>>(JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null) ?? new());

    public void Configure(EntityTypeBuilder<Teminat> builder)
    {
        builder.ToTable("teminat");
        builder.Property(x => x.Tur).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Yon).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Durum).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.KomisyonDonemi).HasConversion<string>().HasMaxLength(12);
        builder.Property(x => x.KomisyonOrani).HasPrecision(9, 4);
        builder.Property(x => x.KomisyonTutari).HasPrecision(18, 2);
        builder.Property(x => x.BankaAdi).HasMaxLength(100);
        builder.Property(x => x.ReferansNo).HasMaxLength(100);
        builder.Property(x => x.ParaBirimi).HasMaxLength(3);
        builder.Property(x => x.Aciklama).HasMaxLength(1000);
        builder.Property(x => x.Detaylar).HasConversion(DetayDonusturucu, DetayKarsilastirici).HasColumnType("jsonb");
        builder.HasIndex(x => new { x.FirmaId, x.Durum });
        builder.HasIndex(x => new { x.FirmaId, x.CariHesapId });
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
        builder.HasIndex(x => x.MasrafMerkeziId);
        builder.HasOne<MasrafMerkezi>().WithMany().HasForeignKey(x => x.MasrafMerkeziId).OnDelete(DeleteBehavior.Restrict);
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

internal sealed class MasrafMerkeziConfiguration : IEntityTypeConfiguration<MasrafMerkezi>
{
    public void Configure(EntityTypeBuilder<MasrafMerkezi> builder)
    {
        builder.ToTable("masraf_merkezi");
        builder.Property(x => x.Kod).HasMaxLength(20);
        builder.Property(x => x.Ad).HasMaxLength(100);
        builder.HasIndex(x => new { x.FirmaId, x.Kod }).IsUnique();
    }
}

internal sealed class DemirbasConfiguration : IEntityTypeConfiguration<Demirbas>
{
    public void Configure(EntityTypeBuilder<Demirbas> builder)
    {
        builder.ToTable("demirbas");
        builder.HasIndex(x => new { x.FirmaId, x.IsyeriId });
        builder.Property(x => x.SicilNo).HasMaxLength(30);
        builder.Property(x => x.Ad).HasMaxLength(200);
        builder.Property(x => x.Grup).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Yontem).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ElCikarmaTuru).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.BelgeNo).HasMaxLength(50);
        builder.Property(x => x.Zimmetli).HasMaxLength(100);
        builder.Property(x => x.Konum).HasMaxLength(100);
        builder.Property(x => x.Aciklama).HasMaxLength(500);
        builder.Property(x => x.ElCikarmaAciklamasi).HasMaxLength(500);
        builder.HasIndex(x => new { x.FirmaId, x.SicilNo }).IsUnique();
        builder.HasOne<MasrafMerkezi>().WithMany().HasForeignKey(x => x.MasrafMerkeziId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class KrediConfiguration : IEntityTypeConfiguration<Kredi>
{
    public void Configure(EntityTypeBuilder<Kredi> builder)
    {
        builder.ToTable("kredi");
        builder.Property(x => x.Kod).HasMaxLength(20);
        builder.Property(x => x.Ad).HasMaxLength(150);
        builder.Property(x => x.Tur).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Durum).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.ParaBirimi).HasMaxLength(3);
        builder.Property(x => x.AylikFaizOrani).HasPrecision(9, 4);
        builder.Property(x => x.Aciklama).HasMaxLength(1000);
        builder.Ignore(x => x.KalanAnapara);
        builder.HasIndex(x => new { x.FirmaId, x.Kod }).IsUnique();
        builder.HasMany(x => x.Taksitler).WithOne().HasForeignKey(x => x.KrediId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Taksitler).HasField("_taksitler");
    }
}

internal sealed class KrediTaksitiConfiguration : IEntityTypeConfiguration<KrediTaksiti>
{
    public void Configure(EntityTypeBuilder<KrediTaksiti> builder)
    {
        builder.ToTable("kredi_taksiti");
        builder.Ignore(x => x.Toplam);
        builder.Ignore(x => x.OdendiMi);
        builder.HasIndex(x => new { x.KrediId, x.Sira }).IsUnique();
    }
}
