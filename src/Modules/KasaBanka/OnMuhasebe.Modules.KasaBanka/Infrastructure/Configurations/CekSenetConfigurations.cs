using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnMuhasebe.Modules.KasaBanka.Domain;
using OnMuhasebe.Modules.KasaBanka.Domain.CekSenet;

namespace OnMuhasebe.Modules.KasaBanka.Infrastructure.Configurations;

internal sealed class EvrakConfiguration : IEntityTypeConfiguration<Evrak>
{
    public void Configure(EntityTypeBuilder<Evrak> builder)
    {
        builder.ToTable("evrak");
        builder.Property(x => x.Kod).HasMaxLength(10);
        builder.Property(x => x.Tur).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Yon).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Durum).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.SeriNo).HasMaxLength(30);
        builder.Property(x => x.ParaBirimi).HasMaxLength(3);
        builder.Property(x => x.Tutar).HasPrecision(18, 2);
        builder.Property(x => x.TahsilEdilen).HasPrecision(18, 2);
        builder.Property(x => x.CariTutar).HasPrecision(18, 2);
        builder.Property(x => x.CiroCariTutar).HasPrecision(18, 2);
        builder.Property(x => x.Kur).HasPrecision(19, KasaHareketi.KurOndalik);
        builder.Property(x => x.CariKur).HasPrecision(19, KasaHareketi.KurOndalik);
        builder.Property(x => x.CiroCariKur).HasPrecision(19, KasaHareketi.KurOndalik);
        builder.Property(x => x.BorcluAdi).HasMaxLength(200);
        builder.Property(x => x.BorcluKimlikNo).HasMaxLength(11);
        builder.Property(x => x.BankaAdi).HasMaxLength(100);
        builder.Property(x => x.Sube).HasMaxLength(100);
        builder.Property(x => x.HesapNo).HasMaxLength(50);
        builder.Property(x => x.KesideYeri).HasMaxLength(100);
        builder.Property(x => x.KefilAdi).HasMaxLength(200);
        builder.Property(x => x.KefilKimlikNo).HasMaxLength(11);
        builder.Property(x => x.Karekod).HasMaxLength(500);
        builder.Property(x => x.Aciklama).HasMaxLength(500);
        builder.Ignore(x => x.KalanTutar);
        builder.Ignore(x => x.GeriAlinacak);

        builder.HasIndex(x => new { x.FirmaId, x.Kod }).IsUnique();
        builder.HasIndex(x => new { x.FirmaId, x.Yon, x.Durum, x.VadeTarihi });
        builder.HasIndex(x => new { x.FirmaId, x.CariHesapId });
        builder.HasIndex(x => new { x.FirmaId, x.BorcluId });
        builder.HasIndex(x => new { x.FirmaId, x.Karekod }).HasFilter("karekod IS NOT NULL");
        builder.HasOne<EvrakBorclusu>().WithMany().HasForeignKey(x => x.BorcluId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<KasaHesabi>().WithMany().HasForeignKey(x => x.KendiHesapId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<KasaHesabi>().WithMany().HasForeignKey(x => x.KonumHesapId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EvrakKarnesi>().WithMany().HasForeignKey(x => x.KarneId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.KarneId, x.SeriNo }).HasFilter("karne_id IS NOT NULL");
        builder.HasMany(x => x.Hareketler).WithOne().HasForeignKey(x => x.EvrakId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.Hareketler).HasField("_hareketler");
    }
}

internal sealed class EvrakHareketiConfiguration : IEntityTypeConfiguration<EvrakHareketi>
{
    public void Configure(EntityTypeBuilder<EvrakHareketi> builder)
    {
        builder.ToTable("evrak_hareketi");
        builder.Property(x => x.Islem).HasConversion<string>().HasMaxLength(25);
        builder.Property(x => x.OncekiDurum).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.YeniDurum).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Tutar).HasPrecision(18, 2);
        builder.Property(x => x.OncekiCiroCariTutar).HasPrecision(18, 2);
        builder.Property(x => x.OncekiCiroCariKur).HasPrecision(19, KasaHareketi.KurOndalik);
        builder.Property(x => x.Aciklama).HasMaxLength(500);
        builder.HasIndex(x => new { x.EvrakId, x.Sira }).IsUnique();
        builder.HasIndex(x => x.BordroId);
        builder.HasIndex(x => x.IslemId).IsUnique();
        builder.HasOne<EvrakBordrosu>().WithMany().HasForeignKey(x => x.BordroId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class EvrakBordrosuConfiguration : IEntityTypeConfiguration<EvrakBordrosu>
{
    public void Configure(EntityTypeBuilder<EvrakBordrosu> builder)
    {
        builder.ToTable("evrak_bordrosu");
        builder.Property(x => x.Kod).HasMaxLength(10);
        builder.Property(x => x.Islem).HasConversion<string>().HasMaxLength(25);
        builder.Property(x => x.Aciklama).HasMaxLength(500);
        builder.HasIndex(x => new { x.FirmaId, x.Kod }).IsUnique();
    }
}

internal sealed class EvrakBorclusuConfiguration : IEntityTypeConfiguration<EvrakBorclusu>
{
    public void Configure(EntityTypeBuilder<EvrakBorclusu> builder)
    {
        builder.ToTable("evrak_borclusu");
        builder.Property(x => x.Ad).HasMaxLength(200);
        builder.Property(x => x.KimlikNo).HasMaxLength(11);
        builder.Property(x => x.Telefon).HasMaxLength(30);
        builder.Property(x => x.Adres).HasMaxLength(500);
        builder.Property(x => x.Not).HasMaxLength(500);
        builder.Property(x => x.RiskNotu).HasMaxLength(500);
        builder.HasIndex(x => new { x.FirmaId, x.KimlikNo }).IsUnique().HasFilter("kimlik_no IS NOT NULL");
        builder.HasIndex(x => new { x.FirmaId, x.Ad });
        builder.HasMany(x => x.Hesaplar).WithOne().HasForeignKey(x => x.BorcluId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Hesaplar).HasField("_hesaplar");
    }
}

internal sealed class BorcluHesabiConfiguration : IEntityTypeConfiguration<BorcluHesabi>
{
    public void Configure(EntityTypeBuilder<BorcluHesabi> builder)
    {
        builder.ToTable("borclu_hesabi");
        builder.Property(x => x.BankaAdi).HasMaxLength(100);
        builder.Property(x => x.Sube).HasMaxLength(100);
        builder.Property(x => x.HesapNo).HasMaxLength(50);
    }
}

internal sealed class CekSenetAyariConfiguration : IEntityTypeConfiguration<CekSenetAyari>
{
    public void Configure(EntityTypeBuilder<CekSenetAyari> builder)
    {
        builder.ToTable("cek_senet_ayari");
        builder.Property(x => x.RiskDavranisi).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => x.FirmaId).IsUnique();
    }
}

internal sealed class EvrakKarnesiConfiguration : IEntityTypeConfiguration<EvrakKarnesi>
{
    public void Configure(EntityTypeBuilder<EvrakKarnesi> builder)
    {
        builder.ToTable("evrak_karnesi");
        builder.Property(x => x.Tur).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Onek).HasMaxLength(20);
        builder.Property(x => x.Aciklama).HasMaxLength(500);
        builder.Ignore(x => x.BitisNo);
        builder.HasIndex(x => new { x.FirmaId, x.Tur, x.KendiHesapId });
        builder.HasOne<KasaHesabi>().WithMany().HasForeignKey(x => x.KendiHesapId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Iptaller).WithOne().HasForeignKey(x => x.KarneId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Iptaller).HasField("_iptaller");
    }
}

internal sealed class KarneYaprakIptaliConfiguration : IEntityTypeConfiguration<KarneYaprakIptali>
{
    public void Configure(EntityTypeBuilder<KarneYaprakIptali> builder)
    {
        builder.ToTable("karne_yaprak_iptali");
        builder.Property(x => x.SeriNo).HasMaxLength(30);
        builder.Property(x => x.Neden).HasMaxLength(EvrakKarnesi.NedenUzunlugu);
        builder.HasIndex(x => new { x.KarneId, x.SeriNo }).IsUnique();
    }
}
