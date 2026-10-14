using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Interfaces.Repositories;

namespace RealFenixFailures.Application.Services;

public class FlightSessionService : IFlightSessionService {
    private readonly IFlightSessionRepository _sessionRepository;

    public FlightSessionService(IFlightSessionRepository sessionRepository) {
        _sessionRepository = sessionRepository;
    }

    public async Task<FlightSession> StartNewAsync(RiskLevel risk, UserAircraftDto aircraft, CancellationToken cancellationToken) {
        var session = new FlightSession {
            StartedAt = DateTime.UtcNow,
            RiskLevel = risk,
            UserAircraftId = aircraft.Id,
        };

        await _sessionRepository.AddAsync(session, cancellationToken);
        await _sessionRepository.SaveChangesAsync(cancellationToken);

        return session;
    }

    public async Task<FlightSession> StopAsync(int sessionId, DateTime finishedAt, CancellationToken ct) {
        var session = await _sessionRepository.GetByIdAsync(sessionId, ct) ?? throw new KeyNotFoundException();
        session.FinishedAt = finishedAt;
        await _sessionRepository.SaveChangesAsync(ct);

        return session;
    }

    public async Task AddTriggeredFailureAsync(int sessionId, string fenixFailureId, ComplexFlightPhaseEnum flightPhase, DateTime triggeredAtUtc, CancellationToken ct) {
        var triggeredFailure = new TriggeredFailure {
            FlightSessionId = sessionId,
            FenixFailureId = fenixFailureId,
            TriggeredAt = triggeredAtUtc,
            FlightPhase = flightPhase
        };
        await _sessionRepository.AddTriggeredFailureAsync(triggeredFailure, ct);
        await _sessionRepository.SaveChangesAsync(ct);
    }
}
