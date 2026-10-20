using System.Globalization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;
using OnMuhasebe.Web;
using OnMuhasebe.Web.Api;
using OnMuhasebe.Web.Kimlik;

var turkce = new CultureInfo("tr-TR");
CultureInfo.DefaultThreadCurrentCulture = turkce;
CultureInfo.DefaultThreadCurrentUICulture = turkce;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Boş bırakılırsa Web ile aynı adresten sunulan API kullanılır.
var apiAdresi = builder.Configuration["ApiAdresi"] is { Length: > 0 } adres
    ? new Uri(adres)
    : new Uri(builder.HostEnvironment.BaseAddress);

builder.Services.AddMudServices(options =>
{
    options.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    options.SnackbarConfiguration.VisibleStateDuration = 4000;
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddTransient<CerezHandler>();
builder.Services.AddTransient<ApiYetkiHandler>();
builder.Services.AddHttpClient(OturumYoneticisi.KimlikIstemcisi, c => c.BaseAddress = apiAdresi)
    .AddHttpMessageHandler<CerezHandler>();
builder.Services.AddHttpClient(ApiIstemcisi.Ad, c => c.BaseAddress = apiAdresi)
    .AddHttpMessageHandler<ApiYetkiHandler>()
    .AddHttpMessageHandler<CerezHandler>();

// Handler'lar ayrı DI kapsamında oluşturulduğu için oturum singleton olmalı; aksi halde farklı örnekler görürler.
builder.Services.AddSingleton<OturumYoneticisi>();
builder.Services.AddScoped<ApiIstemcisi>();
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, OturumAuthStateProvider>();

var host = builder.Build();

// İlk ekran çizilmeden önce çerezdeki oturum varsa sessizce açılır.
await host.Services.GetRequiredService<OturumYoneticisi>().SessizGirisAsync();
await host.RunAsync();
