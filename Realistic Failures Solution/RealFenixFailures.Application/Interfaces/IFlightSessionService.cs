using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;

namespace RealFenixFailures.Application.Interfaces;

public interface IFlightSessionService {
    Task<FlightSession> StartNewAsync(RealisticModeContext context, CancellationToken cancellationToken);
    Task<FlightSession> StopAsync(int sessionId, DateTime finishedAt, CancellationToken ct);
    Task AddTriggeredFailureAsync(int sessionId, PresetFailureDefinition chosen, ComplexFlightPhaseEnum flightPhase, DateTime triggeredAtUtc, CancellationToken ct, DateTime? expiresAtUtc = null);
    Task AddEventLogAsync(int sessionId, SessionEventCategory category, string eventCode, string description, ComplexFlightPhaseEnum flightPhase, DateTime occurredAtUtc, CancellationToken ctOptions);

    /// <summary>
    /// Transiciona el ciclo de vida de una falla de la sesión (p. ej. Sent → Active → ResolvedByPilot).
    /// Persiste los campos de confirmación/resolución y el <paramref name="source"/> de resolución.
    /// </summary>
    Task UpdateTriggeredFailureStateAsync(int sessionId, string fenixFailureId, FailureLifecycleState newState,
        ComplexFlightPhaseEnum phase, ResolutionSource? source, DateTime utcNow, CancellationToken ct);

    /// <summary>Calcula el resultado agregado de una sesión (diseño objetivo §13). Nunca lanza por sesión inexistente: devuelve nulo.</summary>
    Task<SessionResultDto?> GetResultAsync(int sessionId, CancellationToken ct);

    /// <summary>Sesiones sin finalizar (recuperación al reabrir la app; diseño objetivo §6).</summary>
    Task<IReadOnlyList<FlightSessionDto>> GetUnfinishedSessionsAsync(CancellationToken ct);

    /// <summary>Marca una sesión como finalizada si no lo está, persiste los deltas de desgaste y recalcula su resumen. Devuelve el resultado.</summary>
    Task<SessionResultDto> FinalizeSessionAsync(int sessionId, DateTime finishedAt, IReadOnlyList<SystemWearDeltaDto>? wearDeltas, CancellationToken ct);

    /// <summary>
    /// Finaliza una sesión sin finalizar desde la recuperación (diseño objetivo §6/§13): acumula desgaste
    /// usando la hora estimada de llegada (ETA) del OFP como fin del vuelo (sin ella no aplica desgaste),
    /// consolida las fallas no resueltas y devuelve el resultado.
    /// </summary>
    Task<SessionResultDto> FinalizeUnfinishedSessionAsync(int sessionId, CancellationToken ct);

    /// <summary>
    /// Descarta una sesión sin finalizar desde la recuperación: queda en el historial como descartada
    /// (DiscardedAt), consolida las fallas no resueltas y NO aplica desgaste de sistemas.
    /// </summary>
    Task DiscardSessionAsync(int sessionId, DateTime discardedAtUtc, CancellationToken ct);
}
