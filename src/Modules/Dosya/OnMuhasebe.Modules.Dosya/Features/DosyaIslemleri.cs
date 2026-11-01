using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Dosya.Domain;
using OnMuhasebe.Modules.Dosya.Infrastructure;
using OnMuhasebe.Shared.Contracts.Dosyalar;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Dosyalar;
using OnMuhasebe.Shared.Infrastructure.Guvenlik;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Results;

namespace OnMuhasebe.Modules.Dosya.Features;

// Kayıtlara dosya ekleme. Her işlemde hem dosya yetkisi hem bağlı kaydı görme yetkisi aranır; kayıt sahibi modülün filtrelerinden geçer.
internal static class DosyaIslemleri
{
    private static readonly Hata Bulunamadi = Hata.Bulunamadi("dosya_bulunamadi", "Dosya bulunamadı.");
    private static readonly Hata KayitYok = Hata.Bulunamadi("kayit_bulunamadi", "Dosyanın ekleneceği kayıt bulunamadı.");

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var grup = endpoints.MapGroup("/api/dosya").WithTags("Dosya").RequireYetki(DosyaYetkileri.Ek);
        grup.MapGet("/kullanim", KullanimAsync);
        grup.MapGet("/kayit/{tur}/{kayitId:guid}", ListeleAsync);
        grup.MapPost("/kayit/{tur}/{kayitId:guid}", YukleAsync).DisableAntiforgery()
            .WithMetadata(new YuklemeBoyutu(DosyaSinirlari.AzamiBoyut + 1024 * 1024)).WithFormOptions(multipartBodyLengthLimit: DosyaSinirlari.AzamiBoyut + 1024 * 1024);
        grup.MapGet("/{id:guid}/indir", IndirAsync);
        grup.MapDelete("/{id:guid}", SilAsync);
        grup.MapPost("/{id:guid}/geri-al", GeriAlAsync).Islem(YetkiIslemleri.Sil);
    }

    private static async Task<IResult> KullanimAsync(DosyaDbContext db, ILisansSorgusu lisans, IMevcutKullanici kullanici, CancellationToken ct)
    {
        var durum = await lisans.GetirAsync(kullanici.FirmaId!.Value, ct);
        return TypedResults.Ok(new DepolamaKullanimi(await KullanilanAsync(db, ct), durum?.DepolamaKotasiMb * 1024L * 1024L));
    }

    private static async Task<IResult> ListeleAsync(
        string tur, Guid kayitId, bool? silinenler, DosyaDbContext db, IEnumerable<IDosyaKaynagi> kaynaklar, IYetkiDenetimi yetki, ILisansSorgusu lisans,
        IMevcutKullanici kullanici, CancellationToken ct)
    {
        if (await KayitDenetleAsync(tur, kayitId, false, kaynaklar, yetki, lisans, kullanici, null, ct) is { } hata)
        {
            return hata.Problem();
        }

        var sorgu = db.DosyaEkleri.AsNoTracking().Where(d => d.KayitTuru == tur && d.KayitId == kayitId);
        sorgu = silinenler == true ? sorgu.Where(d => d.SilinmeTarihi != null) : sorgu.Where(d => d.SilinmeTarihi == null);
        return TypedResults.Ok(await sorgu.OrderByDescending(d => d.OlusturmaTarihi)
            .Select(d => new DosyaEkiOzeti(d.Id, d.Ad, d.Boyut, d.OlusturmaTarihi, d.Yukleyen, d.SilinmeTarihi))
            .ToListAsync(ct));
    }

    private static async Task<IResult> YukleAsync(
        string tur, Guid kayitId, IFormFile? dosya, DosyaDbContext db, IDosyaDeposu depo, IEnumerable<IDosyaKaynagi> kaynaklar, IYetkiDenetimi yetki,
        ILisansSorgusu lisans, IMevcutKullanici kullanici, TimeProvider zaman, CancellationToken ct)
    {
        if (await KayitDenetleAsync(tur, kayitId, true, kaynaklar, yetki, lisans, kullanici, zaman, ct) is { } hata)
        {
            return hata.Problem();
        }

        if (dosya is null || dosya.Length == 0)
        {
            return Hata.Dogrulama("dosya_bos", "Dosya seçin.").Problem();
        }

        if (dosya.Length > DosyaSinirlari.AzamiBoyut)
        {
            return Hata.Dogrulama("dosya_cok_buyuk", "Dosya en fazla 20 MB olabilir.").Problem();
        }

        var ad = DosyaTurleri.AdTemizle(dosya.FileName);
        await using var icerik = dosya.OpenReadStream();
        if (ad.Length == 0 || !DosyaTurleri.GecerliMi(ad, icerik))
        {
            return Hata.Dogrulama("dosya_turu", "Bu dosya türü kabul edilmiyor ya da içeriği uzantısıyla uyuşmuyor. PDF, JPG, PNG, Word, Excel ya da XML yükleyin.").Problem();
        }

        var kota = (await lisans.GetirAsync(kullanici.FirmaId!.Value, ct))?.DepolamaKotasiMb;
        if (kota is { } mb && await KullanilanAsync(db, ct) + dosya.Length > mb * 1024L * 1024L)
        {
            return Hata.IsKurali("depolama_kotasi", $"Depolama alanınız doldu (paket kotası {mb} MB). Yer açmak için dosya silin ya da paketinizi yükseltin.").Problem();
        }

        var ozet = Convert.ToHexStringLower(await SHA256.HashDataAsync(icerik, ct));
        icerik.Position = 0;
        if (await db.DosyaEkleri.AnyAsync(d => d.KayitTuru == tur && d.KayitId == kayitId && d.Ozet == ozet && d.SilinmeTarihi == null, ct))
        {
            return Hata.Cakisma("dosya_zaten_var", "Bu dosya bu kayda zaten eklenmiş.").Problem();
        }

        var yol = $"{kullanici.FirmaId}/{Guid.CreateVersion7()}";
        await depo.YazAsync(yol, icerik, ct);
        var kayit = DosyaEki.Olustur(tur, kayitId, ad, dosya.Length, ozet, yol, kullanici.KullaniciAdi);
        db.DosyaEkleri.Add(kayit);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            depo.Sil(yol);
            throw;
        }

        return TypedResults.Created($"/api/dosya/{kayit.Id}/indir", new DosyaEkiOzeti(kayit.Id, kayit.Ad, kayit.Boyut, kayit.OlusturmaTarihi, kayit.Yukleyen, null));
    }

    // Tarayıcıda açılmaz: her zaman indirme. Türü tarayıcı tahmin etmesin diye nosniff.
    private static async Task<IResult> IndirAsync(
        Guid id, HttpContext http, DosyaDbContext db, IDosyaDeposu depo, IEnumerable<IDosyaKaynagi> kaynaklar, IYetkiDenetimi yetki, ILisansSorgusu lisans,
        IMevcutKullanici kullanici, CancellationToken ct)
    {
        var dosya = await db.DosyaEkleri.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id && d.SilinmeTarihi == null, ct);
        if (dosya is null || await KayitDenetleAsync(dosya.KayitTuru, dosya.KayitId, false, kaynaklar, yetki, lisans, kullanici, null, ct) is not null)
        {
            return Bulunamadi.Problem();
        }

        if (depo.Ac(dosya.DepoYolu) is not { } akis)
        {
            return Bulunamadi.Problem();
        }

        http.Response.Headers.XContentTypeOptions = "nosniff";
        return TypedResults.File(akis, DosyaTurleri.IcerikTuru(dosya.Ad), dosya.Ad);
    }

    private static async Task<IResult> SilAsync(
        Guid id, DosyaDbContext db, IEnumerable<IDosyaKaynagi> kaynaklar, IYetkiDenetimi yetki, ILisansSorgusu lisans, IMevcutKullanici kullanici,
        TimeProvider zaman, CancellationToken ct)
    {
        var dosya = await db.DosyaEkleri.SingleOrDefaultAsync(d => d.Id == id && d.SilinmeTarihi == null, ct);
        if (dosya is null)
        {
            return Bulunamadi.Problem();
        }

        if (await KayitDenetleAsync(dosya.KayitTuru, dosya.KayitId, true, kaynaklar, yetki, lisans, kullanici, zaman, ct) is { } hata)
        {
            return (hata == KayitYok ? Bulunamadi : hata).Problem();
        }

        dosya.Sil(zaman.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> GeriAlAsync(
        Guid id, DosyaDbContext db, IEnumerable<IDosyaKaynagi> kaynaklar, IYetkiDenetimi yetki, ILisansSorgusu lisans, IMevcutKullanici kullanici,
        TimeProvider zaman, CancellationToken ct)
    {
        var dosya = await db.DosyaEkleri.SingleOrDefaultAsync(d => d.Id == id && d.SilinmeTarihi != null, ct);
        if (dosya is null)
        {
            return Bulunamadi.Problem();
        }

        if (await KayitDenetleAsync(dosya.KayitTuru, dosya.KayitId, true, kaynaklar, yetki, lisans, kullanici, zaman, ct) is { } hata)
        {
            return (hata == KayitYok ? Bulunamadi : hata).Problem();
        }

        var kota = (await lisans.GetirAsync(kullanici.FirmaId!.Value, ct))?.DepolamaKotasiMb;
        if (kota is { } mb && await KullanilanAsync(db, ct) + dosya.Boyut > mb * 1024L * 1024L)
        {
            return Hata.IsKurali("depolama_kotasi", "Depolama alanınız dolu olduğu için dosya geri alınamadı.").Problem();
        }

        dosya.GeriAl();
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new DosyaEkiOzeti(dosya.Id, dosya.Ad, dosya.Boyut, dosya.OlusturmaTarihi, dosya.Yukleyen, null));
    }

    // Kotaya çöp kutusundakiler sayılmaz: silen kullanıcı yerinin açıldığını görür.
    private static async Task<long> KullanilanAsync(DosyaDbContext db, CancellationToken ct) =>
        await db.DosyaEkleri.Where(d => d.SilinmeTarihi == null).SumAsync(d => d.Boyut, ct);

    // Kayıt türü tanımlı mı, modül lisanslı mı, kaydı görme yetkisi var mı, kayıt bu kullanıcıya görünür mü. Yazmada süresi dolan lisans salt okunurdur.
    private static async Task<Hata?> KayitDenetleAsync(
        string tur, Guid kayitId, bool yazmaMi, IEnumerable<IDosyaKaynagi> kaynaklar, IYetkiDenetimi yetki, ILisansSorgusu lisans, IMevcutKullanici kullanici,
        TimeProvider? zaman, CancellationToken ct)
    {
        var kaynak = kaynaklar.FirstOrDefault(k => k.Tur == tur);
        if (kaynak is null)
        {
            return KayitYok;
        }

        var durum = await lisans.GetirAsync(kullanici.FirmaId!.Value, ct);
        if (durum is null || !durum.Moduller.Contains(kaynak.ModulKodu))
        {
            return KayitYok;
        }

        if (yazmaMi && durum.SuresiDolduMu(zaman!.GetUtcNow().UtcDateTime))
        {
            return LisansHatalari.SuresiDoldu;
        }

        if (!await kaynak.ErisebilirMiAsync(yetki, kayitId, ct))
        {
            return Hata.Yetki("yetki_yok", "Bu kaydı görme yetkiniz olmadığı için dosyalarına erişemezsiniz.");
        }

        return await kaynak.KayitVarMiAsync(kayitId, ct) ? null : KayitYok;
    }
}
