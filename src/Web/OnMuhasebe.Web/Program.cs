using System.Globalization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;
using OnMuhasebe.Web;
using OnMuhasebe.Web.Kimlik;

var turkce = new CultureInfo("tr-TR");
CultureInfo.DefaultThreadCurrentCulture = turkce;
CultureInfo.DefaultThreadCurrentUICulture = turkce;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Boş bırakılırsa Web ile aynı adresten sunulan API kullanılır. Ayar "localhost" gösterirken sayfa başka bir adresten (ör. telefondan
// bilgisayarın yerel ağ IP'siyle) açılmışsa API de o adreste aranır.
var sayfa = new Uri(builder.HostEnvironment.BaseAddress);
var apiAdresi = builder.Configuration["ApiAdresi"] is { Length: > 0 } adres ? new Uri(adres) : sayfa;
if (apiAdresi.IsLoopback && !sayfa.IsLoopback)
{
    apiAdresi = new UriBuilder(apiAdresi) { Host = sayfa.Host }.Uri;
}

builder.Services.AddMudServices(options =>
{
    options.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    options.SnackbarConfiguration.VisibleStateDuration = 4000;
});
builder.Services.AddOrtakServisler(apiAdresi);
builder.Services.AddSingleton<ModulYukleyici>();
builder.Services.AddScoped<BildirimDurumu>();

var host = builder.Build();

// İlk ekran çizilmeden önce çerezdeki oturum varsa sessizce açılır.
await host.Services.GetRequiredService<OturumYoneticisi>().SessizGirisAsync();
await host.RunAsync();
