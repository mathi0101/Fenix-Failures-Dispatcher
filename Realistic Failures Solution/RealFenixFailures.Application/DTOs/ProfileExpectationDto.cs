using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Services;

namespace RealFenixFailures.Application.DTOs;

/// <summary>
/// Estimación de presentación por perfil para la pantalla de perfiles: combina la semilla de tierra
/// (E[min(Poisson, cap)]) con la cadencia en vuelo ponderada por duración de fase, siempre con la
/// configuración vigente del motor. No cambia el motor.
/// </summary>
public sealed record ProfileExpectationDto {
    public RiskLevel Risk { get; init; }
    /// <summary>Perfil resuelto (configurado o default) que usará el motor para este nivel de riesgo.</summary>
    public RealisticProfileSettings Profile { get; init; } = null!;
    /// <summary>Fallas esperadas al iniciar la sesión (semilla de tierra, truncada por el cap del perfil).</summary>
    public double GroundSeedsPerFlight { get; init; }
    /// <summary>Fallas esperadas en vuelo (Σλ por fase × duración de fase sobre la duración usada).</summary>
    public double InFlightPerFlight { get; init; }
    /// <summary>Solo fallas graves (Critical): la semilla de tierra del motor es Minor y no suma; cuentan las Critical en vuelo.</summary>
    public double CriticalPerFlight { get; init; }
    public double TotalPerFlight => GroundSeedsPerFlight + InFlightPerFlight;
}
