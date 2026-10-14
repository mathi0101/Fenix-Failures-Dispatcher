using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;

namespace RealFenixFailures.Application.Interfaces;

public interface IFlightSessionService {
    Task<FlightSession> StartNewAsync(RiskLevel risk, UserAircraftDto aircraft, CancellationToken cancellationToken);
    Task<FlightSession> StopAsync(int sessionId, DateTime finishedAt, CancellationToken ct);
    Task AddTriggeredFailureAsync(int sessionId, string fenixFailureId, ComplexFlightPhaseEnum flightPhase, DateTime triggeredAtUtc, CancellationToken ct);
}
