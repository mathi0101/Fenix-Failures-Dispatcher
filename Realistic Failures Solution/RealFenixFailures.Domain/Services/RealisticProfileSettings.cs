namespace RealFenixFailures.Domain.Services;

public class RealisticProfileSettings {
    public int RiskLevel { get; set; }
    /// <summary>Multiplicador de frecuencia del perfil: escala las λ de todas las fallas (espec §1.1).</summary>
    public double ProbabilityMultiplier { get; set; } = 1.0;
    /// <summary>Con desgaste 100% la probabilidad de fallar del sistema se multiplica por este valor (1 → sin efecto).</summary>
    public double WearMaxMultiplier { get; set; } = 3.0;
    /// <summary>Puntaje de agravamiento máximo tolerado por la sesión; null = sin tope (espec §2.2).</summary>
    public int? AggravationCap { get; set; }
    public int GroundFailuresCap { get; set; }
    public int CriticalRespiteSeconds { get; set; }
}
