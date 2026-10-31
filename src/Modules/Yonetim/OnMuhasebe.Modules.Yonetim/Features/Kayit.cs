using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using OnMuhasebe.Modules.Yonetim.Contracts;
using OnMuhasebe.Modules.Yonetim.Domain;
using OnMuhasebe.Modules.Yonetim.Infrastructure;
using OnMuhasebe.Modules.Yonetim.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Yonetim.Features;

// Kendi kaydı: firma, yönetici hesabı ve deneme lisansı açılır; hesap e-postadaki bağlantıyla doğrulanınca giriş yapılabilir.
// Yanıt e-postanın kayıtlı olup olmadığını ele vermez: kayıtlı adrese "zaten hesabınız var" e-postası gider.
internal static class Kayit
{
    public const int DenemeSuresiGun = 14;

    public static void Map(RouteGroupBuilder grup)
    {
        grup.MapPost("/kayit", KayitAsync)
            .AllowAnonymous()
            .RequireRateLimiting(YonetimModulu.KimlikLimitPolitikasi)
            .Dogrula<KayitIstegi>();
        grup.MapPost("/eposta-dogrula", DogrulaAsync)
            .AllowAnonymous()
            .RequireRateLimiting(YonetimModulu.KimlikLimitPolitikasi)
            .Dogrula<EpostaDogrulaIstegi>();
        grup.MapPost("/dogrulama-tekrar", TekrarAsync)
            .AllowAnonymous()
            .RequireRateLimiting(YonetimModulu.KimlikLimitPolitikasi)
            .Dogrula<DogrulamaTekrarIstegi>();
    }

    internal sealed class Dogrulayici : AbstractValidator<KayitIstegi>
    {
        public Dogrulayici()
        {
            RuleFor(x => x.FirmaUnvani).NotEmpty().MaximumLength(250);
            RuleFor(x => x.VergiNo).VergiNo();
            RuleFor(x => x.Ad).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Soyad).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Eposta).Eposta();
            RuleFor(x => x.Sifre).Sifre();
        }
    }

    internal sealed class DogrulaDogrulayici : AbstractValidator<EpostaDogrulaIstegi>
    {
        public DogrulaDogrulayici() => RuleFor(x => x.Kod).NotEmpty().MaximumLength(100);
    }

    internal sealed class TekrarDogrulayici : AbstractValidator<DogrulamaTekrarIstegi>
    {
        public TekrarDogrulayici() => RuleFor(x => x.Eposta).Eposta();
    }

    private static async Task<IResult> KayitAsync(
        KayitIstegi istek,
        YonetimDbContext db,
        IPasswordHasher<Kullanici> hasher,
        KimlikPostasi posta,
        HybridCache onbellek,
        YetkiKatalogu katalog,
        TimeProvider zaman,
        CancellationToken cancellationToken)
    {
        var simdi = zaman.GetUtcNow().UtcDateTime;
        var eposta = Kullanici.EpostaNormalize(istek.Eposta);
        var kullanici = await db.Kullanicilar.SingleOrDefaultAsync(k => k.Eposta == eposta, cancellationToken);

        if (kullanici is { EpostaDogrulandiMi: true })
        {
            // Aynı adrese bu uyarı en fazla iki dakikada bir gider.
            var ilkUyari = false;
            await onbellek.GetOrCreateAsync($"kayit-uyarisi:{kullanici.Id}", _ =>
            {
                ilkUyari = true;
                return ValueTask.FromResult(true);
            }, new HybridCacheEntryOptions { Expiration = KullaniciKodu.YenidenGondermeAraligi, LocalCacheExpiration = KullaniciKodu.YenidenGondermeAraligi },
                cancellationToken: cancellationToken);
            if (ilkUyari)
            {
                await posta.ZatenKayitliAsync(kullanici.Eposta, kullanici.AdSoyad, cancellationToken);
            }

            return Kabul(eposta);
        }

        if (kullanici is null)
        {
            var denemePaketi = await db.Paketler.SingleAsync(p => p.Kod == PaketKodlari.Deneme, cancellationToken);
            var firma = Firma.Olustur(istek.FirmaUnvani, istek.VergiNo, vergiDairesi: null);
            var roller = RolSablonlari.Olustur(katalog.Tumu, firma.Id);
            kullanici = Kullanici.Olustur(istek.Ad, istek.Soyad, istek.Eposta);
            db.AddRange(firma, kullanici);
            db.Roller.AddRange(roller);
            db.Isyerleri.Add(Isyeri.MerkezOlustur(firma.Id));
            db.AddRange(
                FirmaKullanicisi.Olustur(kullanici.Id, roller[0].Id, firma.Id),
                FirmaLisansi.Olustur(firma.Id, denemePaketi, simdi, simdi.AddDays(DenemeSuresiGun)));
        }
        else
        {
            // Doğrulanmamış hesap henüz kimsenin değil: başkası bu adresle önceden kayıt açmış olabilir. Bilgiler son
            // kayda göre güncellenir; hesabı yalnızca e-postanın sahibi (bağlantıyla) etkinleştirebilir.
            kullanici.BekleyenHesabiGuncelle(istek.Ad, istek.Soyad);
            var firmaId = await db.FirmaKullanicilari.IgnoreQueryFilters([ModulDbContext.FirmaFiltresi])
                .Where(u => u.KullaniciId == kullanici.Id).Select(u => (Guid?)u.FirmaId).FirstOrDefaultAsync(cancellationToken);
            if (firmaId is not null && await db.Firmalar.SingleOrDefaultAsync(f => f.Id == firmaId, cancellationToken) is { } firma)
            {
                firma.BilgileriGuncelle(istek.FirmaUnvani, istek.VergiNo, null, null, null, null, null, null);
            }
        }

        kullanici.SifreBelirle(hasher.HashPassword(kullanici, istek.Sifre));
        await db.SaveChangesAsync(cancellationToken);
        await DogrulamaGonderAsync(db, posta, kullanici, simdi, cancellationToken);
        return Kabul(eposta);
    }

    private static async Task<IResult> DogrulaAsync(
        EpostaDogrulaIstegi istek,
        YonetimDbContext db,
        OturumServisi oturumServisi,
        TimeProvider zaman,
        CancellationToken cancellationToken)
    {
        var simdi = zaman.GetUtcNow().UtcDateTime;
        var ozet = TokenServisi.Ozetle(istek.Kod.Trim());
        var kod = await db.KullaniciKodlari.SingleOrDefaultAsync(k => k.KodOzeti == ozet, cancellationToken);
        var kullanici = kod is null
            ? null
            : await db.Kullanicilar.SingleOrDefaultAsync(k => k.Id == kod.KullaniciId && k.AktifMi, cancellationToken);
        if (kod is null || kullanici is null || !kod.GecerliMi(KodAmaci.EpostaDogrulama, simdi))
        {
            return YonetimHatalari.DogrulamaKoduGecersiz.Problem();
        }

        kod.Kullan(simdi);
        kullanici.EpostaDogrula();
        var sonuc = await oturumServisi.AcAsync(kullanici, kullanici.SonFirmaId, eskiToken: null, cancellationToken);
        return sonuc.HttpSonucu();
    }

    private static async Task<IResult> TekrarAsync(
        DogrulamaTekrarIstegi istek,
        YonetimDbContext db,
        KimlikPostasi posta,
        TimeProvider zaman,
        CancellationToken cancellationToken)
    {
        var eposta = Kullanici.EpostaNormalize(istek.Eposta);
        var kullanici = await db.Kullanicilar.SingleOrDefaultAsync(k => k.Eposta == eposta && k.AktifMi && !k.EpostaDogrulandiMi, cancellationToken);
        if (kullanici is not null)
        {
            await DogrulamaGonderAsync(db, posta, kullanici, zaman.GetUtcNow().UtcDateTime, cancellationToken);
        }

        return Kabul(eposta);
    }

    private static async Task DogrulamaGonderAsync(
        YonetimDbContext db, KimlikPostasi posta, Kullanici kullanici, DateTime simdi, CancellationToken cancellationToken)
    {
        if (!await KullaniciKodlari.GonderilebilirMiAsync(db, kullanici.Id, KodAmaci.EpostaDogrulama, simdi, cancellationToken))
        {
            return;
        }

        var (kod, ozet) = TekKullanimlikKod.Uret();
        db.KullaniciKodlari.Add(KullaniciKodu.Olustur(kullanici.Id, KodAmaci.EpostaDogrulama, ozet, simdi));
        await db.SaveChangesAsync(cancellationToken);
        await posta.DogrulamaAsync(kullanici.Eposta, kullanici.AdSoyad, kod, cancellationToken);
    }

    private static IResult Kabul(string eposta) => TypedResults.Accepted((string?)null, new { eposta });
}
