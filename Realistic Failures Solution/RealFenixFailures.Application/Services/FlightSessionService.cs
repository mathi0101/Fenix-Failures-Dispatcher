using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Interfaces.Repositories;

namespace RealFenixFailures.Application.Services;

public class FlightSessionService : IFlightSessionService {
    private readonly IFlightSessionRepository _sessionRepository;
    private readonly ITriggeredFailureRepository _triggeredFailureRepository;

    public FlightSessionService(IFlightSessionRepository sessionRepository, ITriggeredFailureRepository triggeredFailureRepository) {
        _sessionRepository = sessionRepository;
        _triggeredFailureRepository = triggeredFailureRepository;
    }

    public async Task<FlightSession> StartNewAsync(RealisticModeContext context, CancellationToken cancellationToken) {
        var session = new FlightSession {
            StartedAt = DateTime.UtcNow,
            RiskLevel = context.RiskLevel,
            UserAircraftId = context.Aircraft.Id,
            FlightPlanId=context.FlightPlanId
        };

        var ses= await _sessionRepository.AddAsync(session, cancellationToken);
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

    public async Task UpdateTriggeredFailureStateAsync(int sessionId, string fenixFailureId, FailureLifecycleState newState,
        ComplexFlightPhaseEnum phase, ResolutionSource? source, DateTime utcNow, CancellationToken ct) {
        var current = await _triggeredFailureRepository.GetActiveForUpdateAsync(sessionId, fenixFailureId, ct);
        if (current is null || current.State == newState) return;

        current.State = newState;
        if (newState == FailureLifecycleState.Active && current.FenixConfirmedAtUtc is null)
            current.FenixConfirmedAtUtc = utcNow.Kind == DateTimeKind.Utc ? utcNow : utcNow.ToUniversalTime();
        if (newState == FailureLifecycleState.ResolvedByPilot || newState == FailureLifecycleState.AutoResolved) {
            if (current.ResolvedAtUtc is null) current.ResolvedAtUtc = utcNow.Kind == DateTimeKind.Utc ? utcNow : utcNow.ToUniversalTime();
            current.ResolvedPhase = phase;
            current.ResolutionSource = source;
        }

        await _triggeredFailureRepository.SaveChangesAsync(ct);
    }

    public async Task<SessionResultDto?> GetResultAsync(int sessionId, CancellationToken ct) {
        var session = await _sessionRepository.GetByIdAsync(sessionId, ct);
        if (session is null) return null;
        return ToResultDto(session);
    }

    public async Task<IReadOnlyList<FlightSessionDto>> GetUnfinishedSessionsAsync(CancellationToken ct) {
        var sessions = await _sessionRepository.GetUnfinishedAsync(ct);
        return sessions.Select(ToDto).ToArray();
    }

    public async Task<SessionResultDto> FinalizeSessionAsync(int sessionId, DateTime finishedAt, CancellationToken ct) {
        var session = await _sessionRepository.GetByIdAsync(sessionId, ct) ?? throw new KeyNotFoundException();
        if (session.FinishedAt is null) {
            session.FinishedAt = finishedAt.Kind == DateTimeKind.Utc ? finishedAt : finishedAt.ToUniversalTime();
            await _sessionRepository.SaveChangesAsync(ct);
        }
        return ToResultDto(session);
    }

    private static SessionResultDto ToResultDto(FlightSession session) {
        var failures = session.TriggeredFailures ?? [];
        var summary = new ResultSummaryDto {
            TotalFailures = failures.Count,
            ActiveAtEnd = failures.Count(f => f.State is FailureLifecycleState.Sent or FailureLifecycleState.Queued or FailureLifecycleState.Active or FailureLifecycleState.Preparing),
            ResolvedByPilot = failures.Count(f => f.State == FailureLifecycleState.ResolvedByPilot),
            AutoResolved = failures.Count(f => f.State == FailureLifecycleState.AutoResolved),
            Rejected = failures.Count(f => f.State == FailureLifecycleState.Rejected),
            Minor = failures.Count(f => f.Severity == FailureSeverity.Minor),
            Major = failures.Count(f => f.Severity == FailureSeverity.Major),
            Critical = failures.Count(f => f.Severity == FailureSeverity.Critical)
        };
        return new SessionResultDto {
            SessionId = session.Id,
            StartedAt = session.StartedAt,
            FinishedAt = session.FinishedAt,
            RiskLevel = session.RiskLevel,
            OriginIcao = session.FlightPlan?.OriginIcao,
            DestinationIcao = session.FlightPlan?.OriginIcao,
            Summary = summary,
            Failures = failures.Select(f => new FailureResultDto {
                Id = f.Id,
                FenixFailureId = f.FenixFailureId,
                FailureName = f.FailureName ?? f.FenixFailureId,
                Severity = f.Severity,
                State = f.State,
                ResolutionSource = f.ResolutionSource,
                TriggeredAt = f.TriggeredAt,
                FlightPhase = f.FlightPhase,
                ResolvedAtUtc = f.ResolvedAtUtc
            }).ToArray()
        };
    }

    private static FlightSessionDto ToDto(FlightSession f) => new() {
        Id = f.Id,
        StartedAt = f.StartedAt,
        RiskLevel = f.RiskLevel,
        FinishedAt = f.FinishedAt,
        OriginIcao = f.FlightPlan?.OriginIcao,
        DestinationIcao = f.FlightPlan?.DestinationIcao,
        OffBlockUtc = f.FlightPlan?.OffBlockUtc,
        TriggeredFailures = (f.TriggeredFailures ?? []).Select(tf => new TriggeredFailureDto {
            Id = tf.Id,
            FenixFailureId = tf.FenixFailureId,
            FailureName = tf.FailureName ?? tf.FenixFailureId,
            TriggeredAt = tf.TriggeredAt,
            FlightPhase = tf.FlightPhase,
            Severity = tf.Severity,
            PresetId = tf.PresetId,
            PresetName = tf.PresetName,
            Probability = tf.Probability,
            ProbabilityGroup = tf.ProbabilityGroup,
            Ias = tf.Ias,
            AboveAltitude = tf.AboveAltitude,
            BelowAltitude = tf.BelowAltitude,
            Time = tf.Time,
            AfterEvent = tf.AfterEvent,
            AfterEventSeconds = tf.AfterEventSeconds
        }).ToArray(),
        EventLogs = (f.EventLogs ?? []).Select(e => new SessionEventLogDto {
            Id = e.Id,
            OccurredAt = e.OccurredAt,
            EventCode = e.EventCode,
            Category = e.Category,
            Description = e.Description,
            FlightPhase = e.FlightPhase,
            TriggeredFailureId = e.TriggeredFailureId
        }).ToArray(),
        Result = f.FinishedAt is null ? null : ToResultDto(f).Summary
    };
}
