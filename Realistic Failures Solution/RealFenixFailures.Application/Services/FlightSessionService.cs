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

    public async Task AddTriggeredFailureAsync(int sessionId, PresetFailureDefinition chosen, ComplexFlightPhaseEnum flightPhase, DateTime triggeredAtUtc, CancellationToken ct) {
        var triggeredFailure = new TriggeredFailure {
            FlightSessionId = sessionId,
            FenixFailureId = chosen.FenixFailureId,
            TriggeredAt = triggeredAtUtc,
            FlightPhase = flightPhase,
            FailureName = chosen.FenixFailure?.Name ?? chosen.FenixFailureId,
            Severity = chosen.FenixFailure?.Severity,
            PresetId = chosen.PresetId,
            PresetName = chosen.Preset?.Name,
            Probability = chosen.Probability,
            ProbabilityGroup = chosen.ProbabilityGroup,
            Ias = chosen.Ias,
            AboveAltitude = chosen.Above_Altitude,
            BelowAltitude = chosen.Below_Altitude,
            Time = chosen.Time,
            AfterEvent = chosen.AfterEvent,
            AfterEventSeconds = chosen.AfterEventSeconds
        };
        var eventLog = new SessionEventLog {
            FlightSessionId = sessionId,
            OccurredAt = triggeredAtUtc,
            EventCode = "FAILURE_TRIGGERED",
            Category = SessionEventCategory.FailureTriggered,
            Description = $"Falla disparada: {triggeredFailure.FailureName} ({triggeredFailure.FenixFailureId})",
            FlightPhase = flightPhase,
            TriggeredFailure = triggeredFailure
        };
        await _sessionRepository.AddTriggeredFailureAsync(triggeredFailure, ct);
        await _sessionRepository.AddEventLogAsync(eventLog, ct);
        await _sessionRepository.SaveChangesAsync(ct);
    }

    public async Task AddEventLogAsync(int sessionId, SessionEventCategory category, string eventCode, string description, ComplexFlightPhaseEnum flightPhase, DateTime occurredAtUtc, CancellationToken ct) {
        var eventLog = new SessionEventLog {
            FlightSessionId = sessionId,
            OccurredAt = occurredAtUtc,
            EventCode = eventCode,
            Category = category,
            Description = description,
            FlightPhase = flightPhase
        };
        await _sessionRepository.AddEventLogAsync(eventLog, ct);
        await _sessionRepository.SaveChangesAsync(ct);
    }
}
