using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OnMuhasebe.Shared.Domain.Abstractions;
using OnMuhasebe.Shared.Infrastructure.Kimlik;

namespace OnMuhasebe.Shared.Infrastructure.Persistence;

internal sealed class DenetimInterceptor(IMevcutKullanici mevcutKullanici, TimeProvider zaman) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Uygula(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Uygula(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Uygula(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var simdi = zaman.GetUtcNow().UtcDateTime;
        var kullaniciId = mevcutKullanici.KullaniciId;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is ISilinebilir && entry.State == EntityState.Deleted)
            {
                // Unchanged'e çekip yalnızca silinme alanlarını değiştirmek, diğer kolonların güncellenmesini önler.
                entry.State = EntityState.Unchanged;
                Ata(entry, nameof(ISilinebilir.SilindiMi), true);
                Ata(entry, nameof(ISilinebilir.SilinmeTarihi), simdi);
            }

            if (entry.Entity is IFirmaVarligi firmaVarligi)
            {
                FirmaKontrolEt(entry, firmaVarligi);
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
    }

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
