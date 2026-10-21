using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Bildirim.Contracts;
using OnMuhasebe.Modules.Bildirim.Domain;
using OnMuhasebe.Modules.Bildirim.Infrastructure;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Sayfalama;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Bildirim.Features;

// Kullanıcının kendi bildirimleri ve tercihi, firma ayarı (yönetici), SMTP ayarı ve elle çalıştırma (sistem yöneticisi).
internal static class BildirimIslemleri
{
    public const int AzamiVadeOncesiGun = 60;

    private static readonly Hata _bulunamadi = Hata.Bulunamadi("bildirim_bulunamadi", "Bildirim bulunamadı.");
    private static readonly Hata _sifrelemeYok = Hata.IsKurali("sifreleme_anahtari_yok",
        "Sunucuda şifreleme anahtarı (Bildirim:SifrelemeAnahtari) tanımlı değil; SMTP şifresi kaydedilemez.");

    public static void Map(RouteGroupBuilder grup)
    {
        var firma = grup.MapGroup("").RequireAuthorization(p => p.RequireClaim(ClaimTipleri.FirmaId));
        firma.MapGet("", ListeleAsync);
        firma.MapGet("/sayac", SayacAsync);
        firma.MapPost("/{id:guid}/okundu", OkunduAsync);
        firma.MapPost("/tumu-okundu", TumuOkunduAsync);
        firma.MapGet("/tercihler", TercihGetirAsync);
        firma.MapPut("/tercihler", TercihKaydetAsync).Dogrula<BildirimTercihleri>();
        firma.MapGet("/ayarlar", AyarGetirAsync);
        firma.MapPut("/ayarlar", AyarKaydetAsync).Dogrula<BildirimAyarlari>().RequireAuthorization(p => p.RequireRole(Roller.Yonetici));

        var sistem = grup.MapGroup("/sistem").RequireAuthorization(p => p.RequireRole(Roller.SistemYoneticisi));
        sistem.MapGet("/eposta", EpostaGetirAsync);
        sistem.MapPut("/eposta", EpostaKaydetAsync).Dogrula<EpostaAyarlari>();
        sistem.MapPost("/eposta/test", EpostaTestAsync).Dogrula<EpostaTestIstegi>();
        sistem.MapPost("/calistir", async (BildirimCalistirici calistirici, CancellationToken ct) =>
            TypedResults.Ok(await calistirici.HepsiAsync(ct)));
    }

    internal sealed class TercihDogrulayici : AbstractValidator<BildirimTercihleri>
    {
        public TercihDogrulayici()
        {
            RuleFor(x => x.KapaliTurler).NotNull();
            RuleForEach(x => x.KapaliTurler).IsInEnum();
            RuleFor(x => x.Eposta).IsInEnum();
        }
    }

    internal sealed class AyarDogrulayici : AbstractValidator<BildirimAyarlari>
    {
        public AyarDogrulayici()
        {
            RuleFor(x => x.VadeOncesiGun).InclusiveBetween(0, AzamiVadeOncesiGun);
            RuleFor(x => x.KapaliTurler).NotNull();
            RuleForEach(x => x.KapaliTurler).IsInEnum();
        }
    }

    internal sealed class EpostaDogrulayici : AbstractValidator<EpostaAyarlari>
    {
        public EpostaDogrulayici()
        {
            RuleFor(x => x.Sunucu).NotEmpty().MaximumLength(200).When(x => x.AktifMi);
            RuleFor(x => x.Port).InclusiveBetween(1, 65535);
            RuleFor(x => x.Guvenlik).IsInEnum();
            RuleFor(x => x.KullaniciAdi).MaximumLength(200);
            RuleFor(x => x.Sifre).MaximumLength(200);
            RuleFor(x => x.GonderenAdres).NotEmpty().When(x => x.AktifMi);
            RuleFor(x => x.GonderenAdres).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.GonderenAdres));
            RuleFor(x => x.GonderenAd).MaximumLength(100);
        }
    }

    internal sealed class TestDogrulayici : AbstractValidator<EpostaTestIstegi>
    {
        public TestDogrulayici() => RuleFor(x => x.Alici).NotEmpty().EmailAddress().MaximumLength(256);
    }

    private static async Task<IResult> ListeleAsync(
        bool? sadeceOkunmamis, int? sayfa, int? boyut, IMevcutKullanici kullanici, BildirimDbContext db, CancellationToken ct)
    {
        var sorgu = db.Bildirimler.AsNoTracking().Where(b => b.KullaniciId == kullanici.KullaniciId);
        if (sadeceOkunmamis == true)
        {
            sorgu = sorgu.Where(b => b.OkunmaTarihi == null);
        }

        return TypedResults.Ok(await sorgu
            .OrderByDescending(b => b.OlusturmaTarihi).ThenByDescending(b => b.Id)
            .Select(b => new BildirimOzeti(b.Id, b.Tur, b.Baslik, b.Metin, b.Baglanti, b.Onem, b.OkunmaTarihi != null, b.OlusturmaTarihi))
            .SayfalaAsync(new SayfalamaIstegi(sayfa ?? 1, boyut ?? SayfalamaIstegi.VarsayilanBoyut), ct));
    }

    private static async Task<IResult> SayacAsync(IMevcutKullanici kullanici, BildirimDbContext db, CancellationToken ct) =>
        TypedResults.Ok(new BildirimSayaci(
            await db.Bildirimler.CountAsync(b => b.KullaniciId == kullanici.KullaniciId && b.OkunmaTarihi == null, ct)));

    private static async Task<IResult> OkunduAsync(Guid id, IMevcutKullanici kullanici, BildirimDbContext db, TimeProvider zaman, CancellationToken ct)
    {
        var bildirim = await db.Bildirimler.SingleOrDefaultAsync(b => b.Id == id && b.KullaniciId == kullanici.KullaniciId, ct);
        if (bildirim is null)
        {
            return _bulunamadi.Problem();
        }

        bildirim.Okundu(zaman.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> TumuOkunduAsync(IMevcutKullanici kullanici, BildirimDbContext db, TimeProvider zaman, CancellationToken ct)
    {
        var simdi = zaman.GetUtcNow().UtcDateTime;
        var sayi = await db.Bildirimler
            .Where(b => b.KullaniciId == kullanici.KullaniciId && b.OkunmaTarihi == null)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.OkunmaTarihi, simdi), ct);
        return TypedResults.Ok(new BildirimSayaci(sayi));
    }

    private static async Task<IResult> TercihGetirAsync(IMevcutKullanici kullanici, ClaimsPrincipal oturum, BildirimDbContext db, CancellationToken ct) =>
        TypedResults.Ok(await TercihAsync(kullanici, oturum, db, ct));

    private static async Task<IResult> TercihKaydetAsync(
        BildirimTercihleri istek, IMevcutKullanici kullanici, ClaimsPrincipal oturum, BildirimDbContext db, CancellationToken ct)
    {
        var tercih = await db.Tercihler.SingleOrDefaultAsync(t => t.KullaniciId == kullanici.KullaniciId, ct);
        if (tercih is null)
        {
            db.Tercihler.Add(KullaniciTercihi.Olustur(kullanici.KullaniciId!.Value, istek));
        }
        else
        {
            tercih.Guncelle(istek);
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await TercihAsync(kullanici, oturum, db, ct));
    }

    // Kaydı olmayan kullanıcının tercihi rolüne göre varsayılandır.
    private static async Task<BildirimTercihleri> TercihAsync(
        IMevcutKullanici kullanici, ClaimsPrincipal oturum, BildirimDbContext db, CancellationToken ct) =>
        (await db.Tercihler.AsNoTracking().SingleOrDefaultAsync(t => t.KullaniciId == kullanici.KullaniciId, ct))?.Sozlesme()
        ?? AliciKurallari.Varsayilan(oturum.IsInRole(Roller.Yonetici) ? Roller.Yonetici : Roller.Kullanici);

    private static async Task<IResult> AyarGetirAsync(BildirimDbContext db, CancellationToken ct) =>
        TypedResults.Ok((await db.Ayarlar.AsNoTracking().FirstOrDefaultAsync(ct) ?? BildirimAyari.Olustur()).Sozlesme());

    private static async Task<IResult> AyarKaydetAsync(BildirimAyarlari istek, BildirimDbContext db, CancellationToken ct)
    {
        var ayar = await db.Ayarlar.FirstOrDefaultAsync(ct);
        if (ayar is null)
        {
            ayar = BildirimAyari.Olustur();
            db.Ayarlar.Add(ayar);
        }

        ayar.Guncelle(istek.VadeOncesiGun, istek.KapaliTurler);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ayar.Sozlesme());
    }

    private static async Task<IResult> EpostaGetirAsync(BildirimDbContext db, CancellationToken ct) =>
        TypedResults.Ok((await db.EpostaAyarlari.AsNoTracking().FirstOrDefaultAsync(ct) ?? EpostaAyari.Olustur()).Sozlesme());

    private static async Task<IResult> EpostaKaydetAsync(EpostaAyarlari istek, BildirimDbContext db, SifreKoruyucu koruyucu, CancellationToken ct)
    {
        string? sifreli = null;
        if (!string.IsNullOrEmpty(istek.Sifre))
        {
            if (!koruyucu.HazirMi)
            {
                return _sifrelemeYok.Problem();
            }

            sifreli = koruyucu.Sifrele(istek.Sifre);
        }

        var ayar = await db.EpostaAyarlari.FirstOrDefaultAsync(ct);
        if (ayar is null)
        {
            ayar = EpostaAyari.Olustur();
            db.EpostaAyarlari.Add(ayar);
        }

        ayar.Guncelle(istek, sifreli);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ayar.Sozlesme());
    }

    private static async Task<IResult> EpostaTestAsync(EpostaTestIstegi istek, BildirimDbContext db, IEpostaGonderici gonderici, CancellationToken ct)
    {
        var ayar = await db.EpostaAyarlari.AsNoTracking().FirstOrDefaultAsync(ct);
        if (ayar is not { Sunucu: not null, GonderenAdres: not null })
        {
            return Hata.IsKurali("eposta_ayari_yok", "Önce SMTP sunucusunu ve gönderen adresini kaydedin.").Problem();
        }

        try
        {
            await gonderici.GonderAsync(ayar, [new Eposta(istek.Alici, istek.Alici, "Ön Muhasebe test e-postası",
                "<p>Bu bir test e-postasıdır. SMTP ayarlarınız çalışıyor.</p>")], ct);
            return TypedResults.NoContent();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Hata.IsKurali("eposta_gonderilemedi", $"E-posta gönderilemedi: {e.Message}").Problem();
        }
    }
}
