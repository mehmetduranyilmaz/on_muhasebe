using MudBlazor;

namespace OnMuhasebe.Web.Bilesenler;

// "Emin misiniz?" sorusunu tek satıra indirir: if (!await Onay.SorAsync(...)) return;
public sealed class OnayServisi(IDialogService dialogService)
{
    public async Task<bool> SorAsync(string baslik, string mesaj, string onayMetni = "Onayla", bool tehlikeli = false)
    {
        var parametreler = new DialogParameters<OnayDialog>
        {
            { d => d.Mesaj, mesaj },
            { d => d.OnayMetni, onayMetni },
            { d => d.Tehlikeli, tehlikeli }
        };
        var dialog = await dialogService.ShowAsync<OnayDialog>(baslik, parametreler,
            new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true });
        return await dialog.Result is { Canceled: false };
    }
}
