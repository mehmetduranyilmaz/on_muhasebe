namespace OnMuhasebe.Shared.Domain.Abstractions;

public interface IDenetlenebilir
{
    DateTime OlusturmaTarihi { get; }
    Guid? OlusturanId { get; }
    DateTime? GuncellemeTarihi { get; }
    Guid? GuncelleyenId { get; }
}
