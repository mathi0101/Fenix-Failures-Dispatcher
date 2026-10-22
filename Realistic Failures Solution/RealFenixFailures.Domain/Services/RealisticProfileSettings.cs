namespace RealFenixFailures.Domain.Services;

public class RealisticProfileSettings {
    public int RiskLevel { get; set; }
    public int GroundFailuresCap { get; set; }
    /// <summary>Probabilidad base (0–1) de cada falla Minor individual en el seed de tierra, antes del desgaste.</summary>
    public double GroundFailureBaseProbability { get; set; }
    /// <summary>Ganancia por desgaste (k): multiplica la probabilidad base según wear% del sistema padre (0 → sin efecto).</summary>
    public double GroundWearProbabilityGain { get; set; }
    public int? MinorCap { get; set; }
    public int? MajorCap { get; set; }
    public int? CriticalCap { get; set; }
    public double MinorPerHour { get; set; }
    public double MajorPerHour { get; set; }
    public double CriticalPerHour { get; set; }
    public int CriticalRespiteSeconds { get; set; }
}

