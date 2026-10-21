using OnMuhasebe.Modules.Bildirim.Contracts;
using OnMuhasebe.Shared.Contracts.Bildirimler;
using OnMuhasebe.Web.Api;

namespace OnMuhasebe.Web;

// Okunmamış bildirim sayısı: üst çubuktaki zil ve Bildirimler sayfası aynı sayıyı gösterir.
public sealed class BildirimDurumu(ApiIstemcisi api)
{
    public int Okunmamis { get; private set; }

    public event Action? Degisti;

    public async Task YenileAsync()
    {
        var sonuc = await api.GetAsync<BildirimSayaci>("api/bildirim/sayac");
        if (sonuc.BasariliMi && sonuc.Deger.Okunmamis != Okunmamis)
        {
            Okunmamis = sonuc.Deger.Okunmamis;
            Degisti?.Invoke();
        }
    }

    public async Task OkunduAsync(BildirimOzeti b)
    {
        if (!b.OkunduMu && (await api.PostAsync($"api/bildirim/{b.Id}/okundu")).BasariliMi)
        {
            await YenileAsync();
        }
    }

    public async Task TumuOkunduAsync()
    {
        if ((await api.PostAsync("api/bildirim/tumu-okundu")).BasariliMi)
        {
            await YenileAsync();
        }
    }
}

public static class BildirimMetinleri
{
    public static string Ikon(BildirimTuru tur) => tur switch
    {
        BildirimTuru.AlinanEvrakVadesi => MudBlazor.Icons.Material.Filled.CallReceived,
        BildirimTuru.VerilenEvrakVadesi => MudBlazor.Icons.Material.Filled.CallMade,
        BildirimTuru.GecikmisEvrak => MudBlazor.Icons.Material.Filled.EventBusy,
        BildirimTuru.RiskLimiti => MudBlazor.Icons.Material.Filled.GppMaybe,
        BildirimTuru.KarneAzaldi => MudBlazor.Icons.Material.Filled.MenuBook,
        BildirimTuru.AbonelikBitiyor => MudBlazor.Icons.Material.Filled.WorkspacePremium,
        BildirimTuru.OdemeBildirimiSonucu => MudBlazor.Icons.Material.Filled.AccountBalanceWallet,
        _ => MudBlazor.Icons.Material.Filled.Notifications
    };

    public static string Renk(BildirimOnemi onem) => onem switch
    {
        BildirimOnemi.Kritik => "#DC2626",
        BildirimOnemi.Uyari => "#F59E0B",
        _ => "#14B8A6"
    };

    // "az önce", "5 dk önce", "3 sa önce", "dün", "12.10.2026"
    public static string Gecen(DateTime utc, DateTime simdiUtc)
    {
        var fark = simdiUtc - utc;
        return fark.TotalMinutes < 1 ? "az önce"
            : fark.TotalHours < 1 ? $"{(int)fark.TotalMinutes} dk önce"
            : fark.TotalDays < 1 ? $"{(int)fark.TotalHours} sa önce"
            : fark.TotalDays < 2 ? "dün"
            : utc.ToLocalTime().ToString("dd.MM.yyyy");
    }
}
