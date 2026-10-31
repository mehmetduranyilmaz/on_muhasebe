using OnMuhasebe.Modules.Fatura.Contracts;

namespace OnMuhasebe.Web.Fatura.Bilesenler;

// Aktarım diyaloğunun sonucu: seçilen irsaliye satırı ve faturalanacak miktar (kısmi olabilir).
public sealed record IrsaliyeAktarimi(AktarilacakIrsaliyeSatiri Satir, decimal Miktar);

// Siparişten aktarımın sonucu: seçilen sipariş satırı ve sevk/fatura edilecek miktar.
public sealed record SiparisAktarimi(AktarilacakSiparisSatiri Satir, decimal Miktar);
