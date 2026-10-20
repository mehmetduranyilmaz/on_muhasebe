using FluentValidation;
using OnMuhasebe.Shared.Contracts.Ayarlar;

namespace OnMuhasebe.Shared.Infrastructure.Validation;

// Oturumdaki firmanın zorunlu yaptığı alan kodları (Yönetim modülü sağlar, önbellekli).
public interface IZorunluAlanSorgusu
{
    Task<IReadOnlySet<string>> SeciliAsync(CancellationToken cancellationToken = default);
}

// İstek üzerinde bir alanın dolu olup olmadığı. Ozellik hata sözlüğündeki anahtardır (ekran alanın altında gösterir).
public sealed record AlanKontrolu<T>(string Kod, string Ozellik, Func<T, bool> DoluMu);

public static class ZorunluAlanKurali
{
    // Firmanın zorunlu yaptığı alanlar boşsa doğrulama hatası; alan adı modülün tanımından gelir. Yeni kayıtta ve düzenlemede
    // aynı doğrulayıcı çalıştığından eski bir kayıt düzenlenirken de eksik alan istenir.
    public static void ZorunluAlanlar<T>(
        this AbstractValidator<T> dogrulayici, IZorunluAlanSorgusu sorgu, IReadOnlyList<ZorunluAlanTanimi> tanimlar, IReadOnlyList<AlanKontrolu<T>> kontroller) =>
        dogrulayici.RuleFor(x => x).CustomAsync(async (istek, baglam, ct) =>
        {
            var secili = await sorgu.SeciliAsync(ct);
            if (secili.Count == 0)
            {
                return;
            }

            foreach (var k in kontroller.Where(k => secili.Contains(k.Kod) && !k.DoluMu(istek)))
            {
                var ad = tanimlar.FirstOrDefault(t => t.Kod == k.Kod)?.Ad ?? k.Ozellik;
                baglam.AddFailure(k.Ozellik, $"{ad} zorunludur (firma ayarı).");
            }
        });

    public static bool Dolu(string? deger) => !string.IsNullOrWhiteSpace(deger);
}
