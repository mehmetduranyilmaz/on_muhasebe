using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Yonetim.Contracts;
using OnMuhasebe.Modules.Yonetim.Domain;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Yonetim.Infrastructure.Kimlik;

internal sealed class OturumServisi(
    YonetimDbContext db,
    TokenServisi tokenServisi,
    ILisansSorgusu lisansSorgusu,
    IHttpContextAccessor httpContextAccessor,
    TimeProvider zaman)
{
    // Şifre değişince ya da sıfırlanınca kullanıcının bütün açık oturumları kapanır (kaydetmeyi çağıran yapar).
    public static async Task TumunuKapatAsync(YonetimDbContext db, Guid kullaniciId, DateTime simdi, CancellationToken cancellationToken)
    {
        var tokenlar = await db.RefreshTokenlar
            .Where(t => t.KullaniciId == kullaniciId && t.IptalTarihi == null && t.BitisTarihi > simdi)
            .ToListAsync(cancellationToken);
        tokenlar.ForEach(t => t.IptalEt(simdi));
    }

    // Tercih edilen firmaya üyelik yoksa ilk aktif üyeliğe düşer. eskiToken verilirse rotasyonla iptal edilir.
    public async Task<Sonuc<TokenCevabi>> AcAsync(
        Kullanici kullanici, Guid? tercihFirmaId, RefreshToken? eskiToken, CancellationToken cancellationToken)
    {
        var uyelikler = await db.FirmaKullanicilari
            .IgnoreQueryFilters([ModulDbContext.FirmaFiltresi])
            .Where(u => u.KullaniciId == kullanici.Id && u.AktifMi && u.Firma.AktifMi)
            .OrderBy(u => u.OlusturmaTarihi)
            .ToListAsync(cancellationToken);

        var uyelik = uyelikler.Find(u => u.FirmaId == tercihFirmaId) ?? uyelikler.FirstOrDefault();
        if (uyelik is null && !kullanici.SistemYoneticisiMi)
        {
            return YonetimHatalari.FirmaYok;
        }

        IReadOnlyList<string> moduller = uyelik is null
            ? []
            : (await lisansSorgusu.GetirAsync(uyelik.FirmaId, cancellationToken))?.Moduller ?? [];

        kullanici.OturumAcildi(uyelik?.FirmaId);
        var (accessToken, bitis) = tokenServisi.AccessTokenOlustur(kullanici, uyelik, moduller);
        var (refreshToken, kayit) = tokenServisi.RefreshTokenOlustur(kullanici.Id, uyelik?.FirmaId);

        db.RefreshTokenlar.Add(kayit);
        eskiToken?.IptalEt(zaman.GetUtcNow().UtcDateTime, kayit.Id);
        await db.SaveChangesAsync(cancellationToken);

        YenilemeCerezi.Yaz(httpContextAccessor.HttpContext!.Response, refreshToken, kayit.BitisTarihi);
        return new TokenCevabi(accessToken, bitis);
    }
}
