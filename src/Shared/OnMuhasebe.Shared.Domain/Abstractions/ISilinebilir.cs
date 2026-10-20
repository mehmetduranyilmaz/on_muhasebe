namespace OnMuhasebe.Shared.Domain.Abstractions;

public interface ISilinebilir
{
    bool SilindiMi { get; }
    DateTime? SilinmeTarihi { get; }
}
