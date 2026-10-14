using RealFenixFailures.Domain.Entities;

namespace RealFenixFailures.Domain.Interfaces.Repositories;

public interface IFlightSessionRepository {
    Task<FlightSession?> GetByIdAsync(int id, CancellationToken ct);
    Task<FlightSession> AddAsync(FlightSession session, CancellationToken cancellationToken);
    Task AddTriggeredFailureAsync(TriggeredFailure triggeredFailure, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
