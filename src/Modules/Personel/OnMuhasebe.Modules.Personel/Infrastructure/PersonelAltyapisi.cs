using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.Personel.Contracts;
using OnMuhasebe.Modules.Personel.Domain;
using OnMuhasebe.Shared.Contracts.Bildirimler;
using OnMuhasebe.Shared.Contracts.Dosyalar;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Bildirimler;
using OnMuhasebe.Shared.Infrastructure.Dosyalar;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Personel.Infrastructure;

internal sealed class PersonelDbContext(DbContextOptions<PersonelDbContext> options, IMevcutKullanici mevcutKullanici)
    : ModulDbContext(options, mevcutKullanici)
{
    protected override string Sema => "personel";

    public DbSet<PersonelKarti> Personeller => Set<PersonelKarti>();
    public DbSet<MaasDonemi> Donemler => Set<MaasDonemi>();
    public DbSet<MaasSatiri> MaasSatirlari => Set<MaasSatiri>();
    public DbSet<Avans> Avanslar => Set<Avans>();
    public DbSet<AvansMahsubu> AvansMahsuplari => Set<AvansMahsubu>();
    public DbSet<PersonelOdemesi> Odemeler => Set<PersonelOdemesi>();
    public DbSet<MasrafBeyani> Masraflar => Set<MasrafBeyani>();
    public DbSet<Zimmet> Zimmetler => Set<Zimmet>();

    // Dönem firmanındır: işyeri kısıtlı kullanıcı açsa da tüm çalışanları kapsar.
    public IQueryable<PersonelKarti> TumPersoneller => Personeller.IgnoreQueryFilters([IsyeriFiltresi]);
}

internal sealed class PersonelKartiConfiguration : IEntityTypeConfiguration<PersonelKarti>
{
    public void Configure(EntityTypeBuilder<PersonelKarti> b)
    {
        b.ToTable("personel_karti");
        b.Property(x => x.Kod).HasMaxLength(20);
        b.Property(x => x.Ad).HasMaxLength(100);
        b.Property(x => x.Soyad).HasMaxLength(100);
        b.Property(x => x.TcKimlik).HasMaxLength(11);
        b.Property(x => x.Gorev).HasMaxLength(100);
        b.Property(x => x.Departman).HasMaxLength(100);
        b.Property(x => x.Iban).HasMaxLength(34);
        b.Property(x => x.NetMaas).HasPrecision(18, 2);
        b.Property(x => x.Telefon).HasMaxLength(30);
        b.Property(x => x.Eposta).HasMaxLength(256);
        b.Ignore(x => x.AdSoyad);
        b.HasIndex(x => new { x.FirmaId, x.Kod }).IsUnique();
        b.HasIndex(x => new { x.FirmaId, x.TcKimlik }).IsUnique().HasFilter("tc_kimlik IS NOT NULL AND NOT silindi_mi");
        b.HasIndex(x => new { x.FirmaId, x.KullaniciId }).IsUnique().HasFilter("kullanici_id IS NOT NULL AND NOT silindi_mi");
    }
}

internal sealed class MaasDonemiConfiguration : IEntityTypeConfiguration<MaasDonemi>
{
    public void Configure(EntityTypeBuilder<MaasDonemi> b)
    {
        b.ToTable("maas_donemi");
        b.Property(x => x.Durum).HasConversion<string>().HasMaxLength(20);
        b.HasMany(x => x.Satirlar).WithOne().HasForeignKey(x => x.DonemId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Satirlar).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasIndex(x => new { x.FirmaId, x.Yil, x.Ay }).IsUnique();
    }
}

internal sealed class MaasSatiriConfiguration : IEntityTypeConfiguration<MaasSatiri>
{
    public void Configure(EntityTypeBuilder<MaasSatiri> b)
    {
        b.ToTable("maas_satiri");
        foreach (var alan in new[] { nameof(MaasSatiri.NetMaas), nameof(MaasSatiri.Prim), nameof(MaasSatiri.Kesinti), nameof(MaasSatiri.AvansKesintisi), nameof(MaasSatiri.Odenen) })
        {
            b.Property(alan).HasPrecision(18, 2);
        }

        b.Property(x => x.Not).HasMaxLength(250);
        b.Ignore(x => x.Odenecek);
        b.Ignore(x => x.Kalan);
        b.HasIndex(x => new { x.DonemId, x.PersonelId }).IsUnique();
        b.HasIndex(x => x.PersonelId);
    }
}

internal sealed class AvansConfiguration : IEntityTypeConfiguration<Avans>
{
    public void Configure(EntityTypeBuilder<Avans> b)
    {
        b.ToTable("avans");
        b.Property(x => x.Tutar).HasPrecision(18, 2);
        b.Property(x => x.Kalan).HasPrecision(18, 2);
        b.Property(x => x.Aciklama).HasMaxLength(250);
        b.HasIndex(x => new { x.FirmaId, x.PersonelId });
    }
}

internal sealed class AvansMahsubuConfiguration : IEntityTypeConfiguration<AvansMahsubu>
{
    public void Configure(EntityTypeBuilder<AvansMahsubu> b)
    {
        b.ToTable("avans_mahsubu");
        b.Property(x => x.Tutar).HasPrecision(18, 2);
        b.HasIndex(x => x.SatirId);
        b.HasIndex(x => x.AvansId);
        b.HasIndex(x => new { x.FirmaId, x.Tarih });
    }
}

internal sealed class PersonelOdemesiConfiguration : IEntityTypeConfiguration<PersonelOdemesi>
{
    public void Configure(EntityTypeBuilder<PersonelOdemesi> b)
    {
        b.ToTable("personel_odemesi");
        b.Property(x => x.Tur).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.Tutar).HasPrecision(18, 2);
        b.HasIndex(x => x.KaynakId);
        b.HasIndex(x => new { x.FirmaId, x.PersonelId });
    }
}

internal sealed class MasrafBeyaniConfiguration : IEntityTypeConfiguration<MasrafBeyani>
{
    public void Configure(EntityTypeBuilder<MasrafBeyani> b)
    {
        b.ToTable("masraf_beyani");
        b.Property(x => x.Kategori).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Durum).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Tutar).HasPrecision(18, 2);
        b.Property(x => x.Aciklama).HasMaxLength(500);
        b.Property(x => x.KararVeren).HasMaxLength(256);
        b.Property(x => x.Not).HasMaxLength(500);
        b.HasIndex(x => new { x.FirmaId, x.Durum });
        b.HasIndex(x => x.PersonelId);
    }
}

internal sealed class ZimmetConfiguration : IEntityTypeConfiguration<Zimmet>
{
    public void Configure(EntityTypeBuilder<Zimmet> b)
    {
        b.ToTable("zimmet");
        b.Property(x => x.Ad).HasMaxLength(200);
        b.Property(x => x.SeriNo).HasMaxLength(100);
        b.Property(x => x.Bedel).HasPrecision(18, 2);
        b.Property(x => x.Durum).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Not).HasMaxLength(500);
        b.HasIndex(x => new { x.FirmaId, x.PersonelId });
    }
}

// Masraf fişi: masraf yetkisi olan gördüğü masrafa, çalışan yalnız kendi masrafına erişir. Fiş aynı kayda eklendiği için onaylayan da görür.
internal sealed class MasrafDosyaKaynagi(PersonelDbContext db, IMevcutKullanici kullanici) : IDosyaKaynagi
{
    public string Tur => DosyaKayitTurleri.PersonelMasrafi;

    public string ModulKodu => ModulKodlari.Personel;

    public string GorunumYetkisi => PersonelYetkileri.Masraf.Yetki(YetkiIslemleri.Goruntule);

    public async Task<bool> ErisebilirMiAsync(IYetkiDenetimi yetki, Guid kayitId, CancellationToken cancellationToken) =>
        await yetki.VarMiAsync(GorunumYetkisi, cancellationToken)
        || (await yetki.VarMiAsync(PersonelYetkileri.Kendi.Yetki(YetkiIslemleri.Goruntule), cancellationToken) && await KendisininMiAsync(kayitId, cancellationToken));

    public async Task<bool> KayitVarMiAsync(Guid kayitId, CancellationToken cancellationToken) =>
        await db.Masraflar.AnyAsync(m => m.Id == kayitId, cancellationToken) || await KendisininMiAsync(kayitId, cancellationToken);

    private async Task<bool> KendisininMiAsync(Guid kayitId, CancellationToken cancellationToken)
    {
        var id = kullanici.KullaniciId;
        return id is not null && await (from m in db.Masraflar.IgnoreQueryFilters([ModulDbContext.IsyeriFiltresi])
                                        join p in db.TumPersoneller on m.PersonelId equals p.Id
                                        where m.Id == kayitId && p.KullaniciId == id
                                        select m.Id).AnyAsync(cancellationToken);
    }
}

// Ödeme tek yol: personel kaydı ve (lisans varsa) kasa/banka hareketi aynı işlemde yazılır.
internal sealed class PersonelOdemeServisi(PersonelDbContext db, IKasaBankaYazici kasa, IKasaBankaSorgusu kasaSorgu, ILisansSorgusu lisans, IMevcutKullanici kullanici)
{
    public async Task<bool> KasaVarMiAsync(CancellationToken ct) =>
        ((await lisans.GetirAsync(kullanici.FirmaId!.Value, ct))?.Moduller ?? []).Contains(ModulKodlari.KasaBanka);

    public async Task<Sonuc<PersonelOdemesi>> YazAsync(Guid personelId, PersonelOdemeTuru tur, Guid kaynakId, DateOnly tarih, decimal tutar, Guid? hesapId, string aciklama, CancellationToken ct)
    {
        var kasaVar = await KasaVarMiAsync(ct);
        if (kasaVar)
        {
            if (hesapId is not { } id)
            {
                return PersonelHatalari.HesapGerekli;
            }

            var hesap = await kasaSorgu.HesapGetirAsync(id, ct);
            if (hesap is not { AktifMi: true, ParaBirimi: "TRY" } || hesap.Tur is not (HesapTuru.Kasa or HesapTuru.Banka))
            {
                return PersonelHatalari.HesapTuru;
            }
        }

        var odeme = PersonelOdemesi.Olustur(personelId, tur, kaynakId, tarih, tutar, kasaVar ? hesapId : null);
        db.Odemeler.Add(odeme);
        if (kasaVar)
        {
            var yazim = await kasa.PersonelOdemesiYazAsync(odeme.Id, new PersonelKasaOdemesi(hesapId!.Value, tur == PersonelOdemeTuru.Avans, tarih, tutar, aciklama), ct);
            if (!yazim.BasariliMi)
            {
                return yazim.Hata;
            }
        }

        return odeme;
    }

    public async Task<Sonuc> IptalAsync(PersonelOdemesi odeme, CancellationToken ct)
    {
        var iptal = odeme.IptalEt();
        if (!iptal.BasariliMi)
        {
            return iptal;
        }

        return odeme.HesapId is null ? Sonuc.Basarili() : await kasa.BelgeIslemleriniIptalEtAsync(odeme.Id, ct);
    }
}

internal sealed class PersonelMuhasebeSorgusu(PersonelDbContext db) : IPersonelMuhasebeSorgusu
{
    public async Task<IReadOnlyList<MuhasebePersonelKaydi>> KayitlarAsync(DateOnly baslangic, DateOnly bitis, Guid? isyeriId, CancellationToken cancellationToken = default)
    {
        var masraflar = await (from m in db.Masraflar.AsNoTracking()
                               join p in db.TumPersoneller.AsNoTracking() on m.PersonelId equals p.Id
                               where m.Tarih >= baslangic && m.Tarih <= bitis && (m.Durum == MasrafDurumu.Onaylandi || m.Durum == MasrafDurumu.Odendi)
                                     && (isyeriId == null || m.IsyeriId == isyeriId)
                               select new MuhasebePersonelKaydi(m.Id, MuhasebePersonelTurleri.Masraf, m.Tarih, p.Kod, p.Ad + " " + p.Soyad + " masraf: " + m.Aciklama, m.Tutar, m.IsyeriId))
            .ToListAsync(cancellationToken);
        var mahsuplar = await (from a in db.AvansMahsuplari.AsNoTracking()
                               join p in db.TumPersoneller.AsNoTracking() on a.PersonelId equals p.Id
                               where a.Tarih >= baslangic && a.Tarih <= bitis && (isyeriId == null || a.IsyeriId == isyeriId)
                               select new MuhasebePersonelKaydi(a.Id, MuhasebePersonelTurleri.AvansMahsubu, a.Tarih, p.Kod, p.Ad + " " + p.Soyad + " avans mahsubu", a.Tutar, a.IsyeriId))
            .ToListAsync(cancellationToken);
        return [.. masraflar, .. mahsuplar];
    }
}

// Onay bekleyen masraf, ödenmemiş maaş, işten çıkan personelin üzerinde kalan zimmet.
internal sealed class PersonelBildirimKaynagi(PersonelDbContext db) : IBildirimKaynagi
{
    public string? ModulKodu => ModulKodlari.Personel;

    public async Task<IReadOnlyList<BildirimAdayi>> AdaylarAsync(BildirimBaglami baglam, CancellationToken cancellationToken)
    {
        var adaylar = new List<BildirimAdayi>();
        var bekleyen = await db.Masraflar.AsNoTracking().CountAsync(m => m.Durum == MasrafDurumu.Beyan, cancellationToken);
        if (bekleyen > 0)
        {
            adaylar.Add(new BildirimAdayi(BildirimTuru.Personel, $"personel-masraf:{baglam.Bugun:yyyyMMdd}", $"{bekleyen} masraf onay bekliyor", "Masraf beyanlarını kontrol edin.", "personel/masraflar", BildirimOnemi.Uyari));
        }

        var donemler = await db.Donemler.AsNoTracking().Include(d => d.Satirlar).Where(d => d.Durum == MaasDonemiDurumu.Onayli).ToListAsync(cancellationToken);
        foreach (var d in donemler.Where(d => MaasHesabi.DonemSonu(d.Yil, d.Ay) <= baglam.Bugun))
        {
            var kalan = d.Satirlar.Sum(s => s.Kalan);
            if (kalan > 0)
            {
                adaylar.Add(new BildirimAdayi(BildirimTuru.Personel, $"personel-maas:{d.Id}:{baglam.Bugun:yyyyMM}", $"Ödenmemiş maaş: {d.Ay:00}/{d.Yil}", $"Kalan {kalan:N2} TL.", $"personel/maas/{d.Id}", BildirimOnemi.Kritik));
            }
        }

        var zimmetler = await (from z in db.Zimmetler.AsNoTracking()
                               join p in db.TumPersoneller.AsNoTracking() on z.PersonelId equals p.Id
                               where z.Durum == ZimmetDurumu.Zimmette && p.IstenCikis != null
                               select new { z.Id, z.Ad, Personel = p.Ad + " " + p.Soyad, p.IstenCikis }).ToListAsync(cancellationToken);
        adaylar.AddRange(zimmetler.Select(z => new BildirimAdayi(BildirimTuru.Personel, $"personel-zimmet:{z.Id}", $"Zimmet iade edilmedi: {z.Ad}", $"{z.Personel} işten çıktı.", "personel/zimmetler", BildirimOnemi.Uyari)));
        return adaylar;
    }
}
