using OnMuhasebe.Shared.Domain.Abstractions;

namespace OnMuhasebe.Shared.Domain.Entities;

public abstract class FirmaEntity : BaseEntity, IFirmaVarligi
{
    public Guid FirmaId { get; private set; }
}
