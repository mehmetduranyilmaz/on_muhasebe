using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnMuhasebe.Modules.Satinalma.Contracts;
using OnMuhasebe.Modules.Satinalma.Domain;
using OnMuhasebe.Modules.Yonetim.Contracts;
using OnMuhasebe.Shared.Contracts.Bildirimler;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Infrastructure.Bildirimler;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Satinalma.Infrastructure;

internal sealed class SatinalmaDbContext(DbContextOptions<SatinalmaDbContext> options, IMevcutKullanici mevcutKullanici)
    : ModulDbContext(options, mevcutKullanici)
{
    protected override string Sema => "satinalma";

    public DbSet<SatinalmaTalebi> Talepler => Set<SatinalmaTalebi>();
    public DbSet<OnayBasamagi> OnayBasamaklari => Set<OnayBasamagi>();
}

internal sealed class SatinalmaTalebiConfiguration : IEntityTypeConfiguration<SatinalmaTalebi>
{
    public void Configure(EntityTypeBuilder<SatinalmaTalebi> builder)
    {
        builder.ToTable("satinalma_talebi");
        builder.Property(x => x.Kod).HasMaxLength(20);
        builder.Property(x => x.TalepEden).HasMaxLength(256);
        builder.Property(x => x.Gerekce).HasMaxLength(1000);
        builder.Property(x => x.Durum).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Toplam).HasPrecision(18, 2);
        builder.Ignore(x => x.BekleyenOnay);
        builder.HasMany(x => x.Kalemler).WithOne().HasForeignKey(x => x.TalepId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Onaylar).WithOne().HasForeignKey(x => x.TalepId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Kalemler).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.Onaylar).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(x => new { x.FirmaId, x.Kod }).IsUnique();
        builder.HasIndex(x => new { x.FirmaId, x.Durum });
    }
}

internal sealed class TalepKalemiConfiguration : IEntityTypeConfiguration<TalepKalemi>
{
    public void Configure(EntityTypeBuilder<TalepKalemi> builder)
    {
        builder.ToTable("talep_kalemi");
        builder.Property(x => x.Ad).HasMaxLength(250);
        builder.Property(x => x.Birim).HasMaxLength(20);
        builder.Property(x => x.Miktar).HasPrecision(18, 4);
        builder.Property(x => x.TahminiBirimFiyat).HasPrecision(18, 4);
        builder.Property(x => x.Tutar).HasPrecision(18, 2);
        builder.HasIndex(x => x.TalepId);
    }
}

internal sealed class TalepOnayiConfiguration : IEntityTypeConfiguration<TalepOnayi>
{
    public void Configure(EntityTypeBuilder<TalepOnayi> builder)
    {
        builder.ToTable("talep_onayi");
        builder.Property(x => x.Onaylayan).HasMaxLength(256);
        builder.Property(x => x.Durum).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.KararVeren).HasMaxLength(256);
        builder.Property(x => x.Not).HasMaxLength(500);
        builder.HasIndex(x => new { x.TalepId, x.GonderimNo, x.Sira }).IsUnique();
    }
}

internal sealed class OnayBasamagiConfiguration : IEntityTypeConfiguration<OnayBasamagi>
{
    public void Configure(EntityTypeBuilder<OnayBasamagi> builder)
    {
        builder.ToTable("onay_basamagi");
        builder.Property(x => x.EnAzTutar).HasPrecision(18, 2);
        builder.Property(x => x.Ad).HasMaxLength(256);
        builder.HasIndex(x => new { x.FirmaId, x.Sira }).IsUnique();
    }
}

// Bir basamakta karar verebilecek kullanıcılar: rol ya da kişi eşleşmesi ve onay yetkisi. Talep eden sayılmaz.
internal sealed class OnaycilarServisi(IFirmaRehberi rehber, IYetkiSorgusu yetkiSorgusu)
{
    public static readonly string OnayYetkisi = SatinalmaYetkileri.Onay.Yetki(YetkiIslemleri.Onayla);

    public async Task<List<Guid>> AdaylarAsync(Guid firmaId, Guid? rolId, Guid? kullaniciId, Guid talepEdenId, CancellationToken ct)
    {
        var adaylar = new List<Guid>();
        foreach (var k in await rehber.KullaniciRolleriAsync(firmaId, ct))
        {
            if (k.KullaniciId != talepEdenId && OnayKurallari.YetkiliMi(rolId, kullaniciId, k.KullaniciId, k.RolId, k.TamYetkiliMi)
                && (await yetkiSorgusu.GetirAsync(firmaId, k.KullaniciId, ct)).VarMi(OnayYetkisi))
            {
                adaylar.Add(k.KullaniciId);
            }
        }

        return adaylar;
    }
}

// Sıradaki onaycılara "onayınızı bekliyor", talep edene sonuç. Alıcı belli olduğu için tercihlere göre başkasına gitmez.
internal sealed class SatinalmaBildirimKaynagi(SatinalmaDbContext db, OnaycilarServisi onaycilar) : IBildirimKaynagi
{
    private const int SonucGunu = 7;

    public string? ModulKodu => ModulKodlari.Satinalma;

    public async Task<IReadOnlyList<BildirimAdayi>> AdaylarAsync(BildirimBaglami baglam, CancellationToken cancellationToken)
    {
        var adaylar = new List<BildirimAdayi>();
        var onaydakiler = await db.Talepler.AsNoTracking().Include(t => t.Onaylar).Where(t => t.Durum == TalepDurumu.Onayda).ToListAsync(cancellationToken);
        foreach (var t in onaydakiler)
        {
            if (t.BekleyenOnay is not { } sira)
            {
                continue;
            }

            foreach (var kisi in await onaycilar.AdaylarAsync(baglam.FirmaId, sira.RolId, sira.KullaniciId, t.TalepEdenId, cancellationToken))
            {
                adaylar.Add(new BildirimAdayi(BildirimTuru.SatinalmaOnayi, $"satinalma-onay:{t.Id}:{t.GonderimNo}:{sira.Sira}:{kisi}",
                    $"Onayınızı bekliyor: {t.Kod}", $"{t.TalepEden} · {t.Gerekce}", $"satinalma/{t.Id}", BildirimOnemi.Uyari, kisi, YalnizHedefe: true));
            }
        }

        var sinir = baglam.Bugun.AddDays(-SonucGunu).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var sonuclananlar = await db.Talepler.AsNoTracking()
            .Where(t => (t.Durum == TalepDurumu.Onaylandi || t.Durum == TalepDurumu.Reddedildi) && t.KararTarihi >= sinir)
            .Select(t => new { t.Id, t.Kod, t.Gerekce, t.Durum, t.GonderimNo, t.TalepEdenId })
            .ToListAsync(cancellationToken);
        foreach (var t in sonuclananlar)
        {
            var onaylandi = t.Durum == TalepDurumu.Onaylandi;
            adaylar.Add(new BildirimAdayi(BildirimTuru.SatinalmaOnayi, $"satinalma-sonuc:{t.Id}:{t.GonderimNo}:{t.Durum}",
                onaylandi ? $"Talebiniz onaylandı: {t.Kod}" : $"Talebiniz reddedildi: {t.Kod}", t.Gerekce, $"satinalma/{t.Id}",
                onaylandi ? BildirimOnemi.Bilgi : BildirimOnemi.Uyari, t.TalepEdenId, YalnizHedefe: true));
        }

        return adaylar;
    }
}
