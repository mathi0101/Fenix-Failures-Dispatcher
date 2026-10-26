using Microsoft.Extensions.Options;
using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Application.Session;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Services;

namespace RealFenixFailures.Application.Services;

/// <inheritdoc />
public sealed class RealisticExpectationService : IRealisticExpectationService {
    /// <summary>
    /// Σλ de referencia por hora para la estimación del header (perfil de referencia Moderado, desgaste 0):
    /// constante de presentación desacoplada de la tabla de severidades — el cálculo real por perfil y
    /// severidad es ComputePerProfileAsync (fórmula global §1.1). La cadencia real la decide el motor.
    /// </summary>
    public const double ReferenceRatePerHour = 0.22;

    /// <summary>Duración de vuelo de referencia para estimar cuando no hay OFP cargado.</summary>
    public const double DefaultFlightHours = 1.5;

    /// <summary>Desgaste medio de referencia cuando la aeronave todavía no registra desgaste.</summary>
    public const double DefaultAverageWearPct = 25;

    private readonly FailureEngineSettings _settings;

    public RealisticExpectationService(IOptions<FailureEngineSettings> settings) {
        _settings = settings.Value;
    }

    public RealisticExpectationDto Compute(double? averageWearPct, double? flightHours, RiskLevel? profileRisk) {
        var mode = _settings.RealisticMode;
        var profile = mode.ProfileFor(profileRisk ?? RiskLevel.Moderate);
        var wearFactor = RealisticFailurePolicy.WearFactor(profile, averageWearPct);
        var rate = ReferenceRatePerHour
            * Math.Max(0, profile.ProbabilityMultiplier)
            * wearFactor;

        var hours = flightHours is > 0 ? flightHours : null;
        return new RealisticExpectationDto {
            RatePerHour = rate,
            FlightHours = hours,
            ExpectedFailures = hours is null ? 0 : rate * hours.Value,
            ProbabilityAtLeastOnePct = hours is null ? 0 : (1 - Math.Exp(-rate * hours.Value)) * 100
        };
    }

    public Task<IReadOnlyList<ProfileExpectationDto>> ComputePerProfileAsync(double? averageWearPct, CancellationToken ct = default) {
        var mode = _settings.RealisticMode;
        var physics = mode.Physics;
        var (minorCount, majorCount, _) = FailureCatalogStats.SeverityCounts();
        var wearPct = averageWearPct ?? DefaultAverageWearPct;

        var results = new List<ProfileExpectationDto>();
        foreach (var risk in new[] { RiskLevel.Low, RiskLevel.Moderate, RiskLevel.High, RiskLevel.Squawk7700 }) {
            var profile = mode.ProfileFor(risk);
            var wearFactor = RealisticFailurePolicy.WearFactor(profile, wearPct);

            var groundSeeds = RealisticProfileExpectation.ExpectedGroundSeedsPerFlight(physics, profile, minorCount, majorCount, wearPct);

            // Estimación global por severidad (sin presets ni fases): λ = base × mult severidad × mult perfil × factor desgaste.
            var rate = Math.Max(0, physics.FailureRatePerHourBase)
                * Math.Max(0, profile.ProbabilityMultiplier)
                * wearFactor;
            var minorPerHour = rate * Math.Max(0, physics.SeverityRateMultiplier.For(FailureSeverity.Minor));
            var majorPerHour = rate * Math.Max(0, physics.SeverityRateMultiplier.For(FailureSeverity.Major));
            // El perfil Bajo descarta críticas por regla del motor (FailureRuleEvaluator): nunca las dispara.
            var criticalPerHour = risk == RiskLevel.Low ? 0 : rate * Math.Max(0, physics.SeverityRateMultiplier.For(FailureSeverity.Critical));

            results.Add(new ProfileExpectationDto {
                Risk = risk,
                Profile = profile,
                GroundSeedsPerFlight = groundSeeds,
                MinorPerHour = minorPerHour,
                MajorPerHour = majorPerHour,
                CriticalPerHour = criticalPerHour,
            });
        }
        return Task.FromResult((IReadOnlyList<ProfileExpectationDto>)results);
    }
}
