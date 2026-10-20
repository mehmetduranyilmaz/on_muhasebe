using OnMuhasebe.Shared.Infrastructure;
using OnMuhasebe.Shared.Infrastructure.Modules;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddSharedInfrastructure();
builder.Services.AddModuller(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();

app.MapHealthChecks("/health");
app.MapModuller();

app.Run();
