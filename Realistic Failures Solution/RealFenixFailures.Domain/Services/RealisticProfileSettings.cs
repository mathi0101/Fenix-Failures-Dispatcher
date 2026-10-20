namespace RealFenixFailures.Domain.Services;

public class RealisticProfileSettings {
    public int RiskLevel { get; set; }
    public int GroundFailuresCap { get; set; }
    public int? MinorCap { get; set; }
    public int? MajorCap { get; set; }
    public int? CriticalCap { get; set; }
    public double MinorPerHour { get; set; }
    public double MajorPerHour { get; set; }
    public double CriticalPerHour { get; set; }
    public int CriticalRespiteSeconds { get; set; }
}

