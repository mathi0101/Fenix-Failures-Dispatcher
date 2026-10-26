using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Services;

namespace RealFenixFailures.Application.DTOs;

/// <summary>
/// Estimación de presentación por perfil para la pantalla de perfiles: tasas en vuelo <b>por hora de
/// vuelo</b> desglosadas por severidad, siempre con la configuración vigente del motor. La semilla de
/// tierra es una cantidad por vuelo (arranque de sesión) y va aparte. No cambia el motor.
/// </summary>
public sealed record ProfileExpectationDto {
    public RiskLevel Risk { get; init; }
    /// <summary>Perfil resuelto (configurado o default) que usará el motor para este nivel de riesgo.</summary>
    public RealisticProfileSettings Profile { get; init; } = null!;
    /// <summary>Fallas esperadas al iniciar la sesión (semilla de tierra, truncada por el cap del perfil); no escala con las horas.</summary>
    public double GroundSeedsPerFlight { get; init; }
    public double MinorPerHour { get; init; }
    public double MajorPerHour { get; init; }
    /// <summary>Solo fallas graves (Critical): la semilla de tierra del motor es Minor y no suma; cuentan las Critical en vuelo.</summary>
    public double CriticalPerHour { get; init; }
    public double TotalPerHour => MinorPerHour + MajorPerHour + CriticalPerHour;
}
