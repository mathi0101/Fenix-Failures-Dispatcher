using RealFenixFailures.Domain.Enums;

namespace RealFenixFailures.Domain.Entities;

public class FlightSession {
    public int Id { get; set; }
    public int FlightPlanId { get; set; }
    public DateTime StartedAt { get; set; }
    public RiskLevel RiskLevel { get; set; } // 1- Low | 2- Moderate | 3-Hard
    public int UserAircraftId { get; set; }
    public DateTime? FinishedAt { get; set; }

    /// <summary>No nulo cuando la sesión fue descartada desde la recuperación de sesiones sin finalizar (no aplica desgaste).</summary>
    public DateTime? DiscardedAt { get; set; }

    public UserAircraft? UserAircraft { get; set; }
    public FlightPlan? FlightPlan { get; set; }
    public ICollection<TriggeredFailure> TriggeredFailures { get; set; } = new List<TriggeredFailure>();
    public ICollection<SessionEventLog> EventLogs { get; set; } = new List<SessionEventLog>();
}
