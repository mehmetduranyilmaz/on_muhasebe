namespace OnMuhasebe.Modules.Doviz.Contracts;

// KurTarihi: kurun alındığı TCMB bülteninin tarihi (işlem tarihinden önceki son iş günü).
public sealed record KurBilgisi(string ParaBirimi, DateOnly KurTarihi, decimal DovizAlis, decimal DovizSatis);

public interface IKurSorgusu
{
    // İşlem tarihi için önerilen kur. TRY için her zaman 1; kur bulunamazsa (TCMB'ye ulaşılamadı vb.) null.
    Task<KurBilgisi?> IslemKuruAsync(string paraBirimi, DateOnly islemTarihi, CancellationToken cancellationToken = default);
}
