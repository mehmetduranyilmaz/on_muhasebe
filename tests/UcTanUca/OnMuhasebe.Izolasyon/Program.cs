// Firmalar arası izolasyon testi: A firmasının kayıtlarına B firmasının token'ıyla API'deki her uçtan erişim denenir.
// Uç listesi OpenAPI belgesinden okunur; yeni uç eklendiğinde eşlemesi yoksa "eşlemesiz uç" olarak raporlanır.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;

var Api = (Environment.GetEnvironmentVariable("OM_API") is { Length: > 0 } api ? api : "http://localhost:5128").TrimEnd('/') + "/";
var sistemEposta = Environment.GetEnvironmentVariable("OM_SISTEM_EPOSTA") is { Length: > 0 } se ? se : "sistem@onmuhasebe.local";
var sistemSifre = Environment.GetEnvironmentVariable("OM_SISTEM_SIFRE") ?? throw new InvalidOperationException("OM_SISTEM_SIFRE ortam değişkeni tanımlı değil.");
var dosyalar = Environment.GetEnvironmentVariable("OM_DOSYALAR") is { Length: > 0 } d ? d : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Dosyalar"));
var gecen = 0; var kalan = 0; var uyari = 0;
void Kontrol(bool kosul, string aciklama)
{
    Console.WriteLine($"{(kosul ? "GECTI" : "KALDI")}  {aciklama}");
    if (kosul) gecen++; else kalan++;
}

HttpClient Istemci(string? token)
{
    var h = new HttpClient { BaseAddress = new Uri(Api) };
    if (token is not null) h.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return h;
}

async Task<JsonNode> Post(HttpClient h, string yol, object govde)
{
    var c = await h.PostAsJsonAsync(yol, govde);
    var metin = await c.Content.ReadAsStringAsync();
    if (!c.IsSuccessStatusCode) throw new InvalidOperationException($"{yol} -> {(int)c.StatusCode} {metin}");
    return JsonNode.Parse(metin)!;
}

var mailpit = (Environment.GetEnvironmentVariable("OM_MAILPIT") is { Length: > 0 } mp ? mp : "http://localhost:8025").TrimEnd('/');

// Adrese giden son e-postadaki bağlantı kodu (eposta-dogrula, davet).
async Task<string> MailpitKodu(string eposta, string yol)
{
    using var h = new HttpClient();
    for (var i = 0; i < 30; i++)
    {
        var arama = JsonNode.Parse(await h.GetStringAsync($"{mailpit}/api/v1/search?query=to:{eposta}"))!;
        if (arama["messages"]?.AsArray().FirstOrDefault()?["ID"]?.GetValue<string>() is { } mesajId)
        {
            var mesaj = await h.GetStringAsync($"{mailpit}/api/v1/message/{mesajId}");
            var m = System.Text.RegularExpressions.Regex.Match(mesaj, yol + "#kod=([A-Za-z0-9_-]+)");
            if (m.Success) return m.Groups[1].Value;
        }

        await Task.Delay(300);
    }

    throw new InvalidOperationException($"{eposta} için {yol} e-postası gelmedi");
}

// Kayıt + e-postadaki bağlantıyla doğrulama.
async Task<string> Kayit(string unvan)
{
    var eposta = $"izo{Guid.NewGuid():N}@ornek.com";
    (await Istemci(null).PostAsJsonAsync("api/yonetim/kayit", new { firmaUnvani = unvan, ad = "I", soyad = "Z", eposta, sifre = "Sifre1234" })).EnsureSuccessStatusCode();
    var c = await Post(Istemci(null), "api/yonetim/eposta-dogrula", new { kod = await MailpitKodu(eposta, "eposta-dogrula") });
    return c["accessToken"]!.GetValue<string>();
}

var tokenA = await Kayit("SIZINTI-A Firma");
var tokenB = await Kayit("Temiz-B Firma");
var a = Istemci(tokenA);
var b = Istemci(tokenB);

// --- A firmasında her modülde kayıt
var id = new Dictionary<string, string>();
id["cariGrup"] = (await Post(a, "api/cari/gruplar", new { ad = "SIZINTI Grup" }))["id"]!.GetValue<string>();
id["cari"] = (await Post(a, "api/cari/hesaplar", new { unvan = "SIZINTI Cari", tur = "Musteri", kisiTipi = "Tuzel", paraBirimi = "TRY", grupId = id["cariGrup"] }))["id"]!.GetValue<string>();
id["cariHareket"] = (await Post(a, $"api/cari/hesaplar/{id["cari"]}/hareketler", new { tur = "Dekont", yon = "Borc", tarih = "2026-10-01", tutar = 100, aciklama = "SIZINTI dekont" }))["id"]!.GetValue<string>();
id["stokGrup"] = (await Post(a, "api/stok/gruplar", new { ad = "SIZINTI Stok Grup" }))["id"]!.GetValue<string>();
id["depo"] = (await Post(a, "api/stok/depolar", new { ad = "SIZINTI Depo" }))["id"]!.GetValue<string>();
id["stokKart"] = (await Post(a, "api/stok/kartlar", new { ad = "SIZINTI Kalem", tur = "Urun", temelBirim = "ADET", kdvOrani = 20, paraBirimi = "TRY", barkod = "8699999000017" }))["id"]!.GetValue<string>();
id["barkod"] = "8699999000017";
id["stokHareket"] = (await Post(a, $"api/stok/kartlar/{id["stokKart"]}/hareketler", new { tur = "Acilis", tarih = "2026-01-01", birim = "ADET", miktar = 5, birimFiyat = 10 }))["hareketler"]![0]!["id"]!.GetValue<string>();
id["fatura"] = (await Post(a, "api/fatura/faturalar", new
{
    tur = "Satis", cariHesapId = id["cari"], tarih = "2026-10-10", genelIskontoTuru = "Yuzde", genelIskonto = 0, aciklama = "SIZINTI fatura",
    satirlar = new[] { new { ad = "SIZINTI hizmet", birim = "ADET", miktar = 1, birimFiyat = 100, kdvOrani = 20, kdvDahilMi = false, iskonto1 = 0, iskonto2 = 0, iskonto3 = 0 } }
}))["fatura"]!["id"]!.GetValue<string>();
id["kasaHesap"] = (await Post(a, "api/kasabanka/hesaplar", new { ad = "SIZINTI Banka", tur = "Banka", paraBirimi = "TRY" }))["id"]!.GetValue<string>();
var kategoriler = JsonNode.Parse(await a.GetStringAsync("api/kasabanka/kategoriler"))!.AsArray();
id["kategori"] = kategoriler[0]!["id"]!.GetValue<string>();
id["kasaIslem"] = (await Post(a, "api/kasabanka/islemler/tahsilat", new { hesapId = id["kasaHesap"], cariHesapId = id["cari"], tarih = "2026-10-11", tutar = 50, aciklama = "SIZINTI tahsilat" }))["islemId"]!.GetValue<string>();
var belgeSatiri = new { stokKartiId = id["stokKart"], ad = "SIZINTI satir", birim = "ADET", miktar = 1, birimFiyat = 100, kdvOrani = 20, kdvDahilMi = false, iskonto1 = 0, iskonto2 = 0, iskonto3 = 0 };
id["irsaliye"] = (await Post(a, "api/fatura/irsaliyeler", new
{
    tur = "Satis", cariHesapId = id["cari"], tarih = "2026-10-12", sevkTarihi = "2026-10-12", sevkSaati = "10:00:00", aciklama = "SIZINTI irsaliye", tasiyiciUnvani = "SIZINTI Kargo", tasiyiciVkn = "1234567890",
    satirlar = new[] { new { stokKartiId = id["stokKart"], ad = "SIZINTI satir", birim = "ADET", miktar = 1, birimFiyat = 100 } }
}))["irsaliye"]!["id"]!.GetValue<string>();
id["teklif"] = (await Post(a, "api/fatura/teklif-siparis", new
{
    tur = "SatisTeklifi", cariHesapId = id["cari"], tarih = "2026-10-12", genelIskontoTuru = "Yuzde", genelIskonto = 0, aciklama = "SIZINTI teklif",
    satirlar = new[] { belgeSatiri }
}))["id"]!.GetValue<string>();
id["masrafMerkezi"] = (await Post(a, "api/kasabanka/masraf-merkezleri", new { kod = "SZ1", ad = "SIZINTI Masraf" }))["id"]!.GetValue<string>();
id["demirbas"] = (await Post(a, "api/kasabanka/demirbaslar", new
{
    ad = "SIZINTI Demirbas", grup = "Bilgisayar", binekOtomobilMi = false, edinmeTarihi = "2026-01-15", edinmeBedeli = 30000, faydaliOmur = 4, yontem = "Normal"
}))["id"]!.GetValue<string>();
id["kredi"] = (await Post(a, "api/kasabanka/krediler", new
{
    ad = "SIZINTI Kredi", tur = "TaksitliTicari", hesapId = id["kasaHesap"], kullandirimTarihi = "2026-10-01", tutar = 1000, aylikFaizOrani = 2, kullandirimMasrafi = 0,
    taksitler = new[] { new { sira = 1, vade = "2026-11-01", anapara = 1000, faiz = 20, vergi = 1 } }
}))["id"]!.GetValue<string>();
id["krediSira"] = "1";
id["muhasebeTur"] = "Cari";
// Şube eklemesi yalnızca kimliği (JSON metni) döner.
id["sube"] = (await Post(a, "api/kasabanka/subeler", new { bankaAdi = "SIZINTI Bank", ad = "SIZINTI Sube" })).GetValue<string>();
id["teminat"] = (await Post(a, "api/kasabanka/teminatlar", new
{
    tur = "TeminatMektubu", yon = "Alinan", cariHesapId = id["cari"], bankaAdi = "SIZINTI Bank", tutar = 500, paraBirimi = "TRY", verilisTarihi = "2026-10-01", aciklama = "SIZINTI teminat"
})).GetValue<string>();
id["fiyatListesi"] = (await Post(a, "api/stok/fiyat-listeleri", new
{
    ad = "SIZINTI Liste", tur = "Satis", paraBirimi = "TRY", kdvDahilMi = false, aktifMi = true, hedefler = Array.Empty<object>(),
    satirlar = new[] { new { stokKartiId = id["stokKart"], minMiktar = 0, fiyat = 90, iskonto = 0 } }
}))["id"]!.GetValue<string>();
using (var form = new MultipartFormDataContent { { new ByteArrayContent(await File.ReadAllBytesAsync(Path.Combine(dosyalar, "banka.csv"))), "dosya", "banka.csv" } })
{
    var c = await a.PostAsync($"api/kasabanka/hesaplar/{id["kasaHesap"]}/ekstre-aktarimlari", form);
    id["aktarim"] = JsonNode.Parse(await c.Content.ReadAsStringAsync())!["id"]!.GetValue<string>();
}
var esleme = await a.PutAsJsonAsync($"api/kasabanka/ekstre-aktarimlari/{id["aktarim"]}/esleme", new
{
    esleme = new { baslikSatiri = 2, tarihKolonu = 0, aciklamaKolonu = 1, tutarKolonu = (int?)null, alacakKolonu = 3, borcKolonu = 2, bakiyeKolonu = 4, referansKolonu = (int?)null, tutarTersMi = false },
    sablonuKaydet = false
});
id["aktarimSatir"] = JsonNode.Parse(await esleme.Content.ReadAsStringAsync())!["satirlar"]![0]!["id"]!.GetValue<string>();
id["kural"] = (await Post(a, "api/kasabanka/ekstre-kurallari", new { anahtarKelime = "SIZINTI", yon = "Cikis", islemTuru = "Gider" }))["id"]!.GetValue<string>();
var bordro = await Post(a, "api/kasabanka/cek-senet/giris", new
{
    cariHesapId = id["cari"], tarih = "2026-10-12", aciklama = "SIZINTI bordro", riskOnaylandi = false,
    evraklar = new[] { new { tur = "Senet", seriNo = "SIZINTI-1", vadeTarihi = "2026-12-01", tutar = 75, paraBirimi = "TRY", borclu = new { ad = "SIZINTI Borclu" } } }
});
id["bordro"] = bordro["bordroId"]!.GetValue<string>();
id["evrak"] = bordro["evrakIdleri"]![0]!.GetValue<string>();
id["borclu"] = JsonNode.Parse(await a.GetStringAsync("api/kasabanka/cek-senet/borclular"))!["kayitlar"]![0]!["id"]!.GetValue<string>();
var fisListesi = JsonNode.Parse(await a.GetStringAsync("api/muhasebe/fisler?baslangic=2026-01-01&bitis=2026-12-31"))!;
id["muhasebeAktarim"] = (await Post(a, "api/muhasebe/aktarimlar", new { baslangic = "2026-01-01", bitis = "2026-12-31", parmak = fisListesi["parmak"]!.GetValue<string>() }))["id"]!.GetValue<string>();
var roller = JsonNode.Parse(await a.GetStringAsync("api/yonetim/roller"))!.AsArray();
var kullaniciRolu = roller.First(r => r!["ad"]!.GetValue<string>() == "Kullanıcı")!["id"]!.GetValue<string>();
var yoneticiRolu = roller.First(r => r!["tamYetkiliMi"]!.GetValue<bool>())!["id"]!.GetValue<string>();
id["isyeri"] = (await Post(a, "api/yonetim/isyerleri", new { kod = "SZN", ad = "SIZINTI Sube" }))["id"]!.GetValue<string>();
id["rol"] = (await Post(a, "api/yonetim/roller", new { ad = "SIZINTI Rol", yetkiler = (string[])["cari.hesap.goruntule"] }))["id"]!.GetValue<string>();
var kullaniciEposta = $"izok{Guid.NewGuid():N}@ornek.com";
await Post(a, "api/yonetim/kullanicilar", new { eposta = kullaniciEposta, rolId = kullaniciRolu });
await Post(Istemci(null), "api/yonetim/davet-kabul", new { kod = await MailpitKodu(kullaniciEposta, "davet"), ad = "SIZINTI", soyad = "Kullanici", sifre = "Sifre1234" });
id["kullanici"] = JsonNode.Parse(await a.GetStringAsync("api/yonetim/kullanicilar"))!.AsArray()
    .First(u => u!["eposta"]!.GetValue<string>() == kullaniciEposta)!["kullaniciId"]!.GetValue<string>();
id["davet"] = (await Post(a, "api/yonetim/kullanicilar", new { eposta = $"izod{Guid.NewGuid():N}@ornek.com", rolId = kullaniciRolu }))["id"]!.GetValue<string>();
var benA = JsonNode.Parse(await a.GetStringAsync("api/yonetim/ben"))!;
id["firma"] = benA["aktifFirma"]!["id"]!.GetValue<string>();
// Bildirim için sistemde ödeme hesabı ve Temel paketin yıllık ücreti tanımlı olmalı (abonelik_senaryo kurar).
id["bildirim"] = (await Post(a, "api/yonetim/abonelik/odeme-bildirimleri", new { paketKodu = "basic", aySayisi = 12, tutar = 5000, odemeTarihi = "2026-10-20", gonderen = "SIZINTI Odeme" }))["id"]!.GetValue<string>();
id["paket"] = "basic";
id["karne"] = (await Post(a, "api/kasabanka/cek-senet/karneler", new { tur = "Cek", kendiHesapId = id["kasaHesap"], baslangicSeri = "SZ0001", yaprakSayisi = 5, tarih = "2026-10-01", aciklama = "SIZINTI karne" }))["id"]!.GetValue<string>();
await Post(a, $"api/kasabanka/cek-senet/karneler/{id["karne"]}/yaprak-iptal", new { seriNo = "SZ0002", neden = "SIZINTI yirtik" });
id["karneSeri"] = "SZ0002";
// Bildirim: vade hatırlatması 60 güne çekilir, sistem yöneticisi üretimi çalıştırır; A'nın bir bildirimi alınır.
(await a.PutAsJsonAsync("api/bildirim/ayarlar", new { vadeOncesiGun = 60, kapaliTurler = Array.Empty<string>() })).EnsureSuccessStatusCode();
var sistem = Istemci((await Post(Istemci(null), "api/yonetim/giris", new { eposta = sistemEposta, sifre = sistemSifre }))["accessToken"]!.GetValue<string>());
await Post(sistem, "api/bildirim/sistem/calistir", new { });
id["bildirimKaydi"] = JsonNode.Parse(await a.GetStringAsync("api/bildirim?boyut=1"))!["kayitlar"]![0]!["id"]!.GetValue<string>();
Console.WriteLine($"A firmasında {id.Count} kayıt açıldı.");

// --- Yol şablonu -> A'nın hangi kaydı. Anahtar: parametreden önceki yol (parametre adlarıyla birlikte).
var eslemeler = new (string Desen, string Param, string Anahtar)[]
{
    ("/api/cari/gruplar/", "id", "cariGrup"),
    ("/api/cari/hareketler/", "hareketId", "cariHareket"),
    ("/api/cari/hesaplar/", "id", "cari"),
    ("/api/cari/hesaplar/", "cariId", "cari"),
    ("/api/fatura/faturalar/", "id", "fatura"),
    ("/api/kasabanka/cek-senet/borclular/", "id", "borclu"),
    ("/api/kasabanka/cek-senet/bordrolar/", "id", "bordro"),
    ("/api/kasabanka/cek-senet/cari-riski/", "cariId", "cari"),
    ("/api/kasabanka/cek-senet/evraklar/", "id", "evrak"),
    ("/api/kasabanka/cek-senet/karneler/", "id", "karne"),
    ("/api/kasabanka/cek-senet/karneler/", "seriNo", "karneSeri"),
    ("/api/kasabanka/ekstre-aktarimlari/", "id", "aktarim"),
    ("/api/kasabanka/ekstre-aktarimlari/", "satirId", "aktarimSatir"),
    ("/api/kasabanka/ekstre-kurallari/", "id", "kural"),
    ("/api/kasabanka/hesaplar/", "id", "kasaHesap"),
    ("/api/kasabanka/hesaplar/", "hesapId", "kasaHesap"),
    ("/api/kasabanka/islemler/", "islemId", "kasaIslem"),
    ("/api/kasabanka/kategoriler/", "id", "kategori"),
    ("/api/stok/depolar/", "id", "depo"),
    ("/api/stok/gruplar/", "id", "stokGrup"),
    ("/api/stok/kartlar/barkod/", "barkod", "barkod"),
    ("/api/stok/kartlar/", "id", "stokKart"),
    ("/api/stok/kartlar/", "kartId", "stokKart"),
    ("/api/stok/hareketler/", "hareketId", "stokHareket"),
    ("/api/yonetim/kullanicilar/davetler/", "id", "davet"),
    ("/api/yonetim/kullanicilar/", "kullaniciId", "kullanici"),
    ("/api/yonetim/roller/", "id", "rol"),
    ("/api/yonetim/isyerleri/", "id", "isyeri"),
    ("/api/fatura/isyeri-serileri/", "isyeriId", "isyeri"),
    ("/api/muhasebe/eslemeler/", "tur", "muhasebeTur"),
    ("/api/muhasebe/eslemeler/", "kartId", "cari"),
    ("/api/muhasebe/aktarimlar/", "id", "muhasebeAktarim"),
    ("/api/fatura/irsaliyeler/", "id", "irsaliye"),
    ("/api/fatura/teklif-siparis/", "id", "teklif"),
    ("/api/fatura/raporlar/ba-bs/cari/", "cariId", "cari"),
    ("/api/kasabanka/demirbaslar/", "id", "demirbas"),
    ("/api/kasabanka/krediler/", "id", "kredi"),
    ("/api/kasabanka/krediler/", "sira", "krediSira"),
    ("/api/kasabanka/masraf-merkezleri/", "id", "masrafMerkezi"),
    ("/api/kasabanka/subeler/", "id", "sube"),
    ("/api/kasabanka/teminatlar/", "id", "teminat"),
    ("/api/stok/fiyat-listeleri/", "id", "fiyatListesi"),
    ("/api/yonetim/sistem/odeme-bildirimleri/", "id", "bildirim"),
    ("/api/bildirim/", "id", "bildirimKaydi"),
    ("/api/yonetim/sistem/paketler/", "kod", "paket"),
    ("/api/yonetim/sistem/firmalar/", "firmaId", "firma")
};

var openApi = JsonNode.Parse(await new HttpClient().GetStringAsync(Api + "openapi/v1.json"))!;
var islemler = new List<(string Metot, string Yol)>();
foreach (var (yol, metotlar) in openApi["paths"]!.AsObject())
{
    foreach (var (metot, _) in metotlar!.AsObject())
    {
        islemler.Add((metot.ToUpperInvariant(), yol));
    }
}

var aIdleri = id.Where(k => k.Key is not ("barkod" or "paket" or "krediSira" or "muhasebeTur")).Select(k => k.Value).ToHashSet();
bool SizintiVar(string govde) => govde.Contains("SIZINTI", StringComparison.Ordinal) || aIdleri.Any(x => govde.Contains(x, StringComparison.OrdinalIgnoreCase));

// Silmeler en sona: yanlışlıkla başarılı olursa diğer denemeleri etkilemesin.
var parametreli = islemler.Where(i => i.Yol.Contains('{')).OrderBy(i => i.Metot == "DELETE").ThenBy(i => i.Yol).ToList();
Console.WriteLine($"--- {parametreli.Count} parametreli uç B firmasının token'ıyla deneniyor");
foreach (var (metot, sablon) in parametreli)
{
    var yol = sablon;
    var eksik = false;
    foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(sablon, @"\{(\w+)\}"))
    {
        var onek = sablon[..m.Index];
        var e = eslemeler.Where(x => onek.EndsWith(x.Desen, StringComparison.Ordinal) || (onek.Contains(x.Desen, StringComparison.Ordinal) && x.Param == m.Groups[1].Value))
            .FirstOrDefault(x => x.Param == m.Groups[1].Value);
        if (e.Anahtar is null)
        {
            eksik = true;
            break;
        }

        yol = yol.Replace(m.Value, id[e.Anahtar]);
    }

    if (eksik)
    {
        Console.WriteLine($"UYARI  Eşlemesiz uç (test kapsamı dışında): {metot} {sablon}");
        uyari++;
        continue;
    }

    var istek = new HttpRequestMessage(new HttpMethod(metot), yol.TrimStart('/'));
    if (metot is "POST" or "PUT")
    {
        istek.Content = new StringContent("{}", Encoding.UTF8, "application/json");
    }

    var cevap = await b.SendAsync(istek);
    var govde = await cevap.Content.ReadAsStringAsync();
    var kod = (int)cevap.StatusCode;
    if (kod >= 500)
    {
        Kontrol(false, $"[{kod}] {metot} {sablon}: sunucu hatası");
    }
    else if (metot == "GET")
    {
        Kontrol(kod >= 400 || !SizintiVar(govde), $"[{kod}] {metot} {sablon}: A'nın verisi görünmedi");
    }
    else
    {
        Kontrol(kod >= 400, $"[{kod}] {metot} {sablon}: A'nın kaydında değişiklik yapılamadı");
    }
}

Console.WriteLine("--- Parametresiz liste uçlarında A'nın verisi");
foreach (var (_, yol) in islemler.Where(i => i.Metot == "GET" && !i.Yol.Contains('{') && i.Yol.StartsWith("/api/", StringComparison.Ordinal)))
{
    var cevap = await b.GetAsync(yol.TrimStart('/'));
    var govde = await cevap.Content.ReadAsStringAsync();
    Kontrol((int)cevap.StatusCode < 500 && !SizintiVar(govde), $"[{(int)cevap.StatusCode}] GET {yol}");
}

Console.WriteLine("--- Sorgu parametresiyle A'nın kimliği");
foreach (var yol in new[]
{
    $"api/kasabanka/cek-senet/evraklar?cariId={id["cari"]}", $"api/kasabanka/cek-senet/evraklar?borcluId={id["borclu"]}",
    $"api/kasabanka/cek-senet/risk?borcluId={id["borclu"]}&cariId={id["cari"]}", $"api/fatura/faturalar?cariId={id["cari"]}",
    $"api/cari/hesaplar?grupId={id["cariGrup"]}", $"api/stok/kartlar?grupId={id["stokGrup"]}"
})
{
    var cevap = await b.GetAsync(yol);
    var govde = await cevap.Content.ReadAsStringAsync();
    Kontrol((int)cevap.StatusCode < 500 && !SizintiVar(govde), $"[{(int)cevap.StatusCode}] GET /{yol}");
}

Console.WriteLine("--- A'nın kayıtları yerinde");
var cari = JsonNode.Parse(await a.GetStringAsync($"api/cari/hesaplar/{id["cari"]}"))!;
Kontrol(cari["aktifMi"]!.GetValue<bool>() && cari["unvan"]!.GetValue<string>() == "SIZINTI Cari", "A carisi değişmedi");
Kontrol((await a.GetAsync($"api/fatura/faturalar/{id["fatura"]}")).StatusCode == HttpStatusCode.OK, "A faturası duruyor");
Kontrol((await a.GetAsync($"api/stok/kartlar/{id["stokKart"]}")).StatusCode == HttpStatusCode.OK, "A stok kartı duruyor");
var kullanicilar = await a.GetStringAsync("api/yonetim/kullanicilar");
Kontrol(kullanicilar.Contains(kullaniciEposta, StringComparison.Ordinal) && !kullanicilar.Contains("\"aktifMi\":false", StringComparison.Ordinal), "A kullanıcısı pasifleştirilmedi");
Kontrol(JsonNode.Parse(await a.GetStringAsync("api/yonetim/kullanicilar/davetler"))!.AsArray().Any(d => d!["id"]!.GetValue<string>() == id["davet"]), "A'nın bekleyen daveti iptal edilmedi");

Console.WriteLine("--- Gövdede başka firmanın kimlikleri");
Kontrol((await b.PostAsJsonAsync("api/kasabanka/ekstre-kurallari", new { anahtarKelime = "SIZINTI", yon = "Giris", islemTuru = "Tahsilat", cariHesapId = id["cari"] })).StatusCode == HttpStatusCode.NotFound,
    "B, A'nın carisini ekstre kuralına yazamaz (404)");
Kontrol((await b.PostAsJsonAsync("api/kasabanka/ekstre-kurallari", new { anahtarKelime = "SIZINTI", yon = "Cikis", islemTuru = "Virman", karsiHesapId = id["kasaHesap"] })).StatusCode == HttpStatusCode.NotFound,
    "B, A'nın kasa hesabını ekstre kuralına yazamaz (404)");

Console.WriteLine("--- Rol kontrolleri");
var kGiris = await Post(Istemci(null), "api/yonetim/giris", new { eposta = kullaniciEposta, sifre = "Sifre1234" });
var k = Istemci(kGiris["accessToken"]!.GetValue<string>());
Kontrol((await k.PutAsJsonAsync("api/fatura/ayarlar", new { })).StatusCode == HttpStatusCode.Forbidden, "Kullanıcı rolü fatura ayarlarını değiştiremez (403)");
Kontrol((await k.PutAsJsonAsync("api/yonetim/zorunlu-alanlar", new { secili = Array.Empty<string>() })).StatusCode == HttpStatusCode.Forbidden, "Kullanıcı rolü zorunlu alanları değiştiremez (403)");
Kontrol((await k.PostAsJsonAsync("api/yonetim/kullanicilar", new { eposta = "x@y.com", rolId = yoneticiRolu })).StatusCode == HttpStatusCode.Forbidden, "Kullanıcı rolü kullanıcı ekleyemez (403)");
Kontrol((await a.GetAsync("api/yonetim/sistem/firmalar")).StatusCode == HttpStatusCode.Forbidden, "Firma yöneticisi sistem yönetimine erişemez (403)");
Kontrol((await Istemci(null).GetAsync($"api/cari/hesaplar/{id["cari"]}")).StatusCode == HttpStatusCode.Unauthorized, "Oturumsuz istek reddedilir (401)");
Kontrol((await Istemci("bozuk.token.degeri").GetAsync("api/cari/hesaplar")).StatusCode == HttpStatusCode.Unauthorized, "Sahte token reddedilir (401)");

// Eksik gövdeyle gelen istek 400 almalı, sunucu hatası (500) değil. A'nın verisini değiştirebileceği için en sonda çalışır.
Console.WriteLine("--- Boş gövdeyle tüm yazma uçları (sağlamlık)");
foreach (var (metot, sablon) in islemler.Where(i => i.Metot is "POST" or "PUT").OrderBy(i => i.Yol))
{
    var yol = System.Text.RegularExpressions.Regex.Replace(sablon, @"\{(\w+)\}", m =>
    {
        var onek = sablon[..m.Index];
        var e = eslemeler.FirstOrDefault(x => x.Param == m.Groups[1].Value && onek.Contains(x.Desen, StringComparison.Ordinal));
        return e.Anahtar is null ? Guid.NewGuid().ToString() : id[e.Anahtar];
    });
    var cevap = await a.SendAsync(new HttpRequestMessage(new HttpMethod(metot), yol.TrimStart('/'))
    {
        Content = new StringContent("{}", Encoding.UTF8, "application/json")
    });
    Kontrol((int)cevap.StatusCode < 500, $"[{(int)cevap.StatusCode}] {metot} {sablon} boş gövde");
}

Console.WriteLine($"\nSONUC: {gecen} gecti, {kalan} kaldi, {uyari} eslemesiz uc");

// Eşlemesiz uç da hata sayılır: yeni bir uç izolasyon denetimi olmadan eklenemez.
return kalan == 0 && uyari == 0 ? 0 : 1;
