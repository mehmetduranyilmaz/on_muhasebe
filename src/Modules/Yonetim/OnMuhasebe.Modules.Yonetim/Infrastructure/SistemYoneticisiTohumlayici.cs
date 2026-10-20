using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OnMuhasebe.Modules.Yonetim.Domain;

namespace OnMuhasebe.Modules.Yonetim.Infrastructure;

// İlk sistem yöneticisini yapılandırmadan (user-secrets / ortam değişkeni) oluşturur; varsa dokunmaz.
internal sealed class SistemYoneticisiTohumlayici(IServiceScopeFactory scopeFactory, IConfiguration configuration) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var eposta = configuration["SistemYoneticisi:Eposta"];
        var sifre = configuration["SistemYoneticisi:Sifre"];
        if (string.IsNullOrWhiteSpace(eposta) || string.IsNullOrWhiteSpace(sifre))
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<YonetimDbContext>();
        var normalEposta = Kullanici.EpostaNormalize(eposta);
        if (await db.Kullanicilar.AnyAsync(k => k.Eposta == normalEposta, cancellationToken))
        {
            return;
        }

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Kullanici>>();
        var kullanici = Kullanici.Olustur("Sistem", "Yöneticisi", eposta, sistemYoneticisiMi: true);
        kullanici.SifreBelirle(hasher.HashPassword(kullanici, sifre));
        db.Kullanicilar.Add(kullanici);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
