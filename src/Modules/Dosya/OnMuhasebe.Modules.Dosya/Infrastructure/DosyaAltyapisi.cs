using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OnMuhasebe.Modules.Dosya.Domain;
using OnMuhasebe.Modules.Yonetim.Contracts;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Dosya.Infrastructure;

internal sealed class DosyaDbContext(DbContextOptions<DosyaDbContext> options, IMevcutKullanici mevcutKullanici)
    : ModulDbContext(options, mevcutKullanici)
{
    protected override string Sema => "dosya";

    public DbSet<DosyaEki> DosyaEkleri => Set<DosyaEki>();
}

internal sealed class DosyaEkiConfiguration : IEntityTypeConfiguration<DosyaEki>
{
    public void Configure(EntityTypeBuilder<DosyaEki> builder)
    {
        builder.ToTable("dosya_eki");
        builder.Property(x => x.KayitTuru).HasMaxLength(30);
        builder.Property(x => x.Ad).HasMaxLength(200);
        builder.Property(x => x.Ozet).HasMaxLength(64);
        builder.Property(x => x.DepoYolu).HasMaxLength(200);
        builder.Property(x => x.Yukleyen).HasMaxLength(256);
        builder.HasIndex(x => new { x.FirmaId, x.KayitTuru, x.KayitId });
    }
}

// Dosya içeriklerinin saklandığı yer. Nesne depolamaya geçilirse yalnız bu arayüzün uygulaması değişir.
internal interface IDosyaDeposu
{
    Task YazAsync(string yol, Stream icerik, CancellationToken cancellationToken);

    Stream? Ac(string yol);

    void Sil(string yol);
}

// Yol kullanıcı girdisi içermez (firma kimliği / dosya kimliği). Önce geçici ada yazılır: yarım dosya tam sanılmasın.
internal sealed class DiskDosyaDeposu : IDosyaDeposu
{
    private readonly string _kok;

    public DiskDosyaDeposu(IConfiguration configuration, IHostEnvironment ortam)
    {
        var klasor = configuration["Dosya:Klasor"];
        if (string.IsNullOrWhiteSpace(klasor))
        {
            if (ortam.IsProduction())
            {
                throw new InvalidOperationException("Dosya:Klasor ayarı tanımlı değil.");
            }

            klasor = Path.Combine(Path.GetTempPath(), "onmuhasebe-dosyalar");
        }

        _kok = Path.GetFullPath(klasor);
    }

    public async Task YazAsync(string yol, Stream icerik, CancellationToken cancellationToken)
    {
        var hedef = TamYol(yol);
        Directory.CreateDirectory(Path.GetDirectoryName(hedef)!);
        var gecici = hedef + ".yarim";
        try
        {
            await using (var yazici = new FileStream(gecici, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81_920, useAsync: true))
            {
                await icerik.CopyToAsync(yazici, cancellationToken);
                await yazici.FlushAsync(cancellationToken);
            }

            File.Move(gecici, hedef);
        }
        catch
        {
            File.Delete(gecici);
            throw;
        }
    }

    public Stream? Ac(string yol)
    {
        var tam = TamYol(yol);
        return File.Exists(tam) ? new FileStream(tam, FileMode.Open, FileAccess.Read, FileShare.Read, 81_920, useAsync: true) : null;
    }

    public void Sil(string yol) => File.Delete(TamYol(yol));

    private string TamYol(string yol)
    {
        var tam = Path.GetFullPath(Path.Combine(_kok, yol));
        return tam.StartsWith(_kok + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? tam
            : throw new InvalidOperationException("Geçersiz dosya yolu.");
    }
}

// Saklama süresi dolan çöp kutusu dosyalarını günde bir kalıcı siler. Her firma kendi kapsamında çalışır.
internal sealed partial class DosyaTemizleyici(
    IServiceScopeFactory scopeFactory, FirmaKapsami firmaKapsami, TimeProvider zaman, ILogger<DosyaTemizleyici> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var saat = new PeriodicTimer(TimeSpan.FromHours(24), zaman);
        do
        {
            try
            {
                await TemizleAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                TemizlikHatasi(logger, e);
            }
        }
        while (await saat.WaitForNextTickAsync(stoppingToken));
    }

    private async Task TemizleAsync(CancellationToken ct)
    {
        IReadOnlyList<Guid> firmalar;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            firmalar = await scope.ServiceProvider.GetRequiredService<IFirmaRehberi>().AktifFirmalarAsync(ct);
        }

        var sinir = zaman.GetUtcNow().UtcDateTime.AddDays(-DosyaEki.SaklamaGunu);
        foreach (var firmaId in firmalar)
        {
            await firmaKapsami.CalistirAsync(firmaId, async sp =>
            {
                var db = sp.GetRequiredService<DosyaDbContext>();
                var depo = sp.GetRequiredService<IDosyaDeposu>();
                var silinecekler = await db.DosyaEkleri.Where(d => d.SilinmeTarihi != null && d.SilinmeTarihi < sinir).Take(1000).ToListAsync(ct);
                foreach (var dosya in silinecekler)
                {
                    depo.Sil(dosya.DepoYolu);
                }

                db.DosyaEkleri.RemoveRange(silinecekler);
                await db.SaveChangesAsync(ct);
                return silinecekler.Count;
            });
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Silinen dosyalar temizlenemedi")]
    private static partial void TemizlikHatasi(ILogger logger, Exception exception);
}
