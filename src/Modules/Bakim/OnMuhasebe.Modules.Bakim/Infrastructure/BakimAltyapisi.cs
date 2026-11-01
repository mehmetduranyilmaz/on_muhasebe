using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnMuhasebe.Modules.Bakim.Contracts;
using OnMuhasebe.Modules.Bakim.Domain;
using OnMuhasebe.Shared.Contracts.Bildirimler;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Infrastructure.Bildirimler;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Bakim.Infrastructure;

internal sealed class BakimDbContext(DbContextOptions<BakimDbContext> options, IMevcutKullanici mevcutKullanici)
    : ModulDbContext(options, mevcutKullanici)
{
    protected override string Sema => "bakim";

    public DbSet<Ekipman> Ekipmanlar => Set<Ekipman>();
    public DbSet<BakimPlani> Planlar => Set<BakimPlani>();
    public DbSet<IsEmri> IsEmirleri => Set<IsEmri>();
}

internal sealed class EkipmanConfiguration : IEntityTypeConfiguration<Ekipman>
{
    public void Configure(EntityTypeBuilder<Ekipman> builder)
    {
        builder.ToTable("ekipman");
        builder.Property(x => x.Kod).HasMaxLength(20);
        builder.Property(x => x.Ad).HasMaxLength(200);
        builder.Property(x => x.Tur).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Marka).HasMaxLength(100);
        builder.Property(x => x.Model).HasMaxLength(100);
        builder.Property(x => x.SeriNo).HasMaxLength(100);
        builder.Property(x => x.Konum).HasMaxLength(200);
        builder.Property(x => x.SayacBirimi).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Sayac).HasPrecision(14, 2);
        builder.Property(x => x.Durum).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Aciklama).HasMaxLength(1000);
        builder.HasIndex(x => new { x.FirmaId, x.Kod }).IsUnique();
    }
}

internal sealed class BakimPlaniConfiguration : IEntityTypeConfiguration<BakimPlani>
{
    public void Configure(EntityTypeBuilder<BakimPlani> builder)
    {
        builder.ToTable("bakim_plani");
        builder.Property(x => x.Ad).HasMaxLength(200);
        builder.Property(x => x.KontrolKurulusu).HasMaxLength(200);
        builder.Property(x => x.PeriyotSayac).HasPrecision(14, 2);
        builder.Property(x => x.SonYapilmaSayaci).HasPrecision(14, 2);
        builder.Property(x => x.SonrakiSayac).HasPrecision(14, 2);
        builder.HasOne<Ekipman>().WithMany().HasForeignKey(x => x.EkipmanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.EkipmanId);
    }
}

internal sealed class IsEmriConfiguration : IEntityTypeConfiguration<IsEmri>
{
    public void Configure(EntityTypeBuilder<IsEmri> builder)
    {
        builder.ToTable("is_emri");
        builder.Property(x => x.Kod).HasMaxLength(20);
        builder.Property(x => x.Tur).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Durum).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Baslik).HasMaxLength(200);
        builder.Property(x => x.Aciklama).HasMaxLength(2000);
        builder.Property(x => x.IscilikTutari).HasPrecision(18, 2);
        builder.Property(x => x.DisServisTutari).HasPrecision(18, 2);
        builder.Property(x => x.DisServisFirma).HasMaxLength(200);
        builder.Property(x => x.KontrolKurulusu).HasMaxLength(200);
        builder.Property(x => x.RaporNo).HasMaxLength(100);
        builder.Property(x => x.SayacDegeri).HasPrecision(14, 2);
        builder.Ignore(x => x.AcikMi);
        builder.Ignore(x => x.DurusSaati);
        builder.HasOne<Ekipman>().WithMany().HasForeignKey(x => x.EkipmanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Parcalar).WithOne().HasForeignKey(x => x.IsEmriId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Parcalar).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(x => new { x.FirmaId, x.Kod }).IsUnique();
        builder.HasIndex(x => new { x.EkipmanId, x.Durum });
    }
}

internal sealed class IsEmriParcasiConfiguration : IEntityTypeConfiguration<IsEmriParcasi>
{
    public void Configure(EntityTypeBuilder<IsEmriParcasi> builder)
    {
        builder.ToTable("is_emri_parcasi");
        builder.Property(x => x.Ad).HasMaxLength(250);
        builder.Property(x => x.Birim).HasMaxLength(20);
        builder.Property(x => x.Miktar).HasPrecision(18, 4);
        builder.Property(x => x.BirimFiyat).HasPrecision(18, 4);
        builder.HasIndex(x => x.IsEmriId);
    }
}

// Yaklaşan ve geciken bakımlar. Sorumlu kişi tercihinden bağımsız alır. Yasal kontroller daha önemlidir.
internal sealed class BakimBildirimKaynagi(BakimDbContext db) : IBildirimKaynagi
{
    private static readonly CultureInfo _tr = CultureInfo.GetCultureInfo("tr-TR");

    public string? ModulKodu => ModulKodlari.Bakim;

    public async Task<IReadOnlyList<BildirimAdayi>> AdaylarAsync(BildirimBaglami baglam, CancellationToken cancellationToken)
    {
        var bugun = baglam.Bugun;
        var planlar = await (
                from p in db.Planlar.AsNoTracking()
                join e in db.Ekipmanlar on p.EkipmanId equals e.Id
                where p.AktifMi && e.Durum != EkipmanDurumu.HizmetDisi
                select new { p.Id, p.EkipmanId, e.Kod, EkipmanAdi = e.Ad, e.Sayac, e.SorumluKullaniciId, p.Ad, p.YasalMi, p.SonrakiTarih, p.SonrakiSayac, p.PeriyotSayac, p.HatirlatmaGun })
            .ToListAsync(cancellationToken);

        var adaylar = new List<BildirimAdayi>();
        foreach (var p in planlar)
        {
            var baglanti = $"bakim/{p.EkipmanId}";
            if (p.SonrakiTarih is { } tarih && SureliHatirlatma.Asama(tarih, p.HatirlatmaGun, bugun) is { } asama)
            {
                var kalan = tarih.DayNumber - bugun.DayNumber;
                var gecti = asama == SureliHatirlatma.Doldu;
                var baslik = gecti ? $"{p.EkipmanAdi}: {p.Ad} gecikti" : $"{p.EkipmanAdi}: {p.Ad} için {kalan} gün kaldı";
                var metin = $"{p.Kod} · {(p.YasalMi ? "Yasal periyodik kontrol · " : "")}{tarih.ToString("dd.MM.yyyy", _tr)}";
                var anahtar = $"bakim:{p.Id}:{tarih.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}:{asama}";
                adaylar.Add(new BildirimAdayi(BildirimTuru.BakimSuresi, anahtar, baslik, metin, baglanti,
                    p.YasalMi || asama != SureliHatirlatma.Yaklasiyor ? BildirimOnemi.Uyari : BildirimOnemi.Bilgi, p.SorumluKullaniciId));
            }

            if (p.SonrakiSayac is { } hedef && p.Sayac is { } sayac)
            {
                var durum = BakimHesaplari.Durum(null, 0, hedef, p.PeriyotSayac, sayac, bugun);
                if (durum != PlanDurumu.Zamaninda)
                {
                    var hedefMetni = hedef.ToString("0.##", CultureInfo.InvariantCulture);
                    adaylar.Add(new BildirimAdayi(BildirimTuru.BakimSuresi, $"bakim-sayac:{p.Id}:{hedefMetni}:{durum}",
                        durum == PlanDurumu.Gecikti ? $"{p.EkipmanAdi}: {p.Ad} sayacı geçti" : $"{p.EkipmanAdi}: {p.Ad} sayacı yaklaşıyor",
                        $"{p.Kod} · sayaç {sayac.ToString("N0", _tr)} / {hedef.ToString("N0", _tr)}", baglanti,
                        p.YasalMi || durum == PlanDurumu.Gecikti ? BildirimOnemi.Uyari : BildirimOnemi.Bilgi, p.SorumluKullaniciId));
                }
            }
        }

        return adaylar;
    }
}
