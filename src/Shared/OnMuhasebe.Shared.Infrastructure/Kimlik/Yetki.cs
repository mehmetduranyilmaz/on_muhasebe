using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Modules;
using OnMuhasebe.Shared.Infrastructure.Results;

namespace OnMuhasebe.Shared.Infrastructure.Kimlik;

// Kullanıcının firmadaki etkin yetkileri (rol + kullanıcıya özel ek/yasak + bağımlı yetkiler). Tam yetkili: firmanın yöneticisi.
// Isyerleri: girebildiği işyerleri (boş: tümü; yöneticide her zaman boş). VarsayilanIsyeriId: yeni belgenin açılacağı işyeri.
public sealed record KullaniciYetkileri(bool TamYetkiliMi, IReadOnlyList<string> Yetkiler, IReadOnlyList<Guid> Isyerleri, Guid? VarsayilanIsyeriId)
{
    public static KullaniciYetkileri Yok { get; } = new(false, [], [], null);

    public bool IsyerineGirebilirMi(Guid isyeriId) => Isyerleri.Count == 0 || Isyerleri.Contains(isyeriId);

    public bool VarMi(string yetki) => TamYetkiliMi || Yetkiler.Contains(yetki);
}

public interface IYetkiSorgusu
{
    Task<KullaniciYetkileri> GetirAsync(Guid firmaId, Guid kullaniciId, CancellationToken cancellationToken = default);

    // Rol ya da üyelik değişince firmanın tüm kullanıcılarının önbelleği silinir.
    Task OnbellektenKaldirAsync(Guid firmaId, CancellationToken cancellationToken = default);
}

// Oturumdaki kullanıcı için yetki sorusu; istek boyunca tek kez yüklenir.
public interface IYetkiDenetimi
{
    Task<bool> VarMiAsync(string yetki, CancellationToken cancellationToken = default);

    Task<KullaniciYetkileri> GetirAsync(CancellationToken cancellationToken = default);
}

internal sealed class YetkiDenetimi(IMevcutKullanici mevcutKullanici, IYetkiSorgusu sorgu) : IYetkiDenetimi
{
    private KullaniciYetkileri? _yetkiler;

    public async Task<bool> VarMiAsync(string yetki, CancellationToken cancellationToken = default) =>
        (await GetirAsync(cancellationToken)).VarMi(yetki);

    public async Task<KullaniciYetkileri> GetirAsync(CancellationToken cancellationToken = default) =>
        _yetkiler ??= mevcutKullanici is { FirmaId: { } firmaId, KullaniciId: { } kullaniciId }
            ? await sorgu.GetirAsync(firmaId, kullaniciId, cancellationToken)
            : KullaniciYetkileri.Yok;
}

// Modüllerin bildirdiği tüm yetki kaynakları. Çekirdek kaynaklar (Modul boş) her pakette, diğerleri modül lisansıyla görünür.
public sealed class YetkiKatalogu
{
    private readonly Dictionary<string, YetkiKaynagi> _kaynaklar;

    public YetkiKatalogu(IEnumerable<IModul> moduller)
    {
        Tumu = moduller.SelectMany(m => m.YetkiKaynaklari).ToList();
        _kaynaklar = Tumu.ToDictionary(k => k.Kod);
        Yetkiler = Tumu.SelectMany(k => k.Yetkiler()).ToHashSet();

        if (Tumu.SelectMany(k => k.Gerekenler).FirstOrDefault(g => !Yetkiler.Contains(g)) is { } bilinmeyen)
        {
            throw new InvalidOperationException($"Bağımlı yetki katalogda yok: {bilinmeyen}");
        }
    }

    public IReadOnlyList<YetkiKaynagi> Tumu { get; }

    public IReadOnlySet<string> Yetkiler { get; }

    public YetkiKaynagi? Kaynak(string kod) => _kaynaklar.GetValueOrDefault(kod);

    public IReadOnlyList<YetkiKaynagi> Lisansli(IReadOnlyCollection<string> moduller) =>
        Tumu.Where(k => k.Modul.Length == 0 || moduller.Contains(k.Modul)).ToList();

    // "fatura.belge.onayla" -> "Faturalar - Onayla"
    public string Ad(string yetki)
    {
        var ayrim = yetki.LastIndexOf('.');
        return ayrim > 0 && Kaynak(yetki[..ayrim]) is { } kaynak
            ? $"{kaynak.Ad} - {YetkiIslemleri.Ad(yetki[(ayrim + 1)..])}"
            : yetki;
    }
}

// Ucun istediği yetki. Kaynak genelde gruba, işlem gerekirse uca verilir; işlem verilmezse HTTP metodundan çıkarılır.
public sealed record YetkiGereksinimi(string? Kaynak, string? Islem, bool GerekmezMi);

internal sealed class YetkiFiltresi : IEndpointFilter
{
    public static readonly YetkiFiltresi Ornek = new();

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        // İşyeri kapsamı her istekte (yetki gerekmeyen uçlarda da) yazılır; sorgu filtresi kısıtlı kullanıcıya yalnız izinli işyerlerini verir.
        var denetim = http.RequestServices.GetRequiredService<IYetkiDenetimi>();
        var yetkiler = await denetim.GetirAsync(http.RequestAborted);
        http.RequestServices.GetRequiredService<IsyeriKapsami>().Ata(yetkiler.TamYetkiliMi ? null : yetkiler.Isyerleri);

        var gereken = YetkiCozucu.Coz(http.GetEndpoint()?.Metadata, http.Request.Method);
        if (gereken.GerekmezMi)
        {
            return await next(context);
        }

        if (gereken.Yetki is null)
        {
            return Hata.Yetki("yetki_tanimsiz", "Bu işlem için yetki tanımlanmamış.").Problem();
        }

        if (!yetkiler.VarMi(gereken.Yetki))
        {
            var ad = http.RequestServices.GetRequiredService<YetkiKatalogu>().Ad(gereken.Yetki);
            return Hata.Yetki("yetki_yok", $"Bu işlem için yetkiniz yok ({ad}).").Problem();
        }

        return await next(context);
    }
}

internal static class YetkiCozucu
{
    public static (string? Yetki, bool GerekmezMi) Coz(IReadOnlyList<object>? metadata, string metot)
    {
        string? kaynak = null, islem = null;
        var gerekmez = false;
        foreach (var g in metadata?.OfType<YetkiGereksinimi>() ?? [])
        {
            kaynak = g.Kaynak ?? kaynak;
            islem = g.Islem ?? islem;
            gerekmez |= g.GerekmezMi;
        }

        if (gerekmez)
        {
            return (null, true);
        }

        islem ??= MetottanIslem(metot);
        return (kaynak is null || islem is null ? null : $"{kaynak}.{islem}", false);
    }

    private static string? MetottanIslem(string metot) =>
        HttpMethods.IsGet(metot) || HttpMethods.IsHead(metot) ? YetkiIslemleri.Goruntule
        : HttpMethods.IsPost(metot) ? YetkiIslemleri.Ekle
        : HttpMethods.IsPut(metot) || HttpMethods.IsPatch(metot) ? YetkiIslemleri.Duzenle
        : HttpMethods.IsDelete(metot) ? YetkiIslemleri.Sil
        : null;
}

// Yetki filtresi işaretçisi: açılışta, filtreli her ucun katalogda tanımlı bir yetkiye bağlandığı denetlenir.
internal sealed record YetkiDenetlenirMetadata;

public static class YetkiExtensions
{
    // Modül uçları (RequireModul) yetki filtresini zaten taşır; aşağıdakiler yalnızca hangi yetkinin isteneceğini belirtir.
    public static TBuilder Yetki<TBuilder>(this TBuilder builder, YetkiKaynagi kaynak, string? islem = null)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new YetkiGereksinimi(kaynak.Kod, islem, false));

    public static TBuilder Islem<TBuilder>(this TBuilder builder, string islem)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new YetkiGereksinimi(null, islem, false));

    // Firmanın her üyesinin erişebildiği uçlar (ör. modül ayarlarını okuma); lisans denetimi yine yapılır.
    public static TBuilder YetkiGerekmez<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new YetkiGereksinimi(null, null, true));

    // Modül dışı uçlar için: oturum + firma + yetki filtresi.
    public static TBuilder RequireYetki<TBuilder>(this TBuilder builder, YetkiKaynagi kaynak, string? islem = null)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.RequireAuthorization(p => p.RequireClaim(ClaimTipleri.FirmaId));
        builder.YetkiFiltresiEkle();
        return builder.Yetki(kaynak, islem);
    }

    internal static TBuilder YetkiFiltresiEkle<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new YetkiDenetlenirMetadata());
        builder.AddEndpointFilter(YetkiFiltresi.Ornek);
        return builder;
    }

    // Açılışta: yetki filtreli her uç, katalogdaki bir yetkiye ya da açıkça "yetki gerekmez"e bağlı olmalı.
    // Eksik uç çalışma anında zaten reddedilir (403); geliştirmede API açılmaz ki eksik fark edilsin.
    public static void YetkiTanimlariniDenetle(this WebApplication app) =>
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var katalog = app.Services.GetRequiredService<YetkiKatalogu>();
            var eksikler = new List<string>();
            foreach (var uc in app.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>())
            {
                if (uc.Metadata.GetMetadata<YetkiDenetlenirMetadata>() is null)
                {
                    continue;
                }

                foreach (var metot in uc.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [])
                {
                    var (yetki, gerekmez) = YetkiCozucu.Coz(uc.Metadata, metot);
                    if (!gerekmez && (yetki is null || !katalog.Yetkiler.Contains(yetki)))
                    {
                        eksikler.Add($"{metot} {uc.RoutePattern.RawText} -> {yetki ?? "(yok)"}");
                    }
                }
            }

            if (eksikler.Count == 0)
            {
                return;
            }

            app.Logger.LogCritical("Yetkisi tanımsız uçlar:\n{Uclar}", string.Join('\n', eksikler));
            if (app.Environment.IsDevelopment())
            {
                app.Lifetime.StopApplication();
            }
        });
}
