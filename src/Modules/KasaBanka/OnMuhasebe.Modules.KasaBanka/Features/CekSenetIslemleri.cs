using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.KasaBanka.Domain;
using OnMuhasebe.Modules.KasaBanka.Domain.CekSenet;
using OnMuhasebe.Modules.KasaBanka.Infrastructure;
using OnMuhasebe.Modules.KasaBanka.Infrastructure.CekSenet;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Domain.Dogrulama;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.KasaBanka.Features;

// Çek/senet: bordrolar, durum işlemleri, geri alma, keşideci/borçlu kartları ve ayarlar. Sorgular CekSenetSorgulari'nda.
internal static class CekSenetIslemleri
{
    public static void Map(RouteGroupBuilder kok)
    {
        var grup = kok.MapGroup("/cek-senet");
        grup.MapPost("/giris", GirisAsync).Dogrula<AlinanBordroIstegi>();
        grup.MapPost("/cikis", CikisAsync).Dogrula<VerilenBordroIstegi>();
        grup.MapPost("/islemler", IslemAsync).Dogrula<EvrakIslemIstegi>();
        grup.MapPost("/evraklar/{id:guid}/geri-al", GeriAlAsync);

        grup.MapPost("/borclular", BorcluEkleAsync).Dogrula<BorcluKaydetIstegi>();
        grup.MapPut("/borclular/{id:guid}", BorcluGuncelleAsync).Dogrula<BorcluKaydetIstegi>();
        grup.MapPut("/borclular/{id:guid}/risk", RiskAsync);
        grup.MapDelete("/borclular/{id:guid}", BorcluSilAsync);

        grup.MapGet("/ayarlar", AyarGetirAsync);
        grup.MapPut("/ayarlar", AyarKaydetAsync).RequireAuthorization(p => p.RequireRole(Roller.Yonetici));

        CekSenetSorgulari.Map(grup);
    }

    internal sealed class AlinanDogrulayici : AbstractValidator<AlinanBordroIstegi>
    {
        public AlinanDogrulayici()
        {
            RuleFor(x => x.Aciklama).MaximumLength(500);
            RuleFor(x => x.Evraklar).NotEmpty().WithMessage("Bordroda en az bir evrak olmalıdır.").Must(e => e.Count <= 100);
            RuleForEach(x => x.Evraklar).ChildRules(e =>
            {
                e.RuleFor(x => x.Tur).IsInEnum();
                e.RuleFor(x => x.SeriNo).NotEmpty().MaximumLength(30);
                e.RuleFor(x => x.Tutar).GreaterThan(0).LessThan(1_000_000_000_000m);
                e.RuleFor(x => x.ParaBirimi).Must(ParaBirimleri.GecerliMi).WithMessage("Geçersiz para birimi.");
                e.RuleFor(x => x.Borclu).NotNull();
                e.RuleFor(x => x.Borclu.Ad).MaximumLength(200);
                e.RuleFor(x => x.Borclu.KimlikNo).Must(VergiKimlik.GecerliMi).When(x => !string.IsNullOrWhiteSpace(x.Borclu.KimlikNo))
                    .WithMessage("Keşideci/borçlu VKN/TCKN geçersiz.");
                e.RuleFor(x => x.KefilKimlikNo).Must(VergiKimlik.GecerliMi).When(x => !string.IsNullOrWhiteSpace(x.KefilKimlikNo))
                    .WithMessage("Kefil VKN/TCKN geçersiz.");
                e.RuleFor(x => x.BankaAdi).MaximumLength(100);
                e.RuleFor(x => x.Sube).MaximumLength(100);
                e.RuleFor(x => x.HesapNo).MaximumLength(50);
                e.RuleFor(x => x.KesideYeri).MaximumLength(100);
                e.RuleFor(x => x.KefilAdi).MaximumLength(200);
                e.RuleFor(x => x.Aciklama).MaximumLength(500);
                e.RuleFor(x => x.BankaAdi).NotEmpty().When(x => x.Tur == EvrakTuru.Cek).WithMessage("Çekin bankası girilmelidir.");
            });
        }
    }

    internal sealed class VerilenDogrulayici : AbstractValidator<VerilenBordroIstegi>
    {
        public VerilenDogrulayici()
        {
            RuleFor(x => x.Aciklama).MaximumLength(500);
            RuleFor(x => x.Evraklar).NotEmpty().WithMessage("Bordroda en az bir evrak olmalıdır.").Must(e => e.Count <= 100);
            RuleForEach(x => x.Evraklar).ChildRules(e =>
            {
                e.RuleFor(x => x.Tur).IsInEnum();
                e.RuleFor(x => x.SeriNo).NotEmpty().MaximumLength(30);
                e.RuleFor(x => x.Tutar).GreaterThan(0).LessThan(1_000_000_000_000m);
                e.RuleFor(x => x.ParaBirimi).Must(ParaBirimleri.GecerliMi).When(x => x.ParaBirimi is not null).WithMessage("Geçersiz para birimi.");
                e.RuleFor(x => x.KendiHesapId).NotNull().When(x => x.Tur == EvrakTuru.Cek).WithMessage("Verilen çekin banka hesabı seçilmelidir.");
                e.RuleFor(x => x.Aciklama).MaximumLength(500);
            });
        }
    }

    internal sealed class IslemDogrulayici : AbstractValidator<EvrakIslemIstegi>
    {
        public IslemDogrulayici()
        {
            RuleFor(x => x.Islem).IsInEnum().Must(i => i is not (EvrakIslemi.Giris or EvrakIslemi.Cikis))
                .WithMessage("Giriş ve çıkış bordrosu kendi uçlarından yapılır.");
            RuleFor(x => x.EvrakIdleri).NotEmpty().WithMessage("En az bir evrak seçilmelidir.").Must(e => e.Count <= 100);
            RuleFor(x => x.Tutar).GreaterThan(0).When(x => x.Tutar is not null);
            RuleFor(x => x.Tutar).Null().When(x => x.Islem != EvrakIslemi.KarsiliksizdanTahsil)
                .WithMessage("Tutar yalnızca karşılıksızdan tahsilde girilir.");
            RuleFor(x => x.Aciklama).MaximumLength(500);
        }
    }

    internal sealed class BorcluDogrulayici : AbstractValidator<BorcluKaydetIstegi>
    {
        public BorcluDogrulayici()
        {
            RuleFor(x => x.Ad).NotEmpty().MaximumLength(200);
            RuleFor(x => x.KimlikNo).Must(VergiKimlik.GecerliMi).When(x => !string.IsNullOrWhiteSpace(x.KimlikNo)).WithMessage("VKN/TCKN geçersiz.");
            RuleFor(x => x.Telefon).MaximumLength(30);
            RuleFor(x => x.Adres).MaximumLength(500);
            RuleFor(x => x.Not).MaximumLength(500);
        }
    }

    private static async Task<IResult> GirisAsync(AlinanBordroIstegi istek, CekSenetServisi servis, CancellationToken ct) =>
        (await servis.GirisAsync(istek, ct)).HttpSonucu(s => TypedResults.Created($"/api/kasabanka/cek-senet/bordrolar/{s.BordroId}", s));

    private static async Task<IResult> CikisAsync(VerilenBordroIstegi istek, CekSenetServisi servis, CancellationToken ct) =>
        (await servis.CikisAsync(istek, ct)).HttpSonucu(s => TypedResults.Created($"/api/kasabanka/cek-senet/bordrolar/{s.BordroId}", s));

    private static async Task<IResult> IslemAsync(EvrakIslemIstegi istek, CekSenetServisi servis, CancellationToken ct) =>
        (await servis.IslemAsync(istek, ct)).HttpSonucu(s => TypedResults.Created($"/api/kasabanka/cek-senet/bordrolar/{s.BordroId}", s));

    private static async Task<IResult> GeriAlAsync(Guid id, CekSenetServisi servis, CancellationToken ct) =>
        (await servis.GeriAlAsync(id, ct)).HttpSonucu(_ => TypedResults.NoContent());

    private static async Task<IResult> BorcluEkleAsync(BorcluKaydetIstegi istek, KasaBankaDbContext db, CancellationToken ct)
    {
        var kimlik = string.IsNullOrWhiteSpace(istek.KimlikNo) ? null : istek.KimlikNo.Trim();
        if (kimlik is not null && await db.EvrakBorclulari.AnyAsync(b => b.KimlikNo == kimlik, ct))
        {
            return KasaBankaHatalari.KimlikNoKullaniliyor.Problem();
        }

        var borclu = EvrakBorclusu.Olustur(istek.Ad, kimlik, istek.Telefon);
        borclu.Guncelle(istek);
        db.EvrakBorclulari.Add(borclu);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/kasabanka/cek-senet/borclular/{borclu.Id}", new { borclu.Id });
    }

    private static async Task<IResult> BorcluGuncelleAsync(Guid id, BorcluKaydetIstegi istek, KasaBankaDbContext db, CancellationToken ct)
    {
        var borclu = await db.EvrakBorclulari.SingleOrDefaultAsync(b => b.Id == id, ct);
        if (borclu is null)
        {
            return KasaBankaHatalari.BorcluBulunamadi.Problem();
        }

        var kimlik = string.IsNullOrWhiteSpace(istek.KimlikNo) ? null : istek.KimlikNo.Trim();
        if (kimlik is not null && await db.EvrakBorclulari.AnyAsync(b => b.KimlikNo == kimlik && b.Id != id, ct))
        {
            return KasaBankaHatalari.KimlikNoKullaniliyor.Problem();
        }

        db.Entry(borclu).Property(b => b.Surum).OriginalValue = istek.Surum;
        borclu.Guncelle(istek);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> RiskAsync(Guid id, RiskDurumuIstegi istek, KasaBankaDbContext db, CancellationToken ct)
    {
        var borclu = await db.EvrakBorclulari.SingleOrDefaultAsync(b => b.Id == id, ct);
        if (borclu is null)
        {
            return KasaBankaHatalari.BorcluBulunamadi.Problem();
        }

        if (istek.RiskNotu?.Length > 500)
        {
            return Shared.Domain.Results.Hata.Dogrulama("risk_notu", "Not en fazla 500 karakter olabilir.").Problem();
        }

        borclu.RiskAyarla(istek);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> BorcluSilAsync(Guid id, KasaBankaDbContext db, CancellationToken ct)
    {
        var borclu = await db.EvrakBorclulari.Include(b => b.Hesaplar).SingleOrDefaultAsync(b => b.Id == id, ct);
        if (borclu is null)
        {
            return KasaBankaHatalari.BorcluBulunamadi.Problem();
        }

        if (await db.Evraklar.AnyAsync(e => e.BorcluId == id, ct))
        {
            return KasaBankaHatalari.BorcluEvrakliSilinemez.Problem();
        }

        db.EvrakBorclulari.Remove(borclu);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> AyarGetirAsync(CekSenetServisi servis, CancellationToken ct) =>
        TypedResults.Ok((await servis.AyarAsync(ct)).Sozlesme());

    private static async Task<IResult> AyarKaydetAsync(CekSenetAyarlari istek, KasaBankaDbContext db, CancellationToken ct)
    {
        if (!Enum.IsDefined(istek.RiskDavranisi))
        {
            return Shared.Domain.Results.Hata.Dogrulama("risk_davranisi", "Geçersiz risk davranışı.").Problem();
        }

        var ayar = await db.CekSenetAyarlari.SingleOrDefaultAsync(ct);
        if (ayar is null)
        {
            ayar = CekSenetAyari.Varsayilan();
            db.CekSenetAyarlari.Add(ayar);
        }

        ayar.Guncelle(istek);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ayar.Sozlesme());
    }
}
