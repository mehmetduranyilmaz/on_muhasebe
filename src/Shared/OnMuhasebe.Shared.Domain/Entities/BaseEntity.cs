using OnMuhasebe.Shared.Domain.Abstractions;

namespace OnMuhasebe.Shared.Domain.Entities;

public abstract class BaseEntity : IDenetlenebilir
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public DateTime OlusturmaTarihi { get; private set; }
    public Guid? OlusturanId { get; private set; }
    public DateTime? GuncellemeTarihi { get; private set; }
    public Guid? GuncelleyenId { get; private set; }

    // PostgreSQL xmin sistem kolonuna eşlenir; iyimser kilitleme için kullanılır.
    public uint Surum { get; private set; }
}
