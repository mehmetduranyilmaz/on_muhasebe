using OnMuhasebe.Shared.Domain.Abstractions;

namespace OnMuhasebe.Shared.Domain.Entities;

public abstract class FirmaEntity : BaseEntity, IFirmaVarligi
{
    protected FirmaEntity() { }

    // Yalnızca oturum dışı işlemler (firma kaydı, sistem yöneticisi) içindir; normalde FirmaId oturumdan atanır.
    protected FirmaEntity(Guid firmaId) => FirmaId = firmaId;

    public Guid FirmaId { get; private set; }
}
