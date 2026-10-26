using RealFenixFailures.Domain.Enums;

namespace RealFenixFailures.Domain.Entities;

public class TriggeredFailure {
    public int Id { get; set; }
    public required int FlightSessionId { get; set; }
    public required string FenixFailureId { get; set; }
    public DateTime TriggeredAt { get; set; }
    public ComplexFlightPhaseEnum FlightPhase { get; set; }
    public string? FailureName { get; set; }
    public FailureSeverity? Severity { get; set; }
    public int? PresetId { get; set; }
    public string? PresetName { get; set; }
    public double? Probability { get; set; }
    public int? ProbabilityGroup { get; set; }
    public string? Ias { get; set; }
    public string? AboveAltitude { get; set; }
    public string? BelowAltitude { get; set; }
    public string? Time { get; set; }
    public string? AfterEvent { get; set; }
    public string? AfterEventSeconds { get; set; }
    public FailureLifecycleState State { get; set; } = FailureLifecycleState.Sent;
    public DateTime? FenixConfirmedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public ComplexFlightPhaseEnum? ResolvedPhase { get; set; }
    public ResolutionSource? ResolutionSource { get; set; }
    public bool CanBeTemporary { get; set; }
    /// <summary>Momento (tiempo de escenario) en que la falla temporal se auto-resuelve; null = permanente. Solo lo setea el motor al armar fallas aleatorias no críticas que salieron temporales (tipos-de-falla §4.3).</summary>
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? PreparedAtUtc { get; set; }

    public FlightSession? FlightSession { get; set; }
    public FenixFailureDefinition? FenixFailure { get; set; }
}
