using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Bildirim.Contracts;
using OnMuhasebe.Modules.Bildirim.Domain;
using OnMuhasebe.Modules.Yonetim.Contracts;
using OnMuhasebe.Shared.Contracts.Bildirimler;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Infrastructure.Bildirimler;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Lisans;

namespace OnMuhasebe.Modules.Bildirim.Infrastructure;

// Bir firmanın bildirimlerini üretir ve e-postaya gidecekleri gönderir. Firma kapsamında (FirmaKapsami) çalışır.
internal sealed class BildirimMotoru(
    BildirimDbContext db,
    IEnumerable<IBildirimKaynagi> kaynaklar,
    IFirmaRehberi rehber,
    ILisansSorgusu lisansSorgusu,
    EpostaPostacisi postaci,
    IMevcutKullanici mevcutKullanici,
    TimeProvider zaman)
{
    public async Task<(int Uretilen, int Eposta)> CalistirAsync(CancellationToken cancellationToken)
    {
        var firmaId = mevcutKullanici.FirmaId ?? throw new InvalidOperationException("Bildirim üretimi firma kapsamında çalışır.");
        var ayar = await db.Ayarlar.AsNoTracking().FirstOrDefaultAsync(cancellationToken) ?? BildirimAyari.Olustur();
        var kapali = BildirimAyari.Turler(ayar.KapaliTurler).ToHashSet();
        var moduller = (await lisansSorgusu.GetirAsync(firmaId, cancellationToken))?.Moduller.ToHashSet() ?? [];
        var baglam = new BildirimBaglami(firmaId, TurkiyeSaati.Bugun(zaman), ayar.VadeOncesiGun);

        var adaylar = new List<BildirimAdayi>();
        foreach (var kaynak in kaynaklar.Where(k => k.ModulKodu is null || moduller.Contains(k.ModulKodu)))
        {
            adaylar.AddRange(await kaynak.AdaylarAsync(baglam, cancellationToken));
        }

        adaylar = adaylar.Where(a => !kapali.Contains(a.Tur)).DistinctBy(a => a.Anahtar).ToList();
        var kullanicilar = await rehber.KullanicilarAsync(firmaId, cancellationToken);
        var tercihler = await db.Tercihler.ToDictionaryAsync(t => t.KullaniciId, cancellationToken);

        var uretilen = 0;
        if (adaylar.Count > 0)
        {
            var anahtarlar = adaylar.Select(a => a.Anahtar).ToList();
            var yazilmis = (await db.Bildirimler.Where(b => anahtarlar.Contains(b.Anahtar))
                    .Select(b => new { b.KullaniciId, b.Anahtar }).ToListAsync(cancellationToken))
                .Select(b => (b.KullaniciId, b.Anahtar)).ToHashSet();

            foreach (var k in kullanicilar)
            {
                var tercih = tercihler.GetValueOrDefault(k.KullaniciId)?.Sozlesme() ?? AliciKurallari.Varsayilan(k.Rol);
                foreach (var a in adaylar.Where(a => (a.HedefKullaniciId == k.KullaniciId || AliciKurallari.Alir(k.Rol, tercih, a.Tur)) && !yazilmis.Contains((k.KullaniciId, a.Anahtar))))
                {
                    db.Bildirimler.Add(Domain.Bildirim.Olustur(k.KullaniciId, a, tercih.Eposta != EpostaSikligi.Kapali));
                    uretilen++;
                }
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return (uretilen, await postaci.GonderAsync(kullanicilar, tercihler, cancellationToken));
    }
}
