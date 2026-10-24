using RealFenixFailures.Domain.Entities;

namespace RealFenixFailures.Domain.Interfaces.Repositories;

public interface IFenixFailureDefinitionRepository {
    Task<bool> HasAnyData(CancellationToken ct);
    Task<IReadOnlyList<FenixFailureDefinition>> GetAllWithSystemsAsync(CancellationToken ct);
    Task LoadNewFailuresAsync(IReadOnlyCollection<FenixFailureSystem> systems, CancellationToken ct);
}
