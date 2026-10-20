using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using OnMuhasebe.Modules.Yonetim.Domain;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Tekrar;

namespace OnMuhasebe.Modules.Yonetim.Infrastructure;

internal sealed class IstekKaydiDeposu(YonetimDbContext db, IMevcutKullanici mevcutKullanici) : IIstekKaydiDeposu
{
    public async Task<(Guid? YeniId, KayitliIstek? Mevcut)> BaslatAsync(string anahtar, string ozet, CancellationToken cancellationToken)
    {
        var kullaniciId = mevcutKullanici.KullaniciId!.Value;
        var kayit = IstekKaydi.Olustur(kullaniciId, anahtar, ozet);
        db.IstekKayitlari.Add(kayit);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return (kayit.Id, null);
        }
        catch (DbUpdateException hata) when (hata.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Aynı anahtar daha önce (ya da şu anda başka bir istekte) açılmış: benzersiz indeks yarışı tek kayda indirir.
            db.Entry(kayit).State = EntityState.Detached;
            var mevcut = await db.IstekKayitlari.AsNoTracking()
                .SingleAsync(k => k.KullaniciId == kullaniciId && k.Anahtar == anahtar, cancellationToken);
            return (null, new KayitliIstek(mevcut.Ozet, mevcut.Tamamlandi, mevcut.OlusturmaTarihi, mevcut.DurumKodu, mevcut.IcerikTuru, mevcut.Konum,
                mevcut.Govde));
        }
        finally
        {
            if (db.Entry(kayit).State != EntityState.Detached)
            {
                db.Entry(kayit).State = EntityState.Detached;
            }
        }
    }

    public Task TamamlaAsync(Guid id, int durumKodu, string? icerikTuru, string? konum, byte[] govde, CancellationToken cancellationToken) =>
        db.IstekKayitlari.Where(k => k.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(k => k.Tamamlandi, true)
            .SetProperty(k => k.DurumKodu, durumKodu)
            .SetProperty(k => k.IcerikTuru, icerikTuru)
            .SetProperty(k => k.Konum, konum)
            .SetProperty(k => k.Govde, govde), cancellationToken);

    public Task SilAsync(Guid id, CancellationToken cancellationToken) =>
        db.IstekKayitlari.Where(k => k.Id == id).ExecuteDeleteAsync(cancellationToken);
}

// Süresi dolan istek kayıtlarını saatte bir siler (tüm firmalar).
internal sealed class IstekKaydiTemizleyici(IServiceScopeFactory kapsamlar, TimeProvider zaman) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var zamanlayici = new PeriodicTimer(TimeSpan.FromHours(1), zaman);
        do
        {
            await using var kapsam = kapsamlar.CreateAsyncScope();
            var db = kapsam.ServiceProvider.GetRequiredService<YonetimDbContext>();
            var sinir = zaman.GetUtcNow().UtcDateTime.AddHours(-IstekKaydi.SaklamaSaati);
            await db.IstekKayitlari.IgnoreQueryFilters([ModulDbContext.FirmaFiltresi])
                .Where(k => k.OlusturmaTarihi < sinir)
                .ExecuteDeleteAsync(stoppingToken);
        }
        while (await zamanlayici.WaitForNextTickAsync(stoppingToken));
    }
}
