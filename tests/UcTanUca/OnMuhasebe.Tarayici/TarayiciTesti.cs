using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OnMuhasebe.Tarayici;

// Headless Chrome'u DevTools Protocol ile sürer; testler bu sınıfın yardımcılarıyla ekranları gerçek tarayıcıda dener.
internal abstract class TarayiciTesti
{
    protected static string Web => Ortam.Web;
    protected static string Api => Ortam.Api;
    protected static string SistemEposta => Ortam.SistemEposta;
    protected static string SistemSifre => Ortam.SistemSifre;
    protected static string Dosyalar => Ortam.Dosyalar;

    protected readonly HttpClient http = YeniIstemci();
    protected readonly List<JsonNode> olaylar = [];
    protected string[] args = [];

    private ClientWebSocket _ws = null!;
    private int _sayac;
    private int _gecen;
    private int _kalan;
    private string _ekranlar = null!;

    // Kendi imzalı sertifikalı ortamda (ör. deploy ile localhost) OM_GUVENSIZ_SERTIFIKA=1 sertifika doğrulamasını kapatır.
    protected static HttpClient YeniIstemci() => Ortam.GuvensizSertifika
        ? new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator })
        : new HttpClient();

    public abstract string Ad { get; }

    // Testin Chrome'a eklemek istediği argümanlar (ör. sahte kamera).
    protected virtual string ChromeArgumanlari => "";

    // Chrome açılmadan önce hazırlık (ör. test görüntüleri üretmek).
    protected virtual Task HazirlaAsync() => Task.CompletedTask;

    protected abstract Task CalistirAsync();

    public async Task<(int Gecen, int Kalan)> KosAsync(string[] argumanlar)
    {
        args = argumanlar;
        _ekranlar = Path.Combine(Ortam.Cikti, Ad);
        Directory.CreateDirectory(_ekranlar);
        await HazirlaAsync();

        var port = BosPort();
        var profil = Path.Combine(Path.GetTempPath(), "om-cdp-" + Guid.NewGuid().ToString("N"));
        using var chrome = Process.Start(new ProcessStartInfo(Ortam.Chrome,
            $"--headless=new --disable-gpu --no-first-run --no-sandbox --lang=tr-TR --window-size=1400,1000 {(Ortam.GuvensizSertifika ? "--ignore-certificate-errors " : "")}{ChromeArgumanlari}--remote-debugging-port={port} --user-data-dir=\"{profil}\" about:blank")
        { UseShellExecute = false, RedirectStandardError = true })!;
        chrome.ErrorDataReceived += (_, _) => { };
        chrome.BeginErrorReadLine();
        try
        {
            JsonArray? sekmeler = null;
            for (var i = 0; i < 100 && sekmeler is null; i++)
            {
                try { sekmeler = await http.GetFromJsonAsync<JsonArray>($"http://127.0.0.1:{port}/json/list"); }
                catch (HttpRequestException) { await Task.Delay(200); }
            }

            var wsAdres = sekmeler!.First(s => s!["type"]!.GetValue<string>() == "page")!["webSocketDebuggerUrl"]!.GetValue<string>();
            _ws = new ClientWebSocket();
            await _ws.ConnectAsync(new Uri(wsAdres), CancellationToken.None);
            await Komut("Log.enable");
            await CalistirAsync();

            // İçerik güvenlik politikasının (canlıdaki CSP) engellediği bir kaynak ya da script olmamalı.
            var ihlaller = olaylar
                .Select(o => o["method"]?.GetValue<string>() == "Log.entryAdded" ? o["params"]?["entry"]?["text"]?.GetValue<string>() : null)
                .Where(m => m?.Contains("Content Security Policy", StringComparison.Ordinal) == true)
                .ToList();
            Kontrol(ihlaller.Count == 0, "CSP ihlali yok" + (ihlaller.Count > 0 ? $" ({ihlaller.Count}): {ihlaller[0]}" : ""));
        }
        catch (Exception e)
        {
            Kontrol(false, $"Beklenmeyen hata: {e.GetType().Name}: {e.Message}");
        }
        finally
        {
            _ws?.Dispose();
            chrome.Kill(entireProcessTree: true);
            await chrome.WaitForExitAsync();
            try { Directory.Delete(profil, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        return (_gecen, _kalan);
    }

    private static int BosPort()
    {
        using var dinleyici = new TcpListener(IPAddress.Loopback, 0);
        dinleyici.Start();
        return ((IPEndPoint)dinleyici.LocalEndpoint).Port;
    }

    protected async Task<JsonNode?> Komut(string metot, object? p = null)
    {
        var id = ++_sayac;
        await _ws.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { id, method = metot, @params = p ?? new { } })),
            WebSocketMessageType.Text, true, CancellationToken.None);
        var tampon = new byte[1 << 20];
        while (true)
        {
            var sb = new StringBuilder();
            WebSocketReceiveResult r;
            do { r = await _ws.ReceiveAsync(tampon, CancellationToken.None); sb.Append(Encoding.UTF8.GetString(tampon, 0, r.Count)); } while (!r.EndOfMessage);
            var cevap = JsonNode.Parse(sb.ToString())!;
            if (cevap["method"] is not null) olaylar.Add(cevap);
            if (cevap["id"]?.GetValue<int>() == id) return cevap["result"];
        }
    }

    protected async Task<string> Js(string ifade) =>
        (await Komut("Runtime.evaluate", new { expression = ifade, awaitPromise = true, returnByValue = true }))?["result"]?["value"]?.ToString() ?? "";

    protected async Task<bool> Bekle(string kosul, int saniye = 20)
    {
        var son = DateTime.UtcNow.AddSeconds(saniye * Ortam.BeklemeCarpani);
        while (DateTime.UtcNow < son)
        {
            if (await Js($"!!({kosul})") == "true") return true;
            await Task.Delay(250);
        }
        return false;
    }

    protected void Kontrol(bool kosul, string aciklama)
    {
        Console.WriteLine($"{(kosul ? "GECTI" : "KALDI")}  {aciklama}");
        if (kosul) _gecen++; else _kalan++;
    }

    protected static string S(string metin) => JsonSerializer.Serialize(metin);
    protected static string Govde(string metin) => $"document.body.innerText.includes({S(metin)})";

    // Etiketi verilen MudBlazor alanının input'unu bulur (ilk eşleşen; diyalog açıksa diyalog içinde aranır).
    protected static string Alan(string etiket) => $$"""
        (() => { const kok = document.querySelector('.mud-dialog') ?? document;
          const c = [...kok.querySelectorAll('.mud-input-control')].find(x => x.querySelector('label')?.textContent.trim().startsWith({{S(etiket)}}));
          return c?.querySelector('input, textarea'); })()
        """;

    protected async Task Doldur(string etiket, string deger) => await Js($$"""
        (() => { const i = {{Alan(etiket)}};
          const ayarla = Object.getOwnPropertyDescriptor(Object.getPrototypeOf(i), 'value').set; ayarla.call(i, {{S(deger)}});
          i.dispatchEvent(new Event('input', { bubbles: true })); i.dispatchEvent(new Event('change', { bubbles: true }));
          i.dispatchEvent(new Event('blur', { bubbles: true })); })()
        """);

    protected async Task Tikla(string metin) => await Js($$"""
        [...document.querySelectorAll('button, a, .mud-menu-item, .mud-list-item')].find(e => e.textContent.trim().startsWith({{S(metin)}}))?.click()
        """);

    protected async Task Sec(string etiket, string secenek)
    {
        await Js($"(() => {{ const i = {Alan(etiket)}; i.closest('.mud-input-control').querySelector('.mud-input').dispatchEvent(new MouseEvent('mousedown', {{ bubbles: true }})); i.click(); }})()");
        await Bekle($"[...document.querySelectorAll('.mud-popover-open .mud-list-item')].some(e => e.textContent.trim().startsWith({S(secenek)}))", 5);
        await Js($"[...document.querySelectorAll('.mud-popover-open .mud-list-item')].find(e => e.textContent.trim().startsWith({S(secenek)}))?.click()");
        await Task.Delay(300);
    }

    protected async Task Git(string yol) => await Komut("Page.navigate", new { url = Web + yol });

    // MudAutocomplete: metni yazar, açılan listeden metinle başlayan ilk öğeyi seçer.
    protected async Task<bool> Otomatik(string eleman, string yazi, string oge)
    {
        await Js($$"""
            (() => { const i = {{eleman}}; i.focus();
              Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(i, {{S(yazi)}});
              i.dispatchEvent(new Event('input', { bubbles: true })); })()
            """);
        var bulundu = await Bekle($"[...document.querySelectorAll('.mud-popover-open .mud-list-item')].some(e => e.textContent.includes({S(oge)}))", 8);
        await Js($"[...document.querySelectorAll('.mud-popover-open .mud-list-item')].find(e => e.textContent.includes({S(oge)}))?.click()");
        await Task.Delay(600);
        return bulundu;
    }

    // Satır kartındaki alanı (sıra numarası 0'dan) doldurur.
    protected async Task SatirDoldur(int satir, string etiket, string deger) => await Js($$"""
        (() => { const kart = document.querySelectorAll('.fatura-satiri')[{{satir}}];
          const c = [...kart.querySelectorAll('.mud-input-control')].find(x => x.querySelector('label')?.textContent.trim().startsWith({{S(etiket)}}));
          const i = c.querySelector('input');
          Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(i, {{S(deger)}});
          i.dispatchEvent(new Event('input', { bubbles: true })); i.dispatchEvent(new Event('change', { bubbles: true }));
          i.dispatchEvent(new Event('blur', { bubbles: true })); })()
        """);

    protected async Task Kaydet() => await Js("[...document.querySelectorAll('button')].find(b => b.textContent.trim() === 'Kaydet' && !b.disabled)?.click()");

    protected async Task SecAlan(string etiket, string secenek)
    {
        await Bekle("!!" + Alan(etiket), 10);
        await Sec(etiket, secenek);
    }

    protected async Task Ekran(string ad)
    {
        await Task.Delay(900);
        var r = await Komut("Page.captureScreenshot", new { format = "png" });
        await File.WriteAllBytesAsync(Path.Combine(_ekranlar, ad + ".png"), Convert.FromBase64String(r!["data"]!.GetValue<string>()));
    }

    protected async Task DosyaSec(string ad)
    {
        var kok = await Komut("DOM.getDocument", new { depth = 0 });
        var dugum = await Komut("DOM.querySelector", new { nodeId = kok!["root"]!["nodeId"]!.GetValue<int>(), selector = "input[type=file]" });
        await Komut("DOM.setFileInputFiles", new { files = new[] { Path.Combine(Dosyalar, ad) }, nodeId = dugum!["nodeId"]!.GetValue<int>() });
    }

    protected async Task SatirTikla(string metin) =>
        await Js($"[...document.querySelectorAll('.veri-tablosu tbody tr')].find(t => t.textContent.includes({S(metin)}))?.click()");

    protected async Task DiyalogDugmesi(string metin) =>
        await Js($"[...document.querySelectorAll('.mud-dialog button')].find(b => b.textContent.trim() === {S(metin)} && !b.disabled)?.click()");

    protected async Task<string> Satir(string metin) =>
        await Js($"[...document.querySelectorAll('.veri-tablosu tbody tr')].find(t => t.textContent.includes({S(metin)}))?.innerText ?? ''");

    protected async Task<JsonObject> Post(string yol, object govde) =>
        (await (await http.PostAsJsonAsync(Api + yol, govde)).Content.ReadFromJsonAsync<JsonObject>())!;

    protected static string Etiket(string etiket) => $$"""
        (() => { const c = [...document.querySelectorAll('.mud-input-control')].find(x => x.querySelector('label')?.textContent.trim().startsWith({{S(etiket)}}));
          return c?.querySelector('input')?.required ? 'zorunlu' : 'serbest'; })()
        """;

    protected async Task GirisYap(string kim, string sifre)
    {
        await Komut("Network.clearBrowserCookies");
        await Git("/giris");
        await Js("localStorage.clear(); sessionStorage.clear();");
        await Git("/giris");
        await Bekle("document.querySelectorAll('input').length >= 2");
        await Doldur("E-posta", kim);
        await Doldur("Şifre", sifre);
        await Tikla("Giriş Yap");
        await Bekle("!location.pathname.startsWith('/giris')", 15);
        await Task.Delay(800);
    }
}
