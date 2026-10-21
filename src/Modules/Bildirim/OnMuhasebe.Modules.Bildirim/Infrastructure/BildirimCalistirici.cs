using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OnMuhasebe.Modules.Bildirim.Contracts;
using OnMuhasebe.Modules.Yonetim.Contracts;
using OnMuhasebe.Shared.Infrastructure.Bildirimler;
using OnMuhasebe.Shared.Infrastructure.Kimlik;

namespace OnMuhasebe.Modules.Bildirim.Infrastructure;

// Her firmayı kendi kapsamında (sorgu filtresi + satır güvenliği o firmaya göre) çalıştırır; bir firmadaki hata diğerlerini durdurmaz.
internal sealed partial class BildirimCalistirici(IServiceScopeFactory scopeFactory, FirmaKapsami firmaKapsami, ILogger<BildirimCalistirici> logger)
    : IBildirimTetikleyici
{
    public async Task<BildirimCalismaSonucu> HepsiAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> firmalar;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            firmalar = await scope.ServiceProvider.GetRequiredService<IFirmaRehberi>().AktifFirmalarAsync(cancellationToken);
        }

        var (uretilen, eposta) = (0, 0);
        foreach (var firmaId in firmalar)
        {
            var sonuc = await FirmaAsync(firmaId, cancellationToken);
            (uretilen, eposta) = (uretilen + sonuc.Uretilen, eposta + sonuc.Eposta);
        }

        return new BildirimCalismaSonucu(firmalar.Count, uretilen, eposta);
    }

    public async Task FirmaIcinAsync(Guid firmaId, CancellationToken cancellationToken) => await FirmaAsync(firmaId, cancellationToken);

    private async Task<(int Uretilen, int Eposta)> FirmaAsync(Guid firmaId, CancellationToken cancellationToken)
    {
        try
        {
            return await firmaKapsami.CalistirAsync(firmaId, sp => sp.GetRequiredService<BildirimMotoru>().CalistirAsync(cancellationToken));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            FirmaHatasi(logger, firmaId, e);
            return (0, 0);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "{FirmaId} firmasının bildirimleri üretilemedi")]
    private static partial void FirmaHatasi(ILogger logger, Guid firmaId, Exception exception);
}

// Saatte bir (Bildirim:CalismaAraligiDakika) tüm firmalar için çalışır; ilk çalışma açılıştan bir dakika sonra.
internal sealed class BildirimZamanlayici(BildirimCalistirici calistirici, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Bildirim:Etkin", true))
        {
            return;
        }

        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        using var zamanlayici = new PeriodicTimer(TimeSpan.FromMinutes(configuration.GetValue("Bildirim:CalismaAraligiDakika", 60)));
        do
        {
            await calistirici.HepsiAsync(stoppingToken);
        }
        while (await zamanlayici.WaitForNextTickAsync(stoppingToken));
    }
}
