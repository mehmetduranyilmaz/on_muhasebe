using OnMuhasebe.Modules.Cari;
using OnMuhasebe.Modules.Doviz;
using OnMuhasebe.Modules.Yonetim;
using OnMuhasebe.Shared.Infrastructure;
using OnMuhasebe.Shared.Infrastructure.Modules;
using OnMuhasebe.Shared.Infrastructure.Persistence;

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
builder.Services.AddModuller(builder.Configuration,
    new YonetimModulu(),
    new DovizModulu(),
    new CariModulu());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await app.Services.MigrateModulDbContextsAsync();
    app.MapOpenApi();
}

// CORS en dışta: hata yanıtlarında da CORS başlıkları bulunsun, tarayıcı hata mesajını okuyabilsin.
app.UseCors();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapModuller();

app.Run();
