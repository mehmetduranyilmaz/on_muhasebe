using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Yonetim.Contracts;
using OnMuhasebe.Modules.Yonetim.Domain;
using OnMuhasebe.Shared.Contracts.Bildirimler;
using OnMuhasebe.Shared.Infrastructure.Bildirimler;

namespace OnMuhasebe.Modules.Yonetim.Infrastructure;

// Abonelik bitişi (7 gün ve 1 gün kala, dolunca) ve sonuçlanan havale bildirimleri. Yalnız firma yöneticisine gider.
internal sealed class AbonelikBildirimKaynagi(YonetimDbContext db, TimeProvider zaman) : IBildirimKaynagi
{
    public const int UyariGunu = 7;
    public const int SonucGunu = 7;

    public string? ModulKodu => null;

    public async Task<IReadOnlyList<BildirimAdayi>> AdaylarAsync(BildirimBaglami baglam, CancellationToken cancellationToken)
    {
        var simdi = zaman.GetUtcNow().UtcDateTime;
        var adaylar = new List<BildirimAdayi>();
        var lisans = await db.FirmaLisanslari.AsNoTracking().Include(l => l.Paket).FirstOrDefaultAsync(cancellationToken);
        if (lisans is not null)
        {
            var kalan = AbonelikHesabi.KalanGun(lisans.BitisTarihi, simdi);
            var bitis = lisans.BitisTarihi.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            var sonGun = lisans.BitisTarihi.AddDays(-1).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
            if (lisans.BitisTarihi <= simdi)
            {
                adaylar.Add(new BildirimAdayi(BildirimTuru.AbonelikBitiyor, $"abonelik-doldu:{bitis}", "Abonelik süreniz doldu",
                    "Kayıtlarınız salt okunur. Havale/EFT ile yenileyip ödeme bildirimi gönderebilirsiniz.", "abonelik", BildirimOnemi.Kritik));
            }
            else if (kalan <= UyariGunu)
            {
                adaylar.Add(new BildirimAdayi(BildirimTuru.AbonelikBitiyor, $"abonelik-bitiyor-{(kalan <= 1 ? 1 : UyariGunu)}:{bitis}",
                    $"Aboneliğinizin bitmesine {kalan} gün kaldı", $"{lisans.Paket.Ad} paketiniz {sonGun} tarihine kadar geçerli.", "abonelik",
                    kalan <= 1 ? BildirimOnemi.Kritik : BildirimOnemi.Uyari));
            }
        }

        var sinir = simdi.AddDays(-SonucGunu);
        var sonuclar = await db.OdemeBildirimleri.AsNoTracking().Include(b => b.Paket)
            .Where(b => b.SonucTarihi != null && b.SonucTarihi >= sinir)
            .ToListAsync(cancellationToken);
        adaylar.AddRange(sonuclar.Select(b => b.Durum == OdemeBildirimiDurumu.Onaylandi
            ? new BildirimAdayi(BildirimTuru.OdemeBildirimiSonucu, $"odeme-sonuc:{b.Id}", "Ödemeniz onaylandı",
                $"{b.Paket.Ad} aboneliğiniz {b.YeniBitisTarihi?.AddDays(-1).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)} tarihine kadar uzatıldı.",
                "abonelik", BildirimOnemi.Bilgi)
            : new BildirimAdayi(BildirimTuru.OdemeBildirimiSonucu, $"odeme-sonuc:{b.Id}", "Ödeme bildiriminiz reddedildi",
                b.SonucNotu ?? "Ayrıntı için abonelik sayfasına bakın.", "abonelik", BildirimOnemi.Uyari)));
        return adaylar;
    }
}
