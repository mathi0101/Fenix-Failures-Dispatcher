using RealFenixFailures.Domain.Enums;

namespace RealFenixFailures.Application.DTOs;

/// <summary>Estimación de presentación (no cambia el motor) de cuántas fallas puede traer la próxima sesión.</summary>
public sealed record RealisticExpectationDto {
    /// <summary>Fallas esperadas (Σλ × horas); 0 si no hay duración.</summary>
    public double ExpectedFailures { get; init; }
    /// <summary>Probabilidad de al menos una falla en la sesión, en %.</summary>
    public double ProbabilityAtLeastOnePct { get; init; }
    /// <summary>Probabilidad de al menos una falla grave en la sesión, en %.</summary>
    public double ProbabilityAtLeastOneMajorPct { get; init; }
    /// <summary>Probabilidad de al menos una falla crítica en la sesión, en %.</summary>
    public double ProbabilityAtLeastOneCriticalPct { get; init; }
    /// <summary>Promedio de P(≥1 grave) y P(≥1 crítica) con desgaste 0 — el riesgo base del vuelo, en %.</summary>
    public double BaselineRiskPct { get; init; }
    /// <summary>Igual que BaselineRiskPct pero calculado con el desgaste medio real, en %.</summary>
    public double WearAdjustedRiskPct { get; init; }
    /// <summary>Ritmo esperado por hora de vuelo (Σλ del modelo).</summary>
    public double RatePerHour { get; init; }
    /// <summary>Horas usadas para el cálculo; null = sin OFP (solo ritmo por hora).</summary>
    public double? FlightHours { get; init; }
}
