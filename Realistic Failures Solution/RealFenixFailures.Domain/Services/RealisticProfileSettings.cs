namespace RealFenixFailures.Domain.Services;

/// <summary>Parámetros de un perfil de riesgo del modo Realistic. Defaults en <see cref="RealisticModeSettings"/> (Low/Moderate/High/Squawk7700); appsettings puede pisar cualquier campo parcialmente vía <c>FailureEngine:RealisticMode:{Low|Moderate|High|Squawk7700}</c>.</summary>
public class RealisticProfileSettings {
    /// <summary>Nivel de riesgo que este perfil atiende: 1 Bajo, 2 Moderado, 3 Alto, 4 Squawk 7700 (clave contra la que resuelve el catálogo).</summary>
    public int RiskLevel { get; set; }
    /// <summary>Multiplicador de frecuencia del perfil: escala las λ de todas las fallas (espec §1.1).</summary>
    public double ProbabilityMultiplier { get; set; } = 1.0;
    /// <summary>Con desgaste 100% la probabilidad de fallar del sistema se multiplica por este valor (1 → sin efecto).</summary>
    public double WearMaxMultiplier { get; set; } = 3.0;
    /// <summary>Puntaje de agravamiento máximo tolerado por la sesión; null = sin tope (espec §2.2).</summary>
    public int? AggravationCap { get; set; }
    /// <summary>Tope de fallas de la semilla de tierra: máximo de fallas leves con que puede arrancar la sesión.</summary>
    public int GroundFailuresCap { get; set; }
    /// <summary>Tregua del perfil tras una falla crítica, s: tiempo sin nuevas fallas (0 = sin tregua). Es el único valor aplicado; no hay techo global.</summary>
    public int CriticalRespiteSeconds { get; set; }
}
