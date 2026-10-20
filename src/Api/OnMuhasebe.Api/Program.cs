using OnMuhasebe.Modules.Yonetim;
using OnMuhasebe.Shared.Infrastructure;
using OnMuhasebe.Shared.Infrastructure.Modules;
using OnMuhasebe.Shared.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddSharedInfrastructure();
builder.Services.AddModuller(builder.Configuration,
    new YonetimModulu());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await app.Services.MigrateModulDbContextsAsync();
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapModuller();

app.Run();
