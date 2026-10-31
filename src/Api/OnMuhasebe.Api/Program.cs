using OnMuhasebe.Modules.Bildirim;
using OnMuhasebe.Modules.Cari;
using OnMuhasebe.Modules.Doviz;
using OnMuhasebe.Modules.Fatura;
using OnMuhasebe.Modules.KasaBanka;
using OnMuhasebe.Modules.Stok;
using OnMuhasebe.Modules.Yonetim;
using OnMuhasebe.Shared.Infrastructure;
using OnMuhasebe.Shared.Infrastructure.Guvenlik;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Modules;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Tekrar;

var builder = WebApplication.CreateBuilder(args);

// Web ayrı bir adresten çağırır; refresh token çerezi için credentials'a izin verilir. Geliştirmede telefondan denemek için
// yerel ağ adresleri (192.168.x, 10.x, 172.16-31.x) de kabul edilir ("telefon" çalıştırma profili).
var izinliKaynaklar = builder.Configuration.GetSection("Cors:IzinliKaynaklar").Get<string[]>() ?? [];
var gelistirme = builder.Environment.IsDevelopment();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .SetIsOriginAllowed(kaynak => izinliKaynaklar.Contains(kaynak) || (gelistirme && YerelAg.KaynakMi(kaynak)))
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddSharedInfrastructure();
builder.Services.AddGuvenlik(builder.Configuration);
builder.Services.AddModuller(builder.Configuration,
    new YonetimModulu(),
    new DovizModulu(),
    new CariModulu(),
    new StokModulu(),
    new FaturaModulu(),
    new KasaBankaModulu(),
    new BildirimModulu());

var app = builder.Build();

// Canlıda veritabanı, API açılmadan önce tek seferlik ayrı bir süreçle güncellenir; yönetici (tablo sahibi) bağlantısı
// yalnızca bu sürece verilir, çalışan API satır güvenliğine tabi uygulama rolüyle bağlanır.
if (args.Contains("--veritabani-guncelle"))
{
    await app.Services.MigrateModulDbContextsAsync(app.Configuration);
    return;
}

if (app.Environment.IsDevelopment())
{
    await app.Services.MigrateModulDbContextsAsync(app.Configuration);
    app.MapOpenApi();
}

// Firma izolasyonunun veritabanı katmanı (RLS) ancak uygulama kısıtlı rolle bağlanırsa işler; değilse API açılmaz.
await VeritabaniGuvenligi.UygulamaRolunuDenetleAsync(app.Configuration.GetConnectionString(PersistenceExtensions.BaglantiAdi)!, CancellationToken.None);

// Vekil başlıkları ilk okunur: sonraki her adım (hız sınırı, HSTS) gerçek istemci IP'sini ve şemayı görsün.
app.UseForwardedHeaders();

// CORS en dışta: hata yanıtlarında da CORS başlıkları bulunsun, tarayıcı hata mesajını okuyabilsin.
app.UseCors();
app.UseGuvenlikBasliklari();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseUcGovdeSiniri();
app.UseTekrarKorumasi();

app.MapHealthChecks("/health");
app.MapModuller();
app.YetkiTanimlariniDenetle();

app.Run();

internal static class YerelAg
{
    public static bool KaynakMi(string kaynak) =>
        Uri.TryCreate(kaynak, UriKind.Absolute, out var adres) && System.Net.IPAddress.TryParse(adres.Host, out var ip)
        && ip.GetAddressBytes() is [10, ..] or [192, 168, ..] or [172, >= 16 and <= 31, ..];
}
