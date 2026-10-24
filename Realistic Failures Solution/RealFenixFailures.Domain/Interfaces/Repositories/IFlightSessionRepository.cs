using RealFenixFailures.Domain.Entities;

namespace RealFenixFailures.Domain.Interfaces.Repositories;

public interface IFlightSessionRepository {
    Task<FlightSession?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<FlightSession>> GetUnfinishedAsync(CancellationToken ct);
    Task<FlightSession> AddAsync(FlightSession session, CancellationToken cancellationToken);
    Task AddTriggeredFailureAsync(TriggeredFailure triggeredFailure, CancellationToken ct);
    Task AddEventLogAsync(SessionEventLog eventLog, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);

    /// <summary>Deltas de desgaste persistidos de una sesión (modal de resultados, diseño §13).</summary>
    Task<IReadOnlyList<SessionSystemWearDelta>> GetWearDeltasAsync(int sessionId, CancellationToken ct);

    /// <summary>Encola un delta de desgaste para persistir con SaveChangesAsync.</summary>
    void AddWearDelta(SessionSystemWearDelta delta);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
