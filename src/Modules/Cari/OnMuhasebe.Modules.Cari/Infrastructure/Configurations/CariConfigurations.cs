using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnMuhasebe.Modules.Cari.Domain;

namespace OnMuhasebe.Modules.Cari.Infrastructure.Configurations;

internal sealed class CariHesapConfiguration : IEntityTypeConfiguration<CariHesap>
{
    public void Configure(EntityTypeBuilder<CariHesap> builder)
    {
        builder.ToTable("cari_hesap");
        builder.Property(x => x.Kod).HasMaxLength(20);
        builder.Property(x => x.Unvan).HasMaxLength(250);
        builder.Property(x => x.Tur).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.KisiTipi).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.VergiNo).HasMaxLength(11);
        builder.Property(x => x.VergiDairesi).HasMaxLength(100);
        builder.Property(x => x.Il).HasMaxLength(50);
        builder.Property(x => x.Ilce).HasMaxLength(50);
        builder.Property(x => x.Adres).HasMaxLength(500);
        builder.Property(x => x.Telefon).HasMaxLength(30);
        builder.Property(x => x.Eposta).HasMaxLength(256);
        builder.Property(x => x.ParaBirimi).HasMaxLength(3);
        builder.Property(x => x.Ad).HasMaxLength(100);
        builder.Property(x => x.Soyad).HasMaxLength(100);
        builder.Property(x => x.Ulke).HasMaxLength(60);
        builder.Property(x => x.PostaKodu).HasMaxLength(10);
        builder.Property(x => x.CepTelefonu).HasMaxLength(30);
        builder.Property(x => x.Faks).HasMaxLength(30);
        builder.Property(x => x.WebSitesi).HasMaxLength(200);
        builder.Property(x => x.EFaturaEtiketi).HasMaxLength(100);
        builder.Property(x => x.VarsayilanKdvIstisnaKodu).HasMaxLength(5);
        builder.Property(x => x.VarsayilanTevkifatKodu).HasMaxLength(5);
        builder.Property(x => x.Not).HasMaxLength(1000);
        builder.Property(x => x.VadeFarkiOrani).HasPrecision(6, 2);

        // Silinmiş carilerin kodları da benzersizlikte sayılır; eski kayıtlarla karışmasın.
        builder.HasIndex(x => new { x.FirmaId, x.Kod }).IsUnique();
        builder.HasIndex(x => new { x.FirmaId, x.Unvan });
        builder.HasOne(x => x.Grup).WithMany().HasForeignKey(x => x.GrupId).OnDelete(DeleteBehavior.Restrict);

        // Alt kayıtlar kartla birlikte yazılır; firma_id'leri yok, satır güvenliği üst kayıttan (dış anahtar) türetilir.
        builder.HasMany(x => x.Adresler).WithOne().HasForeignKey(x => x.CariHesapId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Adresler).HasField("_adresler");
        builder.HasMany(x => x.IlgiliKisiler).WithOne().HasForeignKey(x => x.CariHesapId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.IlgiliKisiler).HasField("_ilgiliKisiler");
        builder.HasMany(x => x.BankaHesaplari).WithOne().HasForeignKey(x => x.CariHesapId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.BankaHesaplari).HasField("_bankaHesaplari");
    }
}

internal sealed class CariAdresConfiguration : IEntityTypeConfiguration<CariAdres>
{
    public void Configure(EntityTypeBuilder<CariAdres> builder)
    {
        builder.ToTable("cari_adres");
        builder.Property(x => x.Baslik).HasMaxLength(60);
        builder.Property(x => x.AdSoyad).HasMaxLength(100);
        builder.Property(x => x.Telefon).HasMaxLength(30);
        builder.Property(x => x.Ulke).HasMaxLength(60);
        builder.Property(x => x.Il).HasMaxLength(50);
        builder.Property(x => x.Ilce).HasMaxLength(50);
        builder.Property(x => x.Adres).HasMaxLength(500);
        builder.Property(x => x.PostaKodu).HasMaxLength(10);
        builder.HasIndex(x => new { x.CariHesapId, x.Sira });
    }
}

internal sealed class CariIlgiliKisiConfiguration : IEntityTypeConfiguration<CariIlgiliKisi>
{
    public void Configure(EntityTypeBuilder<CariIlgiliKisi> builder)
    {
        builder.ToTable("cari_ilgili_kisi");
        builder.Property(x => x.AdSoyad).HasMaxLength(100);
        builder.Property(x => x.Gorev).HasMaxLength(60);
        builder.Property(x => x.Telefon).HasMaxLength(30);
        builder.Property(x => x.Eposta).HasMaxLength(256);
        builder.HasIndex(x => new { x.CariHesapId, x.Sira });
    }
}

internal sealed class CariBankaHesabiConfiguration : IEntityTypeConfiguration<CariBankaHesabi>
{
    public void Configure(EntityTypeBuilder<CariBankaHesabi> builder)
    {
        builder.ToTable("cari_banka_hesabi");
        builder.Property(x => x.BankaAdi).HasMaxLength(100);
        builder.Property(x => x.Sube).HasMaxLength(100);
        builder.Property(x => x.HesapNo).HasMaxLength(30);
        builder.Property(x => x.Iban).HasMaxLength(34);
        builder.Property(x => x.ParaBirimi).HasMaxLength(3);
        builder.HasIndex(x => new { x.CariHesapId, x.Sira });
        // Banka ekstresinde açıklamadaki IBAN'dan cari bulunur.
        builder.HasIndex(x => x.Iban);
    }
}

internal sealed class CariHareketConfiguration : IEntityTypeConfiguration<CariHareket>
{
    public void Configure(EntityTypeBuilder<CariHareket> builder)
    {
        builder.ToTable("cari_hareket");
        builder.Property(x => x.Tur).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.BelgeNo).HasMaxLength(50);
        builder.Property(x => x.Aciklama).HasMaxLength(500);
        builder.Property(x => x.ParaBirimi).HasMaxLength(3);
        builder.Property(x => x.Kur).HasPrecision(19, CariHareket.KurOndalik);
        builder.Ignore(x => x.IptalEdildiMi);
        builder.Ignore(x => x.IptalKaydiMi);

        // Bakiye ve ekstre sorguları tabloya gitmeden yalnızca bu index'ten karşılanır (index-only scan).
        builder.HasIndex(x => new { x.FirmaId, x.CariHesapId, x.Tarih })
            .IncludeProperties(x => new { x.Borc, x.Alacak, x.BorcTl, x.AlacakTl });
        builder.HasIndex(x => x.KaynakBelgeId);
        builder.HasOne<CariHesap>().WithMany().HasForeignKey(x => x.CariHesapId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CariGrupConfiguration : IEntityTypeConfiguration<CariGrup>
{
    public void Configure(EntityTypeBuilder<CariGrup> builder)
    {
        builder.ToTable("cari_grubu");
        builder.Property(x => x.Ad).HasMaxLength(100);
        builder.HasIndex(x => new { x.FirmaId, x.Ad }).IsUnique();
    }
}

internal sealed class CariKapamaConfiguration : IEntityTypeConfiguration<CariKapama>
{
    public void Configure(EntityTypeBuilder<CariKapama> builder)
    {
        builder.ToTable("cari_kapama");
        builder.HasIndex(x => new { x.FirmaId, x.CariHesapId });
        builder.HasIndex(x => x.BorcHareketId);
        builder.HasIndex(x => x.AlacakHareketId);
        builder.HasOne<CariHareket>().WithMany().HasForeignKey(x => x.BorcHareketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CariHareket>().WithMany().HasForeignKey(x => x.AlacakHareketId).OnDelete(DeleteBehavior.Restrict);
    }
}
