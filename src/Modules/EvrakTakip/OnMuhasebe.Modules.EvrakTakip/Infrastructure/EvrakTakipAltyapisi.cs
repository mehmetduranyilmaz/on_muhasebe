using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnMuhasebe.Modules.EvrakTakip.Contracts;
using OnMuhasebe.Modules.EvrakTakip.Domain;
using OnMuhasebe.Shared.Contracts.Bildirimler;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Infrastructure.Bildirimler;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.EvrakTakip.Infrastructure;

internal sealed class EvrakTakipDbContext(DbContextOptions<EvrakTakipDbContext> options, IMevcutKullanici mevcutKullanici)
    : ModulDbContext(options, mevcutKullanici)
{
    protected override string Sema => "evraktakip";

    public DbSet<TakipEvraki> TakipEvraklari => Set<TakipEvraki>();
}

internal sealed class TakipEvrakiConfiguration : IEntityTypeConfiguration<TakipEvraki>
{
    public void Configure(EntityTypeBuilder<TakipEvraki> builder)
    {
        builder.ToTable("takip_evraki");
        builder.Property(x => x.Ad).HasMaxLength(200);
        builder.Property(x => x.Tur).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.BelgeNo).HasMaxLength(100);
        builder.Property(x => x.Kurum).HasMaxLength(200);
        builder.Property(x => x.Aciklama).HasMaxLength(1000);
        builder.HasIndex(x => new { x.FirmaId, x.BitisTarihi });
        builder.HasMany(x => x.Yenilemeler).WithOne().HasForeignKey(x => x.TakipEvrakiId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Yenilemeler).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class TakipEvrakiYenilemesiConfiguration : IEntityTypeConfiguration<TakipEvrakiYenilemesi>
{
    public void Configure(EntityTypeBuilder<TakipEvrakiYenilemesi> builder)
    {
        builder.ToTable("takip_evraki_yenilemesi");
        builder.Property(x => x.EskiBelgeNo).HasMaxLength(100);
        builder.Property(x => x.Not).HasMaxLength(500);
        builder.Property(x => x.Yapan).HasMaxLength(256);
        builder.HasIndex(x => x.TakipEvrakiId);
    }
}

// Bitişi yaklaşan ve dolan evraklar. Evrakın kendi hatırlatma günü geçerlidir; sorumlu kişi tercihinden bağımsız alır.
internal sealed class EvrakTakipBildirimKaynagi(EvrakTakipDbContext db) : IBildirimKaynagi
{
    private static readonly CultureInfo _tr = CultureInfo.GetCultureInfo("tr-TR");

    public string? ModulKodu => ModulKodlari.EvrakTakip;

    public async Task<IReadOnlyList<BildirimAdayi>> AdaylarAsync(BildirimBaglami baglam, CancellationToken cancellationToken)
    {
        var bas = baglam.Bugun.AddDays(-TakipHatirlatma.GecikmeGunu);
        var evraklar = await db.TakipEvraklari.AsNoTracking()
            .Where(e => e.AktifMi && e.BitisTarihi >= bas && e.BitisTarihi <= baglam.Bugun.AddDays(e.HatirlatmaGun))
            .Select(e => new { e.Id, e.Ad, e.Tur, e.BelgeNo, e.BitisTarihi, e.HatirlatmaGun, e.SorumluKullaniciId })
            .ToListAsync(cancellationToken);

        var adaylar = new List<BildirimAdayi>();
        foreach (var e in evraklar)
        {
            if (TakipHatirlatma.Asama(e.BitisTarihi, e.HatirlatmaGun, baglam.Bugun) is not { } asama)
            {
                continue;
            }

            var kalan = e.BitisTarihi.DayNumber - baglam.Bugun.DayNumber;
            var bitis = e.BitisTarihi.ToString("dd.MM.yyyy", _tr);
            var doldu = asama == "doldu";
            var baslik = doldu ? $"Süresi doldu: {e.Ad}" : $"{kalan} gün kaldı: {e.Ad}";
            var metin = string.Join(" · ", new[] { TakipEvrakTurleri.Ad(e.Tur), e.BelgeNo, doldu ? $"{bitis} tarihinde bitti" : $"bitiş {bitis}" }.Where(x => !string.IsNullOrWhiteSpace(x)));
            var anahtar = $"evrak-takip:{e.Id}:{e.BitisTarihi.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}:{asama}";
            adaylar.Add(new BildirimAdayi(BildirimTuru.EvrakSuresi, anahtar, baslik, metin, "evrak-takibi",
                asama == "yaklasiyor" ? BildirimOnemi.Bilgi : BildirimOnemi.Uyari, e.SorumluKullaniciId));
        }

        return adaylar;
    }
}
