using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OnMuhasebe.Shared.Contracts.Ayarlar;
using OnMuhasebe.Shared.Domain.Abstractions;
using OnMuhasebe.Shared.Domain.Entities;
using OnMuhasebe.Shared.Infrastructure.Kimlik;

namespace OnMuhasebe.Shared.Infrastructure.Persistence;

// IServiceProvider: dönem kilidi sorgusu Yönetim context'ini kullanır, o da bu interceptor'u; doğrudan enjeksiyon döngü olurdu.
internal sealed class DenetimInterceptor(IMevcutKullanici mevcutKullanici, TimeProvider zaman, IServiceProvider servisler) : SaveChangesInterceptor
{
    // Değişiklik geçmişinde ve dönem kilidinde dikkate alınmayan teknik alanlar.
    private static readonly HashSet<string> _teknikAlanlar =
    [
        nameof(BaseEntity.Surum), nameof(BaseEntity.OlusturmaTarihi), nameof(BaseEntity.OlusturanId),
        nameof(BaseEntity.GuncellemeTarihi), nameof(BaseEntity.GuncelleyenId), nameof(ISilinebilir.SilindiMi),
        nameof(ISilinebilir.SilinmeTarihi), nameof(IFirmaVarligi.FirmaId)
    ];

    private static readonly ConcurrentDictionary<PropertyInfo, bool> _kilitDisiAlanlar = new();
    private static readonly ConcurrentDictionary<Type, bool> _denetimDisiTipler = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        UygulaAsync(eventData.Context, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        return result;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        await UygulaAsync(eventData.Context, cancellationToken);
        return result;
    }

    private async ValueTask UygulaAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (context is null)
        {
            return;
        }

        var simdi = zaman.GetUtcNow().UtcDateTime;
        var kullaniciId = mevcutKullanici.KullaniciId;
        var tumu = context.ChangeTracker.Entries().ToList();
        var izlenen = new Dictionary<Guid, EntityEntry>();
        foreach (var e in tumu)
        {
            if (e.Entity is BaseEntity b)
            {
                izlenen.TryAdd(b.Id, e);
            }
        }

        var girisler = tumu
            .Where(e => e.Entity is not DegisiklikKaydi && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();
        var donemKontrolleri = new List<(Guid FirmaId, DateOnly Tarih)>();
        var kayitlar = new List<DegisiklikKaydi>();

        foreach (var entry in girisler)
        {
            // Yeni kaydın firması önce atanır; dönem kilidi ve geçmiş firmaya göre bakılır.
            if (entry.Entity is IFirmaVarligi firmaVarligi)
            {
                FirmaKontrolEt(entry, firmaVarligi);
            }

            // Durum değişmeden (silme → güncellemeye çevrilmeden) önce bakılır.
            DonemKontrolleriniTopla(izlenen, entry, donemKontrolleri);
            if (DegisiklikKaydiOlustur(izlenen, entry, simdi) is { } kayit)
            {
                kayitlar.Add(kayit);
            }

            if (entry.Entity is ISilinebilir && entry.State == EntityState.Deleted)
            {
                // Unchanged'e çekip yalnızca silinme alanlarını değiştirmek, diğer kolonların güncellenmesini önler.
                entry.State = EntityState.Unchanged;
                Ata(entry, nameof(ISilinebilir.SilindiMi), true);
                Ata(entry, nameof(ISilinebilir.SilinmeTarihi), simdi);
            }

            if (entry.Entity is IDenetlenebilir)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        Ata(entry, nameof(IDenetlenebilir.OlusturmaTarihi), simdi);
                        Ata(entry, nameof(IDenetlenebilir.OlusturanId), kullaniciId);
                        break;
                    case EntityState.Modified:
                        Ata(entry, nameof(IDenetlenebilir.GuncellemeTarihi), simdi);
                        Ata(entry, nameof(IDenetlenebilir.GuncelleyenId), kullaniciId);
                        break;
                }
            }
        }

        await DonemKilidiniDenetleAsync(donemKontrolleri, cancellationToken);

        foreach (var kayit in kayitlar)
        {
            var entry = context.Add(kayit);
            Ata(entry, nameof(IDenetlenebilir.OlusturmaTarihi), simdi);
            Ata(entry, nameof(IDenetlenebilir.OlusturanId), kullaniciId);
        }
    }

    // Döneme bağlı kayıt eklenir/silinirken her zaman, güncellenirken yalnızca tutar/tarih gibi asıl alanlar değiştiyse kilide bakılır.
    // Döneme bağlı kaydın alt satırı (fatura satırı) değişirse üst kaydın tarihi esas alınır.
    private static void DonemKontrolleriniTopla(Dictionary<Guid, EntityEntry> izlenen, EntityEntry entry, List<(Guid, DateOnly)> kontroller)
    {
        if (entry.Entity is IDonemeTabi donemli and IFirmaVarligi firmali)
        {
            if (entry.State == EntityState.Modified && !AsilAlanDegistiMi(entry))
            {
                return;
            }

            var eski = entry.State == EntityState.Added ? donemli.Tarih : (DateOnly)entry.Property(nameof(IDonemeTabi.Tarih)).OriginalValue!;
            kontroller.Add((firmali.FirmaId, eski));
            kontroller.Add((firmali.FirmaId, donemli.Tarih));
            return;
        }

        if (UstKayit(izlenen, entry)?.Giris is { Entity: IDonemeTabi ustDonemli and IFirmaVarligi ustFirmali } ust)
        {
            var eski = ust.State == EntityState.Added ? ustDonemli.Tarih : (DateOnly)ust.Property(nameof(IDonemeTabi.Tarih)).OriginalValue!;
            kontroller.Add((ustFirmali.FirmaId, eski));
            kontroller.Add((ustFirmali.FirmaId, ustDonemli.Tarih));
        }
    }

    private static bool AsilAlanDegistiMi(EntityEntry entry) =>
        entry.Properties.Any(p => p.IsModified
                                  && !_teknikAlanlar.Contains(p.Metadata.Name)
                                  && !KilitDisiMi(p.Metadata.PropertyInfo)
                                  && !Equals(p.OriginalValue, p.CurrentValue));

    private static bool KilitDisiMi(PropertyInfo? ozellik) =>
        ozellik is not null && _kilitDisiAlanlar.GetOrAdd(ozellik, o => o.IsDefined(typeof(DonemKilidiDisindaAttribute)));

    private async Task DonemKilidiniDenetleAsync(List<(Guid FirmaId, DateOnly Tarih)> kontroller, CancellationToken cancellationToken)
    {
        if (kontroller.Count == 0 || servisler.GetService<IDonemKilidiSorgusu>() is not { } sorgu)
        {
            return;
        }

        foreach (var firma in kontroller.GroupBy(k => k.FirmaId))
        {
            if (await sorgu.KilitTarihiAsync(firma.Key, cancellationToken) is not { } kilit)
            {
                continue;
            }

            var enEski = firma.Min(k => k.Tarih);
            if (enEski <= kilit)
            {
                throw new DonemKilitliException(kilit, enEski);
            }
        }
    }

    // Firma kaydının güncellenmesi/silinmesi ile var olan kayda bağlı alt satırın eklenmesi/değişmesi/silinmesi yazılır.
    private DegisiklikKaydi? DegisiklikKaydiOlustur(Dictionary<Guid, EntityEntry> izlenen, EntityEntry entry, DateTime simdi)
    {
        if (entry.Entity is not BaseEntity varlik || DenetimDisiMi(entry.Metadata.ClrType))
        {
            return null;
        }

        var ust = UstKayit(izlenen, entry);
        var firmaId = (entry.Entity as IFirmaVarligi)?.FirmaId ?? (ust?.Giris?.Entity as IFirmaVarligi)?.FirmaId ?? mevcutKullanici.FirmaId;
        if (firmaId is not { } firma || firma == Guid.Empty || (entry.Entity is not IFirmaVarligi && ust is null))
        {
            return null;
        }

        (DegisiklikIslemi Islem, IReadOnlyList<AlanDegisikligi> Alanlar)? degisiklik = entry.State switch
        {
            // Ana kayıtla birlikte eklenen satırlar ve firma kayıtlarının kendisi yazılmaz.
            EntityState.Added when entry.Entity is not IFirmaVarligi && ust is { } u && u.Giris?.State != EntityState.Added =>
                (DegisiklikIslemi.Ekleme, Alanlar(entry, _ => null, p => p.CurrentValue)),
            EntityState.Deleted =>
                (DegisiklikIslemi.Silme, entry.Entity is ISilinebilir ? [] : Alanlar(entry, p => p.OriginalValue, _ => null)),
            EntityState.Modified when entry.Entity is ISilinebilir && YeniSilindiMi(entry) =>
                (DegisiklikIslemi.Silme, []),
            EntityState.Modified =>
                (DegisiklikIslemi.Guncelleme, DegisiklikHesaplayici.Farklar(
                    entry.Properties.Where(p => p.IsModified).Select(p => (p.Metadata.Name, p.OriginalValue, p.CurrentValue)), _teknikAlanlar)),
            _ => null
        };

        if (degisiklik is not { } d || (d.Islem == DegisiklikIslemi.Guncelleme && d.Alanlar.Count == 0))
        {
            return null;
        }

        return DegisiklikKaydi.Olustur(
            firma, mevcutKullanici.KullaniciId, mevcutKullanici.KullaniciAdi, simdi, entry.Metadata.GetTableName() ?? entry.Metadata.ClrType.Name,
            varlik.Id, ust?.Id, d.Islem, d.Alanlar);
    }

    private static List<AlanDegisikligi> Alanlar(EntityEntry entry, Func<PropertyEntry, object?> eski, Func<PropertyEntry, object?> yeni) =>
        entry.Properties
            .Where(p => !_teknikAlanlar.Contains(p.Metadata.Name) && !p.Metadata.IsPrimaryKey())
            .Select(p => new AlanDegisikligi(p.Metadata.Name, DegisiklikHesaplayici.Metin(eski(p)), DegisiklikHesaplayici.Metin(yeni(p))))
            .Where(a => a.Eski is not null || a.Yeni is not null)
            .ToList();

    private static bool YeniSilindiMi(EntityEntry entry)
    {
        var silindi = entry.Property(nameof(ISilinebilir.SilindiMi));
        return silindi.IsModified && silindi.CurrentValue is true && silindi.OriginalValue is false;
    }

    // Alt satırın bağlı olduğu (cascade ile silinen) üst kayıt; ör. fatura satırı → fatura. Giriş, üst kayıt izleniyorsa doludur.
    private static (Guid Id, EntityEntry? Giris)? UstKayit(Dictionary<Guid, EntityEntry> izlenen, EntityEntry entry)
    {
        var fk = entry.Metadata.GetForeignKeys()
            .FirstOrDefault(f => f.IsRequired && f.DeleteBehavior == DeleteBehavior.Cascade && f.Properties.Count == 1);
        if (fk is null || entry.Property(fk.Properties[0].Name).CurrentValue is not Guid ustId)
        {
            return null;
        }

        return (ustId, izlenen.TryGetValue(ustId, out var giris) && giris.Metadata == fk.PrincipalEntityType ? giris : null);
    }

    private static bool DenetimDisiMi(Type tip) =>
        _denetimDisiTipler.GetOrAdd(tip, t => t.IsDefined(typeof(DenetimDisiAttribute), inherit: false));

    // FirmaId istemciden alınmaz: yeni kayıtta oturumdaki firmadan atanır ve sonradan değiştirilemez.
    // Oturumu olmayan işlemler (ör. firma kaydı) ve sistem yöneticisi FirmaId'yi kendisi atayabilir.
    private void FirmaKontrolEt(EntityEntry entry, IFirmaVarligi firmaVarligi)
    {
        var oturumFirmaId = mevcutKullanici.FirmaId;

        if (entry.State == EntityState.Added)
        {
            if (firmaVarligi.FirmaId == Guid.Empty)
            {
                Ata(entry, nameof(IFirmaVarligi.FirmaId), oturumFirmaId
                    ?? throw new InvalidOperationException($"{entry.Metadata.ClrType.Name} için firma bilgisi yok."));
            }
            else if (oturumFirmaId is not null && firmaVarligi.FirmaId != oturumFirmaId && !mevcutKullanici.SistemYoneticisiMi)
            {
                throw new InvalidOperationException($"{entry.Metadata.ClrType.Name} başka bir firmaya kaydedilemez.");
            }
        }
        else if (entry.State == EntityState.Modified
                 && !Equals(entry.Property(nameof(IFirmaVarligi.FirmaId)).OriginalValue, firmaVarligi.FirmaId))
        {
            throw new InvalidOperationException($"{entry.Metadata.ClrType.Name} kaydının firması değiştirilemez.");
        }
    }

    private static void Ata(EntityEntry entry, string ozellik, object? deger) =>
        entry.Property(ozellik).CurrentValue = deger;
}
