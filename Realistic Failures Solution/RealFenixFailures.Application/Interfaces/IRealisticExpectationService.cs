using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Services;

namespace RealFenixFailures.Application.Interfaces;

/// <summary>
/// Estimación de fallas para la próxima sesión (spec modelo-probabilístico §8.7): puramente de presentación,
/// reutiliza la física del motor para que el usuario sepa qué esperar antes de activar.
/// </summary>
public interface IRealisticExpectationService {
    /// <param name="averageWearPct">Desgaste medio de la aeronave (0–100); null = sin datos.</param>
    /// <param name="flightHours">Duración estimada de la sesión en horas (del OFP); null = sin OFP.</param>
    /// <param name="profileRisk">Perfil elegido; null = referencia Moderate (antes de elegir perfil).</param>
    RealisticExpectationDto Compute(double? averageWearPct, double? flightHours, RiskLevel? profileRisk);

    /// <summary>
    /// Estimación por perfil para la pantalla de perfiles: resuelve cada perfil con la configuración
    /// vigente y devuelve sus tasas en vuelo por hora (por severidad) más la semilla de tierra por vuelo.
    /// </summary>
    /// <param name="averageWearPct">Desgaste medio de la aeronave (0–100); null usa una referencia de 25 %.</param>
    Task<IReadOnlyList<ProfileExpectationDto>> ComputePerProfileAsync(double? averageWearPct, CancellationToken ct = default);
}
