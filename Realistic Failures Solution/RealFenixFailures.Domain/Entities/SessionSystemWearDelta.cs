namespace RealFenixFailures.Domain.Entities;

/// <summary>
/// Delta de desgaste que una sesión aplicó a un sistema (diseño objetivo §12/§13): alimenta la columna
/// «Δ sesión» del modal de resultados. Se persiste al cerrar la sesión para que el modal pueda re-abrirse
/// desde el historial con el mismo desglose.
/// </summary>
public class SessionSystemWearDelta {
    public int Id { get; set; }
    public int FlightSessionId { get; set; }
    public int AtaId { get; set; }
    public string SystemName { get; set; } = string.Empty;
    /// <summary>Aumento de desgaste aplicado por esta sesión (%).</summary>
    public double DeltaPercentage { get; set; }
    /// <summary>Porcentaje acumulado del sistema en la aeronave después de aplicar el delta (%).</summary>
    public double TotalPercentage { get; set; }

    public FlightSession? FlightSession { get; set; }
}
