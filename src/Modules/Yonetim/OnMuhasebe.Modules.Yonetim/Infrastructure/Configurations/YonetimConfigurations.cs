using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnMuhasebe.Modules.Yonetim.Domain;

namespace OnMuhasebe.Modules.Yonetim.Infrastructure.Configurations;

internal sealed class FirmaConfiguration : IEntityTypeConfiguration<Firma>
{
    public void Configure(EntityTypeBuilder<Firma> builder)
    {
        builder.ToTable("firma");
        builder.Property(x => x.Unvan).HasMaxLength(250);
        builder.Property(x => x.VergiNo).HasMaxLength(11);
        builder.Property(x => x.VergiDairesi).HasMaxLength(100);
        builder.Property(x => x.Il).HasMaxLength(50);
        builder.Property(x => x.Ilce).HasMaxLength(50);
        builder.Property(x => x.Adres).HasMaxLength(500);
        builder.Property(x => x.Telefon).HasMaxLength(30);
        builder.Property(x => x.Eposta).HasMaxLength(256);
    }
}

internal sealed class KullaniciConfiguration : IEntityTypeConfiguration<Kullanici>
{
    public void Configure(EntityTypeBuilder<Kullanici> builder)
    {
        builder.ToTable("kullanici");
        builder.Property(x => x.Ad).HasMaxLength(100);
        builder.Property(x => x.Soyad).HasMaxLength(100);
        builder.Property(x => x.Eposta).HasMaxLength(256);
        builder.Property(x => x.SifreOzeti).HasMaxLength(500);
        builder.HasIndex(x => x.Eposta).IsUnique();
    }
}

internal sealed class FirmaKullanicisiConfiguration : IEntityTypeConfiguration<FirmaKullanicisi>
{
    public void Configure(EntityTypeBuilder<FirmaKullanicisi> builder)
    {
        builder.ToTable("firma_kullanicisi");
        builder.Property(x => x.Rol).HasMaxLength(20);
        builder.HasIndex(x => new { x.FirmaId, x.KullaniciId }).IsUnique();
        builder.HasIndex(x => x.KullaniciId);
        builder.HasOne(x => x.Firma).WithMany().HasForeignKey(x => x.FirmaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Kullanici).WithMany().HasForeignKey(x => x.KullaniciId).OnDelete(DeleteBehavior.Restrict);
    }
}

// Paket başlangıç verisi HasData ile değil migration içinde eklenir: HasData liste kolonlarını
// referansla karşılaştırdığı için model her seferinde "değişmiş" görünür.
internal sealed class PaketConfiguration : IEntityTypeConfiguration<Paket>
{
    public void Configure(EntityTypeBuilder<Paket> builder)
    {
        builder.ToTable("paket");
        builder.Property(x => x.Kod).HasMaxLength(50);
        builder.Property(x => x.Ad).HasMaxLength(100);
        builder.HasIndex(x => x.Kod).IsUnique();
    }
}

internal sealed class FirmaLisansiConfiguration : IEntityTypeConfiguration<FirmaLisansi>
{
    public void Configure(EntityTypeBuilder<FirmaLisansi> builder)
    {
        builder.ToTable("firma_lisansi");
        builder.HasIndex(x => x.FirmaId).IsUnique();
        builder.HasOne(x => x.Paket).WithMany().HasForeignKey(x => x.PaketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Firma>().WithMany().HasForeignKey(x => x.FirmaId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_token");
        builder.Property(x => x.TokenOzeti).HasMaxLength(64);
        builder.HasIndex(x => x.TokenOzeti).IsUnique();
        builder.HasIndex(x => x.KullaniciId);
        builder.HasOne<Kullanici>().WithMany().HasForeignKey(x => x.KullaniciId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class FirmaZorunluAlaniConfiguration : IEntityTypeConfiguration<FirmaZorunluAlani>
{
    public void Configure(EntityTypeBuilder<FirmaZorunluAlani> builder)
    {
        builder.ToTable("firma_zorunlu_alani");
        builder.Property(x => x.Kod).HasMaxLength(80);
        builder.HasIndex(x => new { x.FirmaId, x.Kod }).IsUnique();
    }
}

internal sealed class IstekKaydiConfiguration : IEntityTypeConfiguration<IstekKaydi>
{
    public void Configure(EntityTypeBuilder<IstekKaydi> builder)
    {
        builder.ToTable("istek_kaydi");
        builder.Property(x => x.Anahtar).HasMaxLength(100);
        builder.Property(x => x.Ozet).HasMaxLength(64);
        builder.Property(x => x.IcerikTuru).HasMaxLength(200);
        builder.Property(x => x.Konum).HasMaxLength(500);
        builder.HasIndex(x => new { x.FirmaId, x.KullaniciId, x.Anahtar }).IsUnique();
        builder.HasIndex(x => x.OlusturmaTarihi);
    }
}

internal sealed class DonemKilidiConfiguration : IEntityTypeConfiguration<DonemKilidi>
{
    public void Configure(EntityTypeBuilder<DonemKilidi> builder)
    {
        builder.ToTable("donem_kilidi");
        builder.Property(x => x.Aciklama).HasMaxLength(DonemKilidi.AciklamaUzunlugu);
        builder.HasIndex(x => x.FirmaId).IsUnique();
    }
}
