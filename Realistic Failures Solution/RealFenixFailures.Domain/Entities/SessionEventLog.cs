using RealFenixFailures.Domain.Enums;

namespace RealFenixFailures.Domain.Entities;

public class SessionEventLog {
    public int Id { get; set; }
    public required int FlightSessionId { get; set; }
    public DateTime OccurredAt { get; set; }
    public required string EventCode { get; set; }
    public SessionEventCategory Category { get; set; }
    public required string Description { get; set; }
    public ComplexFlightPhaseEnum FlightPhase { get; set; }
    public int? TriggeredFailureId { get; set; }

    public FlightSession? FlightSession { get; set; }
    public TriggeredFailure? TriggeredFailure { get; set; }
}
