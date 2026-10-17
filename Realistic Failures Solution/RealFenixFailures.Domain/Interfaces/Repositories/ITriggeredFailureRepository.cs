using RealFenixFailures.Domain.Entities;

namespace RealFenixFailures.Domain.Interfaces.Repositories;

public interface ITriggeredFailureRepository
{
    Task<TriggeredFailure> AddAsync(TriggeredFailure triggeredFailure, CancellationToken cancellationToken);
    Task<IReadOnlyList<TriggeredFailure>> GetActiveForSessionAsync(int sessionId, CancellationToken ct);
    Task<TriggeredFailure?> GetActiveForUpdateAsync(int sessionId, string fenixFailureId, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
