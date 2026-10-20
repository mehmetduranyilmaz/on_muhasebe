using Microsoft.JSInterop;

namespace OnMuhasebe.Web.Tema;

public enum TemaTercihi
{
    Sistem,
    Acik,
    Koyu
}

// Tercih tarayıcıda (localStorage) saklanır; okunamazsa sistem ayarı kullanılır.
public sealed class TemaYoneticisi(IJSRuntime js)
{
    public TemaTercihi Tercih { get; private set; } = TemaTercihi.Sistem;

    public bool SistemKoyuMu { get; private set; }

    public bool KoyuMu => Tercih == TemaTercihi.Koyu || (Tercih == TemaTercihi.Sistem && SistemKoyuMu);

    public event Action? Degisti;

    public async Task YukleAsync()
    {
        var kayitli = await js.InvokeAsync<string?>("omTema.oku");
        Tercih = Enum.TryParse<TemaTercihi>(kayitli, out var tercih) ? tercih : TemaTercihi.Sistem;
    }

    public async Task DegistirAsync(TemaTercihi tercih)
    {
        Tercih = tercih;
        await js.InvokeVoidAsync("omTema.yaz", tercih.ToString());
        Degisti?.Invoke();
    }

    // Açık ↔ koyu arasında geçiş; kullanıcı bir kez seçince sistem ayarı izlenmez.
    public Task SiradakineGecAsync() => DegistirAsync(KoyuMu ? TemaTercihi.Acik : TemaTercihi.Koyu);

    public void SistemAyariniBildir(bool koyuMu)
    {
        SistemKoyuMu = koyuMu;
        Degisti?.Invoke();
    }
}
