using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;

namespace RealFenixFailures.Application.Interfaces;

public interface IFlightSessionService {
    Task<FlightSession> StartNewAsync(RiskLevel risk, UserAircraftDto aircraft, CancellationToken cancellationToken);
    Task<FlightSession> StopAsync(int sessionId, DateTime finishedAt, CancellationToken ct);
    Task AddTriggeredFailureAsync(int sessionId, PresetFailureDefinition chosen, ComplexFlightPhaseEnum flightPhase, DateTime triggeredAtUtc, CancellationToken ct);
    Task AddEventLogAsync(int sessionId, SessionEventCategory category, string eventCode, string description, ComplexFlightPhaseEnum flightPhase, DateTime occurredAtUtc, CancellationToken ctOptions);

    /// <summary>
    /// Transiciona el ciclo de vida de una falla de la sesión (p. ej. Sent → Active → ResolvedByPilot).
    /// Persiste los campos de confirmación/resolución y el <paramref name="source"/> de resolución.
    /// </summary>
    Task UpdateTriggeredFailureStateAsync(int sessionId, string fenixFailureId, FailureLifecycleState newState,
        ComplexFlightPhaseEnum phase, ResolutionSource? source, DateTime utcNow, CancellationToken ct);
}
