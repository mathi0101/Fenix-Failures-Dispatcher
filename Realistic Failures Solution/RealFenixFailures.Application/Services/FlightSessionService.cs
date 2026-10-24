using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealFenixFailures.Application.Services;

public class FlightSessionService : IFlightSessionService {
    private readonly IFlightSessionRepository _sessionRepository;
    private readonly ITriggeredFailureRepository _triggeredFailureRepository;
    private readonly IAircraftSystemWearRepository? _aircraftSystemWearRepository;
    private readonly ILogger<FlightSessionService> _logger;

    public FlightSessionService(IFlightSessionRepository sessionRepository, ITriggeredFailureRepository triggeredFailureRepository,
        IAircraftSystemWearRepository? aircraftSystemWearRepository = null, ILogger<FlightSessionService>? logger = null) {
        _sessionRepository = sessionRepository;
        _triggeredFailureRepository = triggeredFailureRepository;
        _aircraftSystemWearRepository = aircraftSystemWearRepository;
        _logger = logger ?? NullLogger<FlightSessionService>.Instance;
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
        var deltas = MapWearDeltas(await _sessionRepository.GetWearDeltasAsync(sessionId, ct));
        return ToResultDto(session, deltas);
    }

    public async Task<IReadOnlyList<FlightSessionDto>> GetUnfinishedSessionsAsync(CancellationToken ct) {
        var sessions = await _sessionRepository.GetUnfinishedAsync(ct);
        return sessions.Select(ToDto).ToArray();
    }

    public async Task<SessionResultDto> FinalizeSessionAsync(int sessionId, DateTime finishedAt, IReadOnlyList<SystemWearDeltaDto>? wearDeltas, CancellationToken ct) {
        var session = await _sessionRepository.GetByIdAsync(sessionId, ct) ?? throw new KeyNotFoundException();
        IReadOnlyList<SystemWearDeltaDto> deltas = wearDeltas ?? [];
        if (session.FinishedAt is null) {
            var finishedUtc = finishedAt.Kind == DateTimeKind.Utc ? finishedAt : finishedAt.ToUniversalTime();
            session.FinishedAt = finishedUtc;
            ConsolidateUnresolved(session, finishedUtc);
            PersistWearDeltas(session.Id, deltas);
            await _sessionRepository.SaveChangesAsync(ct);
            _logger.LogDebug("Realistic sesión {SessionId}: consolidación de cierre guardada, fin UTC {FinishedAtUtc}, deltas de desgaste {WearCount}", sessionId, finishedUtc, deltas.Count);
        } else {
            deltas = MapWearDeltas(await _sessionRepository.GetWearDeltasAsync(sessionId, ct));
        }
        return ToResultDto(session, deltas);
    }

    public async Task<SessionResultDto> FinalizeUnfinishedSessionAsync(int sessionId, CancellationToken ct) {
        var session = await _sessionRepository.GetByIdAsync(sessionId, ct) ?? throw new KeyNotFoundException();

        if (session.FinishedAt is null) {
            // Desgaste diferido (sesión huérfana, diseño objetivo §6): sin telemetría del vuelo real, el
            // usuario decidió usar la ETA del OFP como fin del vuelo (sin clampear a «ahora»); sin OFP no hay desgaste.
            var etaDeltas = await AccumulateWearByEtaAsync(session, ct);
            try {
                await AddEventLogAsync(sessionId, SessionEventCategory.SessionStopped, "SESSION_FINALIZED_RECOVERY",
                    "Sesión sin finalizar finalizada desde la recuperación (desgaste según ETA del OFP)",
                    ComplexFlightPhaseEnum.Unknown, DateTime.UtcNow, ct);
            } catch (Exception ex) {
                _logger.LogError(ex, "No se pudo registrar la recuperación de la sesión {SessionId}", sessionId);
                // El registro del evento no puede impedir la finalización.
            }
            return await FinalizeSessionAsync(sessionId, DateTime.UtcNow, etaDeltas, ct);
        }

        return await FinalizeSessionAsync(sessionId, DateTime.UtcNow, null, ct);
    }

    public async Task DiscardSessionAsync(int sessionId, DateTime discardedAtUtc, CancellationToken ct) {
        var session = await _sessionRepository.GetByIdAsync(sessionId, ct) ?? throw new KeyNotFoundException();
        if (session.FinishedAt is not null) return;

        var discarded = discardedAtUtc.Kind == DateTimeKind.Utc ? discardedAtUtc : discardedAtUtc.ToUniversalTime();
        session.FinishedAt = discarded;
        session.DiscardedAt = discarded;
        ConsolidateUnresolved(session, discarded);
        await AddEventLogAsync(sessionId, SessionEventCategory.SessionStopped, "SESSION_DISCARDED",
            "Sesión sin finalizar descartada desde la recuperación (sin desgaste)",
            ComplexFlightPhaseEnum.Unknown, discarded, ct);
        await _sessionRepository.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Consolidación al cierre (diseño objetivo §9): toda falla no resuelta (Preparing/Queued/Sent/Active)
    /// pasa a NotResolved con fuente SessionEnded y hora de resolución = momento de cierre. Las Rejected quedan como están.
    /// </summary>
    private static void ConsolidateUnresolved(FlightSession session, DateTime finishedUtc) {
        var lastPhase = session.EventLogs.Where(e => e.FlightPhase != ComplexFlightPhaseEnum.Unknown)
            .OrderBy(e => e.OccurredAt).ThenBy(e => e.Id).LastOrDefault()?.FlightPhase ?? ComplexFlightPhaseEnum.Unknown;
        foreach (var failure in session.TriggeredFailures ?? []) {
            if (failure.State is not (FailureLifecycleState.Preparing or FailureLifecycleState.Queued
                or FailureLifecycleState.Sent or FailureLifecycleState.Active)) continue;
            failure.State = FailureLifecycleState.NotResolved;
            failure.ResolvedAtUtc = finishedUtc;
            failure.ResolvedPhase = lastPhase;
            failure.ResolutionSource = ResolutionSource.SessionEnded;
            session.EventLogs.Add(new SessionEventLog {
                FlightSessionId = session.Id,
                TriggeredFailureId = failure.Id,
                OccurredAt = finishedUtc,
                Category = SessionEventCategory.FailureStateChanged,
                EventCode = "FAILURE_NOT_RESOLVED",
                Description = $"{failure.FailureName ?? failure.FenixFailureId} quedó sin resolver al terminar la sesión",
                FlightPhase = lastPhase
            });
        }
    }

    /// <summary>Desgaste de una sesión huérfana: tasa por sistema × horas hasta la ETA del OFP (fallas de la sesión no lo modifican). Devuelve los deltas aplicados.</summary>
    private async Task<IReadOnlyList<SystemWearDeltaDto>> AccumulateWearByEtaAsync(FlightSession session, CancellationToken ct) {
        if (_aircraftSystemWearRepository is null) return [];
        var eta = session.FlightPlan?.EstimatedArrivalUtc;
        if (eta is null) return [];
        try {
            var hours = (eta.Value - session.StartedAt).TotalHours;
            if (hours <= 0) return [];
            var wears = await _aircraftSystemWearRepository.GetForAircraftAsync(session.UserAircraftId, ct);
            var deltas = new List<SystemWearDeltaDto>();
            foreach (var wear in wears) {
                var rate = wear.WearableSystem?.WearPerFlightHour ?? 0;
                if (rate <= 0) continue;
                var delta = rate * hours;
                var previousWear = wear.WearPercentage;
                wear.WearPercentage = Math.Clamp(wear.WearPercentage + delta, 0, 100);
                if (wear.WearPercentage > previousWear)
                    _logger.LogDebug("Realistic sesión {SessionId}: desgaste diferido calculado ATA {Ata}, tasa {Rate}/h, horas {Hours}, previo {Before}%, delta calculado {Delta}%, efectivo {AppliedDelta}%, total {After}%; pendiente de guardar",
                        session.Id, wear.WearableSystem!.AtaId, rate, hours, previousWear, delta, wear.WearPercentage - previousWear, wear.WearPercentage);
                wear.LastUpdatedAt = eta.Value;
                deltas.Add(new SystemWearDeltaDto {
                    AtaId = wear.WearableSystem!.AtaId,
                    SystemName = wear.WearableSystem.Name,
                    DeltaPercentage = delta,
                    TotalPercentage = wear.WearPercentage
                });
            }
            if (deltas.Count > 0) await _aircraftSystemWearRepository.SaveAsync(ct);
            if (deltas.Count > 0)
                _logger.LogDebug("Realistic sesión {SessionId}: desgaste de recuperación guardado, sistemas {Count}", session.Id, deltas.Count);
            return deltas;
        } catch (Exception ex) {
            _logger.LogError(ex, "No se pudo guardar el desgaste diferido de la sesión {SessionId}", session.Id);
            // El desgaste diferido no puede impedir la finalización de la sesión huérfana.
            return [];
        }
    }

    private static IReadOnlyList<SystemWearDeltaDto> MapWearDeltas(IReadOnlyList<SessionSystemWearDelta> deltas) =>
        deltas.Select(d => new SystemWearDeltaDto {
            AtaId = d.AtaId,
            SystemName = d.SystemName,
            DeltaPercentage = d.DeltaPercentage,
            TotalPercentage = d.TotalPercentage
        }).ToArray();

    /// <summary>Persiste los deltas de desgaste de la sesión para el modal de resultados (re-abirible desde historial).</summary>
    private void PersistWearDeltas(int sessionId, IReadOnlyList<SystemWearDeltaDto> deltas) {
        foreach (var delta in deltas) {
            _sessionRepository.AddWearDelta(new SessionSystemWearDelta {
                FlightSessionId = sessionId,
                AtaId = delta.AtaId,
                SystemName = delta.SystemName,
                DeltaPercentage = delta.DeltaPercentage,
                TotalPercentage = delta.TotalPercentage
            });
        }
    }

    private static SessionResultDto ToResultDto(FlightSession session, IReadOnlyList<SystemWearDeltaDto>? wearDeltas = null) {
        var failures = session.TriggeredFailures ?? [];
        var launched = failures.Where(f => f.State != FailureLifecycleState.Rejected).ToArray();
        var summary = new ResultSummaryDto {
            TotalFailures = launched.Length,
            ActiveAtEnd = failures.Count(f => f.State is FailureLifecycleState.Sent or FailureLifecycleState.Queued or FailureLifecycleState.Active or FailureLifecycleState.Preparing),
            ResolvedByPilot = failures.Count(f => f.State == FailureLifecycleState.ResolvedByPilot),
            AutoResolved = failures.Count(f => f.State == FailureLifecycleState.AutoResolved),
            Rejected = failures.Count(f => f.State == FailureLifecycleState.Rejected),
            Minor = launched.Count(f => f.Severity == FailureSeverity.Minor),
            Major = launched.Count(f => f.Severity == FailureSeverity.Major),
            Critical = launched.Count(f => f.Severity == FailureSeverity.Critical)
        };
        return new SessionResultDto {
            SessionId = session.Id,
            UserAircraftId = session.UserAircraftId,
            Registration = session.UserAircraft?.Registration ?? string.Empty,
            AircraftIcaoType = session.UserAircraft?.IcaoTypeCode ?? session.FlightPlan?.AircraftIcaoType ?? string.Empty,
            FlightNumber = session.FlightPlan?.FlightNumber,
            Callsign = session.FlightPlan?.Callsign,
            OfpSource = session.FlightPlan?.Source,
            DiscardedAt = session.DiscardedAt,
            EventLogs = session.EventLogs.OrderBy(e => e.OccurredAt).ThenBy(e => e.Id).Select(e => new SessionEventLogDto {
                Id = e.Id, OccurredAt = e.OccurredAt, EventCode = e.EventCode, Category = e.Category,
                Description = e.Description, FlightPhase = e.FlightPhase, TriggeredFailureId = e.TriggeredFailureId
            }).ToArray(),
            StartedAt = session.StartedAt,
            FinishedAt = session.FinishedAt,
            RiskLevel = session.RiskLevel,
            OriginIcao = session.FlightPlan?.OriginIcao,
            DestinationIcao = session.FlightPlan?.DestinationIcao,
            Summary = summary,
            Failures = failures.Select(f => new FailureResultDto {
                Id = f.Id,
                FenixFailureId = f.FenixFailureId,
                FailureName = f.FailureName ?? f.FenixFailureId,
                FailureGroup = f.FenixFailure?.Group?.Name ?? "",
                FailureSystem = f.FenixFailure?.Group?.System?.Name ?? "",
                FailureAtaId = f.FenixFailure?.Group?.System?.AtaId,
                Severity = f.Severity,
                State = f.State,
                ResolutionSource = f.ResolutionSource,
                TriggeredAt = f.TriggeredAt,
                FlightPhase = f.FlightPhase,
                ResolvedAtUtc = f.ResolvedAtUtc
            }).ToArray(),
            WearDeltas = (wearDeltas ?? []).ToArray()
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
        EstimatedArrivalUtc = f.FlightPlan?.EstimatedArrivalUtc,
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
        Result = f.FinishedAt is null ? null : ToResultDto(f).Summary,
        DiscardedAt = f.DiscardedAt
    };
}
