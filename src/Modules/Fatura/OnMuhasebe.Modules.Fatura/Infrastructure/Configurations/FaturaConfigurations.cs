using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnMuhasebe.Modules.Fatura.Domain;

namespace OnMuhasebe.Modules.Fatura.Infrastructure.Configurations;

internal sealed class FaturaBelgesiConfiguration : IEntityTypeConfiguration<FaturaBelgesi>
{
    public void Configure(EntityTypeBuilder<FaturaBelgesi> builder)
    {
        builder.ToTable("fatura");
        builder.Property(x => x.Tur).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.SatisSekli).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Durum).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Numara).HasMaxLength(30);
        builder.Property(x => x.CariKodu).HasMaxLength(20);
        builder.Property(x => x.CariUnvan).HasMaxLength(250);
        builder.Property(x => x.VergiNo).HasMaxLength(11);
        builder.Property(x => x.VergiDairesi).HasMaxLength(100);
        builder.Property(x => x.Il).HasMaxLength(50);
        builder.Property(x => x.Ilce).HasMaxLength(50);
        builder.Property(x => x.Adres).HasMaxLength(500);
        builder.Property(x => x.ParaBirimi).HasMaxLength(3);
        builder.Property(x => x.Kur).HasPrecision(19, FaturaBelgesi.KurOndalik);
        builder.Property(x => x.GenelIskontoTuru).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.GenelIskonto).HasPrecision(18, 4);
        builder.Property(x => x.StopajOrani).HasPrecision(5, 2);
        builder.Property(x => x.Aciklama).HasMaxLength(1000);
        builder.Ignore(x => x.GidenMi);

        // Giden faturanın numarası firma içinde tekildir (seri + yıl + sıra). Gelen faturada numara karşı tarafındır
        // ve farklı tedarikçilerde aynı olabilir; o kontrol cari bazında uygulamada yapılır.
        builder.HasIndex(x => new { x.FirmaId, x.Numara }).IsUnique()
            .HasFilter("numara IS NOT NULL AND tur IN ('Satis', 'AlistanIade', 'GiderPusulasi')");
        builder.HasIndex(x => new { x.FirmaId, x.Tarih });
        builder.HasIndex(x => new { x.FirmaId, x.CariHesapId });
        builder.HasMany(x => x.Satirlar).WithOne().HasForeignKey(x => x.FaturaId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Satirlar).HasField("_satirlar");
    }
}

internal sealed class FaturaSatiriConfiguration : IEntityTypeConfiguration<FaturaSatiri>
{
    public void Configure(EntityTypeBuilder<FaturaSatiri> builder)
    {
        builder.ToTable("fatura_satiri");
        builder.Property(x => x.StokKodu).HasMaxLength(30);
        builder.Property(x => x.Ad).HasMaxLength(250);
        builder.Property(x => x.Birim).HasMaxLength(10);
        builder.Property(x => x.Miktar).HasPrecision(18, 4);
        builder.Property(x => x.BirimFiyat).HasPrecision(18, 4);
        builder.Property(x => x.Iskonto1).HasPrecision(5, 2);
        builder.Property(x => x.Iskonto2).HasPrecision(5, 2);
        builder.Property(x => x.Iskonto3).HasPrecision(5, 2);
        builder.Property(x => x.TevkifatKodu).HasMaxLength(5);
        builder.HasIndex(x => x.StokKartiId);
    }
}

internal sealed class FaturaAyariConfiguration : IEntityTypeConfiguration<FaturaAyari>
{
    public void Configure(EntityTypeBuilder<FaturaAyari> builder)
    {
        builder.ToTable("fatura_ayari");
        builder.Property(x => x.KayitAkisi).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ToptanSeri).HasMaxLength(3);
        builder.Property(x => x.PerakendeSeri).HasMaxLength(3);
        builder.Property(x => x.IadeSeri).HasMaxLength(3);
        builder.Property(x => x.GiderPusulasiSeri).HasMaxLength(3);
        builder.HasIndex(x => x.FirmaId).IsUnique();
    }
}

internal sealed class FaturaSeriSayaciConfiguration : IEntityTypeConfiguration<FaturaSeriSayaci>
{
    public void Configure(EntityTypeBuilder<FaturaSeriSayaci> builder)
    {
        builder.ToTable("fatura_seri_sayaci");
        builder.HasKey(x => new { x.FirmaId, x.Seri, x.Yil });
        builder.Property(x => x.Seri).HasMaxLength(3);
    }
}
