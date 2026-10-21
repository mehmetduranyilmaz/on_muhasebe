using OnMuhasebe.Shared.Contracts.Bildirimler;

namespace OnMuhasebe.Shared.Infrastructure.Bildirimler;

// Firmanın bugünkü durumu için: VadeOncesiGun kadar gün kala vade hatırlatılır.
public sealed record BildirimBaglami(Guid FirmaId, DateOnly Bugun, int VadeOncesiGun);

// Modüller kendi verisinden bildirim adayı üretir (çek vadesi, risk limiti...). Firma kapsamında çağrılır: sorgu filtreleri ve satır
// güvenliği o firmaya göre çalışır. ModulKodu lisans kodudur; firmanın lisansı kapsamıyorsa kaynak çağrılmaz (boş: lisanssız).
public interface IBildirimKaynagi
{
    string? ModulKodu { get; }

    Task<IReadOnlyList<BildirimAdayi>> AdaylarAsync(BildirimBaglami baglam, CancellationToken cancellationToken);
}

// Bir olaydan hemen sonra (ör. ödeme bildirimi onayı) firmanın bildirimlerini beklemeden üretir.
public interface IBildirimTetikleyici
{
    Task FirmaIcinAsync(Guid firmaId, CancellationToken cancellationToken);
}
