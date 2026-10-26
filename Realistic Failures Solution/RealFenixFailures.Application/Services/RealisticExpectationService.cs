using Microsoft.Extensions.Options;
using RealFenixFailures.Application.DTOs;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Application.Session;
using RealFenixFailures.Domain.Entities;
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

    /// <summary>Duración de vuelo de referencia para estimar cuando no hay OFP cargado.</summary>
    public const double DefaultFlightHours = 1.5;

    /// <summary>Desgaste medio de referencia cuando la aeronave todavía no registra desgaste.</summary>
    public const double DefaultAverageWearPct = 25;

    /// <summary>Minutos típicos por fase en una vuelta de ~1,5 h; base del peso de cada fase en vuelo.</summary>
    private static readonly IReadOnlyDictionary<ComplexFlightPhaseEnum, double> PhaseMinutes = new Dictionary<ComplexFlightPhaseEnum, double> {
        [ComplexFlightPhaseEnum.OnGate] = 10,
        [ComplexFlightPhaseEnum.Taxi] = 8,
        [ComplexFlightPhaseEnum.Takeoff] = 2,
        [ComplexFlightPhaseEnum.Climb] = 18,
        [ComplexFlightPhaseEnum.Cruise] = 40,
        [ComplexFlightPhaseEnum.Descent] = 8,
        [ComplexFlightPhaseEnum.Approach] = 10,
        [ComplexFlightPhaseEnum.Landing] = 2,
    };
    private static readonly double TotalPhaseMinutes = PhaseMinutes.Values.Sum();

    private readonly FailureEngineSettings _settings;
    private readonly IPresetService? _presetService;

    public RealisticExpectationService(IOptions<FailureEngineSettings> settings, IPresetService? presetService = null) {
        _settings = settings.Value;
        _presetService = presetService;
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

    public async Task<IReadOnlyList<ProfileExpectationDto>> ComputePerProfileAsync(double? averageWearPct, double? flightHours, CancellationToken ct = default) {
        var mode = _settings.RealisticMode;
        var physics = mode.Physics;
        var (minorCount, majorCount, _) = FailureCatalogStats.SeverityCounts();
        var wearPct = averageWearPct ?? DefaultAverageWearPct;
        var hours = flightHours is > 0 ? flightHours.Value : DefaultFlightHours;

        // Universo de candidatas en vuelo: los mismos presets que monta el motor al iniciar la sesión,
        // ponderados por la duración de la fase en que cada preset es elegible.
        var presets = await SafeGetRealisticPresetsAsync(ct);

        var results = new List<ProfileExpectationDto>();
        foreach (var risk in new[] { RiskLevel.Low, RiskLevel.Moderate, RiskLevel.High, RiskLevel.Squawk7700 }) {
            var profile = RealisticProfileCatalog.Resolve(mode, risk);
            var wearFactor = RealisticFailurePolicy.WearFactor(profile, wearPct);

            var groundSeeds = RealisticProfileExpectation.ExpectedGroundSeedsPerFlight(physics, profile, minorCount, majorCount, wearPct);

            var inFlight = 0.0;
            var criticals = 0.0;
            foreach (var preset in presets) {
                if (!PhaseMinutes.TryGetValue(preset.FlightPhase, out var minutes)) continue;
                var phaseHours = minutes / TotalPhaseMinutes * hours;
                inFlight += SumPresetLambda(preset, profile, physics, wearFactor, risk, criticalOnly: false) * phaseHours;
                // Fallas graves (Critical): la semilla de tierra del motor arma solo Minor, así que las
                // Critical esperadas provienen exclusivamente de los sorteos en vuelo (y Low nunca las dispara).
                criticals += SumPresetLambda(preset, profile, physics, wearFactor, risk, criticalOnly: true) * phaseHours;
            }

            results.Add(new ProfileExpectationDto {
                Risk = risk,
                Profile = profile,
                GroundSeedsPerFlight = groundSeeds,
                InFlightPerFlight = inFlight,
                CriticalPerFlight = criticals,
            });
        }
        return results;
    }

    private static double SumPresetLambda(
        FailurePreset preset,
        RealisticProfileSettings profile,
        RealisticPhysicsSettings physics,
        double wearFactor,
        RiskLevel risk,
        bool criticalOnly = false) {
        var sum = 0.0;
        foreach (var definition in preset.PresetFailureDefinitions) {
            var severity = definition.FenixFailure?.Severity ?? FailureSeverity.Minor;
            if (criticalOnly && severity != FailureSeverity.Critical) continue;
            if (risk == RiskLevel.Low && severity == FailureSeverity.Critical) continue;
            sum += RealisticFailurePolicy.FlightFailureLambda(severity, definition.Probability, profile, physics, wearFactor);
        }
        return sum;
    }

    private async Task<IReadOnlyList<FailurePreset>> SafeGetRealisticPresetsAsync(CancellationToken ct) {
        if (_presetService is null) return [];
        try {
            return await _presetService.GetRealisticPresetsAsync(ct);
        } catch (Exception) {
            return [];
        }
    }
}
