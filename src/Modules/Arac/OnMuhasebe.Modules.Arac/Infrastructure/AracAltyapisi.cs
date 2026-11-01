using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnMuhasebe.Modules.Arac.Contracts;
using OnMuhasebe.Modules.Arac.Domain;
using OnMuhasebe.Shared.Contracts.Bildirimler;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Infrastructure.Bildirimler;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Arac.Infrastructure;

internal sealed class AracDbContext(DbContextOptions<AracDbContext> options, IMevcutKullanici mevcutKullanici)
    : ModulDbContext(options, mevcutKullanici)
{
    protected override string Sema => "arac";

    public DbSet<AracKarti> Araclar => Set<AracKarti>();
    public DbSet<AracYukumlulugu> Yukumlulukler => Set<AracYukumlulugu>();
    public DbSet<AracMtv> MtvKayitlari => Set<AracMtv>();
    public DbSet<AracMasrafi> Masraflar => Set<AracMasrafi>();
    public DbSet<AracHasari> Hasarlar => Set<AracHasari>();
}

internal sealed class AracKartiConfiguration : IEntityTypeConfiguration<AracKarti>
{
    public void Configure(EntityTypeBuilder<AracKarti> builder)
    {
        builder.ToTable("arac_karti");
        builder.Property(x => x.Plaka).HasMaxLength(20);
        builder.Property(x => x.Marka).HasMaxLength(100);
        builder.Property(x => x.Model).HasMaxLength(100);
        builder.Property(x => x.Tur).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Yakit).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Mulkiyet).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Durum).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.SasiNo).HasMaxLength(30);
        builder.Property(x => x.MotorNo).HasMaxLength(30);
        builder.Property(x => x.RuhsatSahibi).HasMaxLength(200);
        builder.Property(x => x.Surucu).HasMaxLength(150);
        builder.Property(x => x.SurucuTelefon).HasMaxLength(30);
        builder.Property(x => x.Aciklama).HasMaxLength(1000);

        // Silinen aracın plakası yeniden kullanılabilir.
        builder.HasIndex(x => new { x.FirmaId, x.Plaka }).IsUnique().HasFilter("silindi_mi = false");
    }
}

internal sealed class AracYukumluluguConfiguration : IEntityTypeConfiguration<AracYukumlulugu>
{
    public void Configure(EntityTypeBuilder<AracYukumlulugu> builder)
    {
        builder.ToTable("arac_yukumlulugu");
        builder.Property(x => x.Tur).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Ad).HasMaxLength(100);
        builder.Property(x => x.BelgeNo).HasMaxLength(100);
        builder.Property(x => x.Sirket).HasMaxLength(200);
        builder.Property(x => x.Tutar).HasPrecision(18, 2);
        builder.HasOne<AracKarti>().WithMany().HasForeignKey(x => x.AracId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.FirmaId, x.BitisTarihi });
        builder.HasIndex(x => x.AracId);
    }
}

internal sealed class AracMtvConfiguration : IEntityTypeConfiguration<AracMtv>
{
    public void Configure(EntityTypeBuilder<AracMtv> builder)
    {
        builder.ToTable("arac_mtv");
        builder.Property(x => x.Tutar).HasPrecision(18, 2);
        builder.HasOne<AracKarti>().WithMany().HasForeignKey(x => x.AracId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.AracId, x.Yil, x.Taksit }).IsUnique();
    }
}

internal sealed class AracMasrafiConfiguration : IEntityTypeConfiguration<AracMasrafi>
{
    public void Configure(EntityTypeBuilder<AracMasrafi> builder)
    {
        builder.ToTable("arac_masrafi");
        builder.Property(x => x.Tur).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Tutar).HasPrecision(18, 2);
        builder.Property(x => x.Litre).HasPrecision(12, 3);
        builder.Property(x => x.Tedarikci).HasMaxLength(200);
        builder.Property(x => x.BelgeNo).HasMaxLength(100);
        builder.Property(x => x.Aciklama).HasMaxLength(500);
        builder.HasOne<AracKarti>().WithMany().HasForeignKey(x => x.AracId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.AracId, x.Tarih });
    }
}

internal sealed class AracHasariConfiguration : IEntityTypeConfiguration<AracHasari>
{
    public void Configure(EntityTypeBuilder<AracHasari> builder)
    {
        builder.ToTable("arac_hasari");
        builder.Property(x => x.Kusur).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Aciklama).HasMaxLength(1000);
        builder.Property(x => x.OnarimTutari).HasPrecision(18, 2);
        builder.Property(x => x.SigortaOdemesi).HasPrecision(18, 2);
        builder.Property(x => x.DosyaNo).HasMaxLength(100);
        builder.HasOne<AracKarti>().WithMany().HasForeignKey(x => x.AracId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.AracId, x.Tarih });
    }
}

// Aktif araçların süresi yaklaşan/dolan yükümlülükleri ve ödenmemiş MTV taksitleri. Evrak Takibi lisansı gerekmez.
internal sealed class AracBildirimKaynagi(AracDbContext db) : IBildirimKaynagi
{
    private const int MtvHatirlatmaGunu = 15;

    private static readonly CultureInfo _tr = CultureInfo.GetCultureInfo("tr-TR");

    public string? ModulKodu => ModulKodlari.Arac;

    public async Task<IReadOnlyList<BildirimAdayi>> AdaylarAsync(BildirimBaglami baglam, CancellationToken cancellationToken)
    {
        var bugun = baglam.Bugun;
        var bas = bugun.AddDays(-SureliHatirlatma.GecikmeGunu);
        var yukumlulukler = await (
                from y in db.Yukumlulukler.AsNoTracking()
                join a in db.Araclar on y.AracId equals a.Id
                where a.Durum == AracDurumu.Aktif && !y.YenilendiMi && y.BitisTarihi >= bas && y.BitisTarihi <= bugun.AddDays(y.HatirlatmaGun)
                select new { y.Id, y.AracId, a.Plaka, y.Tur, y.Ad, y.BitisTarihi, y.HatirlatmaGun })
            .ToListAsync(cancellationToken);

        var adaylar = new List<BildirimAdayi>();
        foreach (var y in yukumlulukler)
        {
            var ad = y.Tur == YukumlulukTuru.Diger && y.Ad is not null ? y.Ad : AracMetinleri.Yukumluluk(y.Tur);
            Ekle(adaylar, $"arac-sure:{y.Id}", y.Plaka, ad, y.AracId, y.BitisTarihi, y.HatirlatmaGun, bugun);
        }

        var mtvler = await (
                from m in db.MtvKayitlari.AsNoTracking()
                join a in db.Araclar on m.AracId equals a.Id
                where a.Durum == AracDurumu.Aktif && m.OdemeTarihi == null && m.SonOdemeTarihi >= bas && m.SonOdemeTarihi <= bugun.AddDays(MtvHatirlatmaGunu)
                select new { m.Id, m.AracId, a.Plaka, m.Yil, m.Taksit, m.SonOdemeTarihi })
            .ToListAsync(cancellationToken);
        foreach (var m in mtvler)
        {
            Ekle(adaylar, $"arac-mtv:{m.Id}", m.Plaka, $"MTV {m.Yil}/{m.Taksit}", m.AracId, m.SonOdemeTarihi, MtvHatirlatmaGunu, bugun);
        }

        return adaylar;
    }

    private static void Ekle(List<BildirimAdayi> adaylar, string onek, string plaka, string ad, Guid aracId, DateOnly bitis, int hatirlatmaGun, DateOnly bugun)
    {
        if (SureliHatirlatma.Asama(bitis, hatirlatmaGun, bugun) is not { } asama)
        {
            return;
        }

        var kalan = bitis.DayNumber - bugun.DayNumber;
        var doldu = asama == SureliHatirlatma.Doldu;
        var baslik = doldu ? $"{plaka}: {ad} süresi doldu" : $"{plaka}: {ad} için {kalan} gün kaldı";
        var metin = doldu ? $"{bitis.ToString("dd.MM.yyyy", _tr)} tarihinde bitti" : $"Bitiş {bitis.ToString("dd.MM.yyyy", _tr)}";
        adaylar.Add(new BildirimAdayi(BildirimTuru.AracSuresi, $"{onek}:{bitis.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}:{asama}", baslik, metin, $"arac/{aracId}",
            asama == SureliHatirlatma.Yaklasiyor ? BildirimOnemi.Bilgi : BildirimOnemi.Uyari));
    }
}
