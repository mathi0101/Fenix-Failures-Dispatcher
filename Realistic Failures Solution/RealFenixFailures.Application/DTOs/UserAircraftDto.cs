// ============================================================================
//  DTOs para el flujo del Modo Realista (selección de aeronave, dashboard,
//  desgaste, historial de sesiones y fallas disparadas).
// ============================================================================
using RealFenixFailures.Domain.Enums;

namespace RealFenixFailures.Application.DTOs;

/// <summary>Aeronave del usuario para listar/seleccionar en el Paso 1.</summary>
public sealed record UserAircraftDto {
    public int Id { get; init; }
    public string Registration { get; init; } = string.Empty;
    public string IcaoTypeCode { get; init; } = string.Empty;
    public double TotalFlightHours { get; init; }
    public int TotalFlights { get; init; }
    public DateTime CreatedAt { get; init; }
    public IReadOnlyList<AircraftSystemWearDto> SystemsWear { get; init; } = [];
    public IReadOnlyList<FlightSessionDto> FlightSessions { get; init; } = [];
}

/// <summary>Payload para crear una nueva aeronave (Paso 1 - formulario inline).</summary>
public sealed record CreateUserAircraftRequest {
    public string Registration { get; init; } = string.Empty;
    public string IcaoTypeCode { get; init; } = string.Empty;
}

/// <summary>Desgaste de un sistema para el Paso 2 (dashboard).</summary>
public sealed record AircraftSystemWearDto {
    public required SystemWearDto SystemWear { get; init; }
    public int UserAircraftId { get; init; }
    /// <summary>Porcentaje de desgaste 0–100.</summary>
    public double WearPercentage { get; init; }
    public DateTime LastUpdatedAt { get; init; }
}
public sealed record SystemWearDto {
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string ShortName { get; set; }
    public int DisplayOrder { get; set; }
    /// <summary>ATA que vincula el sistema wearable con los sistemas del catálogo Fenix (FenixFailureSystem.AtaId).</summary>
    public int AtaId { get; set; }
}

/// <summary>Falla disparada dentro de una sesión (resumen expandido del Paso 2).</summary>
public sealed record TriggeredFailureDto {
    public required int Id { get; init; }
    public required string FenixFailureId { get; init; }
    public ComplexFlightPhaseEnum FlightPhase { get; init; }
    public string FailureName { get; init; } = string.Empty;
    public DateTime TriggeredAt { get; init; }
    public FailureSeverity? Severity { get; init; }
    public int? PresetId { get; init; }
    public string? PresetName { get; init; }
    public double? Probability { get; init; }
    public int? ProbabilityGroup { get; init; }
    public string? Ias { get; init; }
    public string? AboveAltitude { get; init; }
    public string? BelowAltitude { get; init; }
    public string? Time { get; init; }
    public string? AfterEvent { get; init; }
    public string? AfterEventSeconds { get; init; }
}

/// <summary>Evento de sesión persistido dentro del historial (Paso 2).</summary>
public sealed record SessionEventLogDto {
    public int Id { get; init; }
    public DateTime OccurredAt { get; init; }
    public string EventCode { get; init; } = string.Empty;
    public SessionEventCategory Category { get; init; }
    public string Description { get; init; } = string.Empty;
    public ComplexFlightPhaseEnum FlightPhase { get; init; }
    public int? TriggeredFailureId { get; init; }
}

/// <summary>Resumen de fallas de una sesión finalizada (diseño objetivo §13 - modal de resultados).</summary>
public sealed record ResultSummaryDto {
    public int TotalFailures { get; init; }
    /// <summary>Fallas que seguían activas/armadas al finalizar la sesión.</summary>
    public int ActiveAtEnd { get; init; }
    public int ResolvedByPilot { get; init; }
    public int AutoResolved { get; init; }
    public int Rejected { get; init; }
    public int Minor { get; init; }
    public int Major { get; init; }
    public int Critical { get; init; }

    public int ResolvedCount => ResolvedByPilot + AutoResolved;
}

/// <summary>Resultado de una falla dentro del resumen de sesión.</summary>
public sealed record FailureResultDto {
    public required int Id { get; init; }
    public required string FenixFailureId { get; init; }
    public string FailureName { get; init; } = string.Empty;
    public string FailureGroup { get; init; } = string.Empty;
    public string FailureSystem { get; init; } = string.Empty;
    public int? FailureAtaId { get; init; }
    public FailureSeverity? Severity { get; init; }
    public FailureLifecycleState State { get; init; }
    public ResolutionSource? ResolutionSource { get; init; }
    public DateTime TriggeredAt { get; init; }
    public ComplexFlightPhaseEnum FlightPhase { get; init; }
    public DateTime? ResolvedAtUtc { get; init; }
}

/// <summary>Delta de desgaste aplicado a un sistema por la sesión (diseño §12/§13, columna «Δ sesión» del modal). El aviso de mantenimiento lo calcula la UI contra el umbral configurado (<c>Wear:ThresholdNoticePct</c>).</summary>
public sealed record SystemWearDeltaDto {
    public required int AtaId { get; init; }
    public required string SystemName { get; init; }
    public double DeltaPercentage { get; init; }
    public double TotalPercentage { get; init; }
}

/// <summary>Resultado completo de una sesión para el modal (diseño objetivo §13).</summary>
public sealed record SessionResultDto {
    public required int SessionId { get; init; }
    public int UserAircraftId { get; init; }
    public string Registration { get; init; } = string.Empty;
    public string AircraftIcaoType { get; init; } = string.Empty;
    public string? FlightNumber { get; init; }
    public string? Callsign { get; init; }
    public string? OfpSource { get; init; }
    public DateTime? DiscardedAt { get; init; }
    public IReadOnlyList<SessionEventLogDto> EventLogs { get; init; } = [];
    public DateTime StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
    public RiskLevel RiskLevel { get; init; }
    public string? OriginIcao { get; init; }
    public string? DestinationIcao { get; init; }
    public TimeSpan? Duration => FinishedAt.HasValue ? FinishedAt.Value - StartedAt : null;
    public ResultSummaryDto Summary { get; init; } = new();
    public IReadOnlyList<FailureResultDto> Failures { get; init; } = Array.Empty<FailureResultDto>();
    /// <summary>Desglose del desgaste aplicado por esta sesión, por sistema (vacío si la sesión no aplicó desgaste).</summary>
    public IReadOnlyList<SystemWearDeltaDto> WearDeltas { get; init; } = Array.Empty<SystemWearDeltaDto>();
}

/// <summary>Sesion de vuelo del historial (Paso 2), con sus fallas.</summary>
public sealed record FlightSessionDto {
    public int Id { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
    /// <summary>1 = Bajo, 2 = Moderado, 3 = Alto, 4 = 7700.</summary>
    public RiskLevel RiskLevel { get; init; }
    public string? OriginIcao { get; init; }
    public string? DestinationIcao { get; init; }
    public DateTime? OffBlockUtc { get; init; }
    /// <summary>ETA del OFP: base del desgaste diferido al finalizar una sesión huérfana (diseño §6).</summary>
    public DateTime? EstimatedArrivalUtc { get; init; }
    public IReadOnlyList<TriggeredFailureDto> TriggeredFailures { get; init; } = Array.Empty<TriggeredFailureDto>();
    public IReadOnlyList<SessionEventLogDto> EventLogs { get; init; } = Array.Empty<SessionEventLogDto>();
    public ResultSummaryDto? Result { get; init; }

    /// <summary>No nulo cuando la sesión fue descartada desde la recuperación (queda en historial como descartada).</summary>
    public DateTime? DiscardedAt { get; init; }

    public int FailureCount => TriggeredFailures.Count;
    public TimeSpan? Duration => FinishedAt.HasValue ? FinishedAt.Value - StartedAt : null;
}

/// <summary>Datos agregados de la aeronave para el dashboard (Paso 2).</summary>
public sealed record AircraftDashboardDto {
    public UserAircraftDto Aircraft { get; init; } = new();
    public int TotalFailuresTriggered { get; init; }
    public IReadOnlyList<AircraftSystemWearDto> SystemWears { get; init; } = Array.Empty<AircraftSystemWearDto>();
}
