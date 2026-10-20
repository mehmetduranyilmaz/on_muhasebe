using OnMuhasebe.Modules.Cari;
using OnMuhasebe.Modules.Doviz;
using OnMuhasebe.Modules.Fatura;
using OnMuhasebe.Modules.KasaBanka;
using OnMuhasebe.Modules.Stok;
using OnMuhasebe.Modules.Yonetim;
using OnMuhasebe.Shared.Infrastructure;
using OnMuhasebe.Shared.Infrastructure.Guvenlik;
using OnMuhasebe.Shared.Infrastructure.Modules;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Tekrar;

var builder = WebApplication.CreateBuilder(args);

// Web ayrı bir adresten çağırır; refresh token çerezi için credentials'a izin verilir.
var izinliKaynaklar = builder.Configuration.GetSection("Cors:IzinliKaynaklar").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(izinliKaynaklar)
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
    new KasaBankaModulu());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await app.Services.MigrateModulDbContextsAsync(app.Configuration);
    app.MapOpenApi();
}

// CORS en dışta: hata yanıtlarında da CORS başlıkları bulunsun, tarayıcı hata mesajını okuyabilsin.
app.UseCors();
app.UseGuvenlikBasliklari(app.Environment);
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

app.Run();
