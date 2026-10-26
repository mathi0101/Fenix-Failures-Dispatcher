using Microsoft.Extensions.Options;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Application.Services;
using RealFenixFailures.Application.Session;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Services;
using Xunit;

namespace RealFenixFailures.TestUnits.Services;

/// <summary>
/// Tests de la estimación por perfil de la pantalla de perfiles: matemática de la Poisson truncada,
/// ordenamiento por intensidad y resolución desde la configuración vigente (sin perfiles configurados,
/// el catálogo resuelve los defaults, así que los valores esperados son estables).
/// </summary>
public class RealisticProfileExpectationTests {
    [Theory]
    [InlineData(0, 5, 0)]
    [InlineData(5, 0, 0)]
    [InlineData(-1, 3, 0)]
    public void MeanTruncatedPoisson_NonPositiveInputs_ReturnsZero(double lambda, int cap, double expected) {
        Assert.Equal(expected, RealisticProfileExpectation.MeanTruncatedPoisson(lambda, cap), precision: 9);
    }

    [Fact]
    public void MeanTruncatedPoisson_SmallLambdaTwoCap_MatchesClosedForm() {
        // λ=0.3, cap=2: E = P1 + 2·P(X≥2) = 0.3·e^-0.3 + 2·(1 − e^-0.3·1.3)
        var expected = 0.3 * Math.Exp(-0.3) + 2 * (1 - Math.Exp(-0.3) * 1.3);
        Assert.Equal(expected, RealisticProfileExpectation.MeanTruncatedPoisson(0.3, 2), precision: 9);
    }

    [Theory]
    [InlineData(10, 1, 1)]
    [InlineData(10, 3, 3)]
    public void MeanTruncatedPoisson_LargeLambda_SaturatesAtCap(double lambda, int cap, double expected) {
        Assert.Equal(expected, RealisticProfileExpectation.MeanTruncatedPoisson(lambda, cap), precision: 2);
    }

    [Fact]
    public void MeanTruncatedPoisson_HugeCap_ApproachesLambda() {
        Assert.Equal(0.7, RealisticProfileExpectation.MeanTruncatedPoisson(0.7, 50), precision: 4);
    }

    [Fact]
    public void ExpectedGroundSeedsPerFlight_ZeroWear_IsMonotoneAcrossProfiles() {
        var counts = FailureCatalogStats.SeverityCounts();
        var physics = new RealisticPhysicsSettings();
        var previous = 0.0;
        foreach (var risk in new[] { RiskLevel.Low, RiskLevel.Moderate, RiskLevel.High, RiskLevel.Squawk7700 }) {
            var expected = RealisticProfileExpectation.ExpectedGroundSeedsPerFlight(
                physics, RealisticProfileCatalog.DefaultsFor(risk), counts.Minor, counts.Major, wearPct: 0);
            Assert.True(expected > previous, $"Se esperaba crecimiento por perfil, pero {risk} dio {expected}.");
            previous = expected;
        }
    }

    [Fact]
    public void ExpectedGroundSeedsPerFlight_ModerateZeroWear_MatchesTruncatedMean() {
        var physics = new RealisticPhysicsSettings();
        // λ = 0.01 · (173·1.0 + 179·0.4) · 1.0 · 1.0 = 2.446; cap = 2.
        var expected = RealisticProfileExpectation.MeanTruncatedPoisson(2.446, 2);
        var actual = RealisticProfileExpectation.ExpectedGroundSeedsPerFlight(
            physics, RealisticProfileCatalog.DefaultsFor(RiskLevel.Moderate), minorCount: 173, majorCount: 179, wearPct: 0);
        Assert.Equal(expected, actual, precision: 9);
    }

    [Fact]
    public void ComputePerProfileAsync_DefaultSettings_ModerateMatchesGroundOnlyEstimate() {
        var service = new RealisticExpectationService(Options.Create(new FailureEngineSettings()));

        var results = service.ComputePerProfileAsync(averageWearPct: 0, flightHours: 1.5).GetAwaiter().GetResult();

        Assert.Equal(4, results.Count);
        Assert.Equal(new[] { RiskLevel.Low, RiskLevel.Moderate, RiskLevel.High, RiskLevel.Squawk7700 }, results.Select(r => r.Risk));

        var moderate = results.First(r => r.Risk == RiskLevel.Moderate);
        Assert.Equal(1.0, moderate.Profile.ProbabilityMultiplier);
        Assert.Equal(2, moderate.Profile.GroundFailuresCap);
        Assert.Equal(0, moderate.InFlightPerFlight, precision: 9);
        Assert.Equal(RealisticProfileExpectation.MeanTruncatedPoisson(2.446, 2), moderate.TotalPerFlight, precision: 6);
    }

    [Fact]
    public void ComputePerProfileAsync_WithoutWearOrHours_UsesReferenceDefaults() {
        var service = new RealisticExpectationService(Options.Create(new FailureEngineSettings()));

        var results = service.ComputePerProfileAsync(averageWearPct: null, flightHours: null).GetAwaiter().GetResult();
        var moderate = results.First(r => r.Risk == RiskLevel.Moderate);

        // Sin datos de desgaste el servicio usa el desgaste medio de referencia (25 %), no desgaste cero.
        var (minor, major, _) = FailureCatalogStats.SeverityCounts();
        var lambda = 0.01 * (minor + major * 0.4) * 1.25;
        Assert.Equal(RealisticProfileExpectation.MeanTruncatedPoisson(lambda, 2), moderate.TotalPerFlight, precision: 6);
    }

    [Fact]
    public void ComputePerProfileAsync_WithoutPresets_CriticalsAreZeroBecauseGroundSeedIsMinorOnly() {
        var service = new RealisticExpectationService(Options.Create(new FailureEngineSettings()));

        var results = service.ComputePerProfileAsync(averageWearPct: 0, flightHours: 1.5).GetAwaiter().GetResult();

        Assert.All(results, r => Assert.Equal(0, r.CriticalPerFlight, precision: 12));
    }

    [Fact]
    public void ComputePerProfileAsync_WithPresets_CriticalCountIncludesOnlyCriticalInFlight() {
        var preset = new FailurePreset {
            Name = "Test", FlightPhase = ComplexFlightPhaseEnum.Cruise,
            PresetFailureDefinitions = [
                new() { FenixFailureId = "MINOR", Probability = 1.0, FenixFailure = new FenixFailureDefinition { FenixFailureId = "MINOR", Severity = FailureSeverity.Minor, Group = null! } },
                new() { FenixFailureId = "MAJOR", Probability = 1.0, FenixFailure = new FenixFailureDefinition { FenixFailureId = "MAJOR", Severity = FailureSeverity.Major, Group = null! } },
                new() { FenixFailureId = "CRIT", Probability = 1.0, FenixFailure = new FenixFailureDefinition { FenixFailureId = "CRIT", Severity = FailureSeverity.Critical, Group = null! } },
            ]
        };
        var service = new RealisticExpectationService(
            Options.Create(new FailureEngineSettings()),
            new StubPresetService([preset]));

        var results = service.ComputePerProfileAsync(averageWearPct: 0, flightHours: 1.5).GetAwaiter().GetResult();

        // Crucero: 40/90 min de 1,5 h. λ crítica Moderate = 0,01 × 0,15 × 1,0 × 1,0 × (2/3 · 1,5).
        var cruiseHours = 40.0 / PhaseTotalMinutes * 1.5;
        var expectedCritical = 0.01 * 0.15 * 1.0 * 1.0 * cruiseHours;
        var moderate = results.First(r => r.Risk == RiskLevel.Moderate);
        Assert.Equal(expectedCritical, moderate.CriticalPerFlight, precision: 12);
        Assert.True(moderate.InFlightPerFlight > moderate.CriticalPerFlight, "InFlight debe incluir también Minor y Major.");

        // Low nunca dispara Critical (FailureRuleEvaluator las descarta).
        Assert.Equal(0, results.First(r => r.Risk == RiskLevel.Low).CriticalPerFlight, precision: 12);

        // 7700: λ crítica × 2,5.
        Assert.Equal(expectedCritical * 2.5, results.First(r => r.Risk == RiskLevel.Squawk7700).CriticalPerFlight, precision: 12);
    }

    private const double PhaseTotalMinutes = 10 + 8 + 2 + 18 + 40 + 8 + 10 + 2;

    private sealed class StubPresetService(IReadOnlyList<FailurePreset> realistic) : IPresetService {
        public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;
        public Task DeletePresetAsync(int id, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<FailurePreset>> GetTrainingPresetsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<FailurePreset>>([]);
        public Task<IReadOnlyList<FailurePreset>> GetRealisticPresetsAsync(CancellationToken ct) => Task.FromResult(realistic);
        public Task<IReadOnlyList<FailurePreset>> GetCustomPresetsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<FailurePreset>>([]);
        public Task<FailurePreset?> GetByIdAsync(int presetId, CancellationToken ct) => Task.FromResult<FailurePreset?>(null);
        public Task<FailurePreset> CreateEmptyCustomPresetAsync(CancellationToken ct) => throw new NotSupportedException();
    }
}
