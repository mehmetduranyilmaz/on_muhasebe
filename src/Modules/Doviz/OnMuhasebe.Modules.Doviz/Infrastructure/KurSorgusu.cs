using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OnMuhasebe.Modules.Doviz.Contracts;
using OnMuhasebe.Modules.Doviz.Domain;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Shared.Domain;

namespace OnMuhasebe.Modules.Doviz.Infrastructure;

internal sealed partial class KurSorgusu(
    DovizDbContext db,
    TcmbIstemcisi tcmb,
    TimeProvider zaman,
    ILogger<KurSorgusu> logger) : IKurSorgusu
{
    // Aynı bülten için eşzamanlı isteklerin TCMB'ye ikişer kez gitmesini ve benzersizlik çakışmasını önler.
    private static readonly SemaphoreSlim _cekmeKilidi = new(1, 1);

    public async Task<KurBilgisi?> IslemKuruAsync(string paraBirimi, DateOnly islemTarihi, CancellationToken cancellationToken = default)
    {
        if (paraBirimi == ParaBirimleri.Tl)
        {
            return new KurBilgisi(ParaBirimleri.Tl, islemTarihi, 1m, 1m);
        }

        var bugun = TurkiyeSaati.Bugun(zaman);
        foreach (var tarih in KurKurallari.AdayBultenTarihleri(islemTarihi).Where(t => t <= bugun))
        {
            var yayimlandiMi = await BultenDurumuAsync(tarih, bugun, cancellationToken);
            if (yayimlandiMi is null)
            {
                return null;
            }

            if (yayimlandiMi == true)
            {
                var kur = await db.Kurlar.AsNoTracking()
                    .SingleOrDefaultAsync(k => k.Tarih == tarih && k.ParaBirimi == paraBirimi, cancellationToken);
                return kur?.DovizAlis is { } alis ? new KurBilgisi(paraBirimi, tarih, alis, kur.DovizSatis ?? alis) : null;
            }
        }

        return null;
    }

    // true: bülten var, false: o gün bülten yok, null: TCMB'ye ulaşılamadı.
    private async Task<bool?> BultenDurumuAsync(DateOnly tarih, DateOnly bugun, CancellationToken cancellationToken)
    {
        if (await KayitliDurumAsync(tarih, cancellationToken) is { } kayitli)
        {
            return kayitli;
        }

        await _cekmeKilidi.WaitAsync(cancellationToken);
        try
        {
            if (await KayitliDurumAsync(tarih, cancellationToken) is { } kayitliKilitte)
            {
                return kayitliKilitte;
            }

            var kurlar = await tcmb.BultenGetirAsync(tarih, cancellationToken);
            if (kurlar is not null)
            {
                db.KurBultenleri.Add(KurBulteni.Olustur(tarih, yayimlandiMi: true));
                db.Kurlar.AddRange(kurlar.Select(k => Kur.Olustur(tarih, k)));
                await db.SaveChangesAsync(cancellationToken);
                return true;
            }

            // Bugünün bülteni 15:30'dan önce henüz yok; "yayımlanmadı" diye kaydedilmez.
            if (tarih < bugun)
            {
                db.KurBultenleri.Add(KurBulteni.Olustur(tarih, yayimlandiMi: false));
                await db.SaveChangesAsync(cancellationToken);
            }

            return false;
        }
        catch (Exception hata) when (hata is HttpRequestException
                                     || (hata is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            TcmbyeUlasilamadi(logger, tarih, hata);
            return null;
        }
        finally
        {
            _cekmeKilidi.Release();
        }
    }

    private async Task<bool?> KayitliDurumAsync(DateOnly tarih, CancellationToken cancellationToken) =>
        await db.KurBultenleri.AsNoTracking()
            .Where(b => b.Tarih == tarih)
            .Select(b => (bool?)b.YayimlandiMi)
            .SingleOrDefaultAsync(cancellationToken);

    [LoggerMessage(Level = LogLevel.Warning, Message = "TCMB kur bültenine ulaşılamadı: {Tarih}")]
    private static partial void TcmbyeUlasilamadi(ILogger logger, DateOnly tarih, Exception hata);
}
