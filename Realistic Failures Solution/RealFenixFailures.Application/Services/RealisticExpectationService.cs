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
    /// Σλ de referencia por hora a multiplier 1.0 y desgaste 0, derivada de la calibración borrador de la
    /// spec (≈20 Minors + ≈5 Majors elegibles por fase a p_base 0.01 → 0.20 + 0.02). Es una estimación
    /// de presentación: la cadencia real la decide el motor tick a tick.
    /// </summary>
    public const double ReferenceRatePerHour = 0.22;

    private readonly FailureEngineSettings _settings;

    public RealisticExpectationService(IOptions<FailureEngineSettings> settings) {
        _settings = settings.Value;
    }

    public RealisticExpectationDto Compute(double? averageWearPct, double? flightHours, RiskLevel? profileRisk) {
        var mode = _settings.RealisticMode;
        var profile = RealisticProfileCatalog.Resolve(mode, profileRisk ?? RiskLevel.Moderate);
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
}
