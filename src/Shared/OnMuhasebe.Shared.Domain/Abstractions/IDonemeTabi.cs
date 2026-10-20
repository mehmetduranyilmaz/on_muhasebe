namespace OnMuhasebe.Shared.Domain.Abstractions;

// Muhasebe dönemine bağlı kayıt (hareket, fatura). Tarihi firmanın kilitli dönemindeyse eklenemez, silinemez ve değiştirilemez;
// yalnızca [DonemKilidiDisinda] işaretli mutabakat alanları (kapama, ekstre eşleşmesi) değişebilir.
public interface IDonemeTabi
{
    DateOnly Tarih { get; }
}

// Bakiye ve tutar değiştirmeyen, kilitli dönemdeki kayıtta da güncellenebilen alan (ör. kapama durumu, iptal bağlantısı).
[AttributeUsage(AttributeTargets.Property)]
public sealed class DonemKilidiDisindaAttribute : Attribute;

// Değişiklik geçmişine (denetim kaydı) yazılmayan teknik kayıt (ör. istek kaydı).
[AttributeUsage(AttributeTargets.Class)]
public sealed class DenetimDisiAttribute : Attribute;

public sealed class DonemKilitliException(DateOnly kilitTarihi, DateOnly kayitTarihi)
    : Exception($"{kilitTarihi:dd.MM.yyyy} ve öncesi kilitli dönem; {kayitTarihi:dd.MM.yyyy} tarihli kayıt eklenemez, değiştirilemez ya da silinemez.")
{
    public DateOnly KilitTarihi { get; } = kilitTarihi;
}
