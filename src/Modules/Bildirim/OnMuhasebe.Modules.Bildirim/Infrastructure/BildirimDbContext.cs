using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnMuhasebe.Modules.Bildirim.Domain;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Bildirim.Infrastructure;

internal sealed class BildirimDbContext(DbContextOptions<BildirimDbContext> options, IMevcutKullanici mevcutKullanici)
    : ModulDbContext(options, mevcutKullanici)
{
    protected override string Sema => "bildirim";

    public DbSet<Domain.Bildirim> Bildirimler => Set<Domain.Bildirim>();
    public DbSet<BildirimAyari> Ayarlar => Set<BildirimAyari>();
    public DbSet<KullaniciTercihi> Tercihler => Set<KullaniciTercihi>();
    public DbSet<EpostaAyari> EpostaAyarlari => Set<EpostaAyari>();
}

internal sealed class BildirimConfiguration : IEntityTypeConfiguration<Domain.Bildirim>
{
    public void Configure(EntityTypeBuilder<Domain.Bildirim> builder)
    {
        builder.ToTable("bildirim");
        builder.Property(x => x.Tur).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Onem).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Eposta).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.Anahtar).HasMaxLength(Domain.Bildirim.AnahtarUzunlugu);
        builder.Property(x => x.Baslik).HasMaxLength(Domain.Bildirim.BaslikUzunlugu);
        builder.Property(x => x.Metin).HasMaxLength(Domain.Bildirim.MetinUzunlugu);
        builder.Property(x => x.Baglanti).HasMaxLength(Domain.Bildirim.BaglantiUzunlugu);
        builder.Ignore(x => x.OkunduMu);
        builder.HasIndex(x => new { x.FirmaId, x.KullaniciId, x.Anahtar }).IsUnique();
        builder.HasIndex(x => new { x.FirmaId, x.KullaniciId, x.OkunmaTarihi });
        builder.HasIndex(x => x.Eposta).HasFilter("eposta = 'Bekliyor'");
    }
}

internal sealed class BildirimAyariConfiguration : IEntityTypeConfiguration<BildirimAyari>
{
    public void Configure(EntityTypeBuilder<BildirimAyari> builder)
    {
        builder.ToTable("bildirim_ayari");
        builder.HasIndex(x => x.FirmaId).IsUnique();
    }
}

internal sealed class KullaniciTercihiConfiguration : IEntityTypeConfiguration<KullaniciTercihi>
{
    public void Configure(EntityTypeBuilder<KullaniciTercihi> builder)
    {
        builder.ToTable("kullanici_tercihi");
        builder.Property(x => x.Eposta).HasConversion<string>().HasMaxLength(15);
        builder.HasIndex(x => new { x.FirmaId, x.KullaniciId }).IsUnique();
    }
}

internal sealed class EpostaAyariConfiguration : IEntityTypeConfiguration<EpostaAyari>
{
    public void Configure(EntityTypeBuilder<EpostaAyari> builder)
    {
        builder.ToTable("eposta_ayari");
        builder.Property(x => x.Guvenlik).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Sunucu).HasMaxLength(200);
        builder.Property(x => x.KullaniciAdi).HasMaxLength(200);
        builder.Property(x => x.SifreSifreli).HasMaxLength(500);
        builder.Property(x => x.GonderenAdres).HasMaxLength(256);
        builder.Property(x => x.GonderenAd).HasMaxLength(100);
    }
}
