using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Modules.Fatura.Domain;
using OnMuhasebe.Modules.Yonetim.Contracts;
using OnMuhasebe.Shared.Contracts.Bildirimler;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Domain.Abstractions;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Bildirimler;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Fatura.Infrastructure;

internal sealed class TekrarPlaniConfiguration : IEntityTypeConfiguration<TekrarPlani>
{
    public void Configure(EntityTypeBuilder<TekrarPlani> builder)
    {
        builder.ToTable("tekrar_plani");
        builder.Property(x => x.Kod).HasMaxLength(20);
        builder.Property(x => x.Ad).HasMaxLength(200);
        builder.Property(x => x.Siklik).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Durum).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Aciklama).HasMaxLength(500);
        builder.Property(x => x.StopajOrani).HasPrecision(5, 2);
        builder.Property(x => x.ArtisOrani).HasPrecision(7, 2);
        builder.Property(x => x.SonHata).HasMaxLength(500);
        builder.Property(x => x.SatirlarJson).HasColumnName("satirlar").HasColumnType("jsonb");
        builder.HasIndex(x => new { x.FirmaId, x.Kod }).IsUnique();
        builder.HasIndex(x => new { x.FirmaId, x.Durum, x.SonrakiTarih });
    }
}

internal sealed class TekrarUretimiConfiguration : IEntityTypeConfiguration<TekrarUretimi>
{
    public void Configure(EntityTypeBuilder<TekrarUretimi> builder)
    {
        builder.ToTable("tekrar_uretimi");
        builder.Property(x => x.UygulananArtis).HasPrecision(7, 2);
        builder.HasIndex(x => new { x.PlanId, x.Donem }).IsUnique();
        builder.HasIndex(x => x.FaturaId);
    }
}

internal enum TekrarAdimi { Bekliyor, Uretildi, Hata }

// Bir planın sıradaki dönemini tek işlemde keser: artış, fatura, üretim kaydı ve sonraki tarih birlikte yazılır ya da hiçbiri yazılmaz.
internal sealed class TekrarUretici(FaturaDbContext db, FaturaKayitServisi kayit, IIslemKapsami islemKapsami, TimeProvider zaman)
{
    public async Task<(TekrarAdimi Adim, string? Hata)> SiradakiAsync(Guid planId, CancellationToken ct)
    {
        var bugun = TurkiyeSaati.Bugun(zaman);
        var plan = await db.TekrarPlanlari.SingleOrDefaultAsync(p => p.Id == planId, ct);
        if (plan is not { Durum: TekrarDurumu.Aktif, SonrakiTarih: { } donem } || donem > bugun)
        {
            return (TekrarAdimi.Bekliyor, null);
        }

        string hata;
        try
        {
            var sonuc = await islemKapsami.CalistirAsync<Guid>(async c =>
            {
                var artis = plan.ArtisUygula(donem);
                var fatura = await kayit.PlandanOlusturAsync(plan.FaturaIstegi(donem), plan.IsyeriId!.Value, plan.OtomatikKesinlestir, c);
                if (!fatura.BasariliMi)
                {
                    return fatura.Hata;
                }

                db.TekrarUretimleri.Add(TekrarUretimi.Olustur(plan.Id, donem, fatura.Deger.Fatura.Id, artis));
                plan.Uretildi(donem);
                await db.SaveChangesAsync(c);
                return fatura.Deger.Fatura.Id;
            }, ct);
            if (sonuc.BasariliMi)
            {
                return (TekrarAdimi.Uretildi, null);
            }

            hata = sonuc.Hata.Mesaj;
        }
        catch (DonemKilitliException e)
        {
            hata = e.Message;
        }
        catch (DbUpdateException)
        {
            hata = "Fatura kaydedilemedi; bu dönem başka bir işlemle kesilmiş olabilir.";
        }

        // İşlem geri alındı; izlenen değişiklikler atılır, yalnız hata notu yazılır.
        db.ChangeTracker.Clear();
        var taze = await db.TekrarPlanlari.SingleAsync(p => p.Id == planId, ct);
        taze.HataYaz($"{donem:dd.MM.yyyy} dönemi: {hata}", zaman.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        return (TekrarAdimi.Hata, hata);
    }
}

// Her dönem ayrı kapsamda çalışır: bir dönemin hatası kalan bağlantıyı ve izlenen kayıtları kirletmez, diğer plan ve firmaları durdurmaz.
internal sealed partial class TekrarCalistirici(IServiceScopeFactory scopeFactory, FirmaKapsami firmaKapsami, TimeProvider zaman, ILogger<TekrarCalistirici> logger)
{
    private const int AzamiDonem = 120;

    public async Task HepsiAsync(CancellationToken ct)
    {
        IReadOnlyList<Guid> firmalar;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            firmalar = await scope.ServiceProvider.GetRequiredService<IFirmaRehberi>().AktifFirmalarAsync(ct);
        }

        foreach (var firmaId in firmalar)
        {
            try
            {
                await FirmaAsync(firmaId, ct);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                FirmaHatasi(logger, firmaId, e);
            }
        }
    }

    private async Task FirmaAsync(Guid firmaId, CancellationToken ct)
    {
        var bugun = TurkiyeSaati.Bugun(zaman);
        var planlar = await firmaKapsami.CalistirAsync(firmaId, async sp =>
        {
            // Süresi dolan ya da Fatura modülü olmayan firmaya fatura yazılmaz; süre uzayınca kaçan dönemler kesilir.
            var lisans = await sp.GetRequiredService<ILisansSorgusu>().GetirAsync(firmaId, ct);
            if (lisans is null || lisans.SuresiDolduMu(zaman.GetUtcNow().UtcDateTime) || !lisans.Moduller.Contains(ModulKodlari.Fatura))
            {
                return [];
            }

            return await sp.GetRequiredService<FaturaDbContext>().TekrarPlanlari.AsNoTracking()
                .Where(p => p.Durum == TekrarDurumu.Aktif && p.SonrakiTarih <= bugun)
                .Select(p => p.Id).ToListAsync(ct);
        });
        foreach (var planId in planlar)
        {
            await PlanAsync(firmaId, planId, ct);
        }
    }

    // Kaçan dönemler sırayla kesilir; hata alan plan bu turda durur, sonraki çalışmada yeniden denenir.
    public async Task<(int Uretilen, string? Hata)> PlanAsync(Guid firmaId, Guid planId, CancellationToken ct)
    {
        var uretilen = 0;
        for (var i = 0; i < AzamiDonem; i++)
        {
            var (adim, hata) = await firmaKapsami.CalistirAsync(firmaId, sp => sp.GetRequiredService<TekrarUretici>().SiradakiAsync(planId, ct));
            if (adim == TekrarAdimi.Uretildi)
            {
                uretilen++;
                continue;
            }

            return (uretilen, hata);
        }

        return (uretilen, null);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "{FirmaId} firmasının tekrarlayan faturaları üretilemedi")]
    private static partial void FirmaHatasi(ILogger logger, Guid firmaId, Exception exception);
}

internal sealed class TekrarZamanlayici(TekrarCalistirici calistirici, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("TekrarFatura:Etkin", true))
        {
            return;
        }

        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        using var zamanlayici = new PeriodicTimer(TimeSpan.FromMinutes(configuration.GetValue("TekrarFatura:CalismaAraligiDakika", 60)));
        do
        {
            await calistirici.HepsiAsync(stoppingToken);
        }
        while (await zamanlayici.WaitForNextTickAsync(stoppingToken));
    }
}

// Kesilen dönem, durdurulan plan ve yaklaşan yıllık artış. Alıcıyı kullanıcının bildirim tercihi belirler.
internal sealed class TekrarBildirimKaynagi(FaturaDbContext db) : IBildirimKaynagi
{
    private const int SonGun = 7;
    private const int ArtisHatirlatmaGunu = 30;

    public string? ModulKodu => ModulKodlari.Fatura;

    public async Task<IReadOnlyList<BildirimAdayi>> AdaylarAsync(BildirimBaglami baglam, CancellationToken cancellationToken)
    {
        var adaylar = new List<BildirimAdayi>();
        var sinir = baglam.Bugun.AddDays(-SonGun);
        var uretimler = await (from u in db.TekrarUretimleri.AsNoTracking()
                               join p in db.TekrarPlanlari.AsNoTracking() on u.PlanId equals p.Id
                               join f in db.Faturalar.AsNoTracking() on u.FaturaId equals f.Id
                               where u.Donem >= sinir
                               select new { u.Id, p.Ad, u.Donem, f.Durum, u.FaturaId, u.UygulananArtis }).ToListAsync(cancellationToken);
        foreach (var u in uretimler.Where(x => x.Durum != FaturaDurumu.Iptal))
        {
            var taslak = u.Durum == FaturaDurumu.Taslak;
            var artis = u.UygulananArtis is { } a ? $" %{a:0.##} yıllık artış uygulandı." : "";
            adaylar.Add(new BildirimAdayi(BildirimTuru.TekrarFatura, $"tekrar-uretim:{u.Id}",
                taslak ? $"Onay bekliyor: {u.Ad}" : $"Fatura kesildi: {u.Ad}", $"{u.Donem:dd.MM.yyyy} dönemi.{artis}", $"fatura/{u.FaturaId}",
                taslak ? BildirimOnemi.Uyari : BildirimOnemi.Bilgi));
        }

        var planlar = await db.TekrarPlanlari.AsNoTracking()
            .Where(p => p.Durum == TekrarDurumu.Aktif && (p.SonHata != null || p.ArtisOrani != null))
            .Select(p => new { p.Id, p.Ad, p.SonHata, p.SonrakiTarih, p.ArtisOrani, p.ArtisAyi, p.SonArtisYili }).ToListAsync(cancellationToken);
        foreach (var p in planlar)
        {
            if (p.SonHata is { } hata)
            {
                adaylar.Add(new BildirimAdayi(BildirimTuru.TekrarFatura, $"tekrar-hata:{p.Id}:{p.SonrakiTarih}", $"Fatura kesilemedi: {p.Ad}", hata, $"tekrar-fatura/{p.Id}", BildirimOnemi.Kritik));
            }

            if (p.SonrakiTarih is { } tarih && tarih <= baglam.Bugun.AddDays(ArtisHatirlatmaGunu) && TekrarTakvimi.ArtisVarMi(tarih, p.ArtisOrani, p.ArtisAyi, p.SonArtisYili))
            {
                adaylar.Add(new BildirimAdayi(BildirimTuru.TekrarFatura, $"tekrar-artis:{p.Id}:{tarih.Year}", $"Yıllık artış yaklaşıyor: {p.Ad}",
                    $"{tarih:dd.MM.yyyy} faturasına %{p.ArtisOrani:0.##} artış uygulanacak. Oranı kontrol edin.", $"tekrar-fatura/{p.Id}", BildirimOnemi.Uyari));
            }
        }

        return adaylar;
    }
}
