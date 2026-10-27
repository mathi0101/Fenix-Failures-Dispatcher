using Microsoft.Extensions.Options;
using RealFenixFailures.Application.Interfaces;
using RealFenixFailures.Application.Services;
using RealFenixFailures.Application.Session;
using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Services;
using RealFenixFailures.TestUnits.Session;
using Xunit;

namespace RealFenixFailures.TestUnits.Services;

/// <summary>
/// Tests de la estimación por perfil de la pantalla de perfiles. Verifican que el servicio cable
/// correctamente las piezas del motor (física, perfil resuelto, desgaste, severidades) sin clavar
/// valores de calibración: los esperados se computan siempre desde los mismos objetos que usa el
/// motor, así que ajustar el balance no rompe la suite salvo que la fórmula se rompa.
/// </summary>
public class RealisticProfileExpectationTests {
    private static readonly RealisticPhysicsSettings Physics = new();
    private static readonly RealisticModeSettings Defaults = new();

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
    public void ExpectedGroundSeedsPerFlight_MatchesThePolicysOwnFormula() {
        var counts = FailureCatalogStats.SeverityCounts();
        foreach (var risk in RealisticProfileDefaultsTests.AllRisks) {
            var profile = Defaults.ProfileFor(risk);
            var wearFactor = RealisticFailurePolicy.WearFactor(profile, wearPercentage: 25);

            var lambda = Physics.GroundSeedBaseProbability
                * (counts.Minor * Physics.SeverityRateMultiplier.Minor + counts.Major * Physics.SeverityRateMultiplier.Major)
                * profile.ProbabilityMultiplier
                * wearFactor;
            var expected = RealisticProfileExpectation.MeanTruncatedPoisson(lambda, profile.GroundFailuresCap);

            var actual = RealisticProfileExpectation.ExpectedGroundSeedsPerFlight(
                Physics, profile, counts.Minor, counts.Major, wearPct: 25);
            Assert.Equal(expected, actual, precision: 9);
        }
    }

    [Fact]
    public void ExpectedGroundSeedsPerFlight_ZeroWear_IsMonotoneAcrossProfiles() {
        var counts = FailureCatalogStats.SeverityCounts();
        var previous = 0.0;
        foreach (var risk in RealisticProfileDefaultsTests.AllRisks) {
            var expected = RealisticProfileExpectation.ExpectedGroundSeedsPerFlight(
                Physics, Defaults.ProfileFor(risk), counts.Minor, counts.Major, wearPct: 0);
            Assert.True(expected > previous, $"Se esperaba crecimiento por perfil, pero {risk} dio {expected}.");
            previous = expected;
        }
    }

    [Fact]
    public void ComputePerProfileAsync_MatchesPolicyFormulaPerProfile() {
        var settings = new FailureEngineSettings();
        var service = new RealisticExpectationService(Options.Create(settings));

        var results = service.ComputePerProfileAsync(averageWearPct: 25).GetAwaiter().GetResult();

        Assert.Equal(RealisticProfileDefaultsTests.AllRisks, results.Select(r => r.Risk));
        var baseRate = settings.RealisticMode.Physics.FailureRatePerHourBase;
        foreach (var result in results) {
            var profile = settings.RealisticMode.ProfileFor(result.Risk);
            Assert.Equal(profile.ProbabilityMultiplier, result.Profile.ProbabilityMultiplier, precision: 9);

            // Fórmula global por severidad: λ = base × mult severidad × mult perfil × factor desgaste.
            var rate = baseRate * profile.ProbabilityMultiplier * RealisticFailurePolicy.WearFactor(profile, 25);
            Assert.Equal(rate * Physics.SeverityRateMultiplier.Minor, result.MinorPerHour, precision: 12);
            Assert.Equal(rate * Physics.SeverityRateMultiplier.Major, result.MajorPerHour, precision: 12);
            var expectedCritical = result.Risk == RiskLevel.Low ? 0 : rate * Physics.SeverityRateMultiplier.Critical;
            Assert.Equal(expectedCritical, result.CriticalPerHour, precision: 12);
            Assert.Equal(result.MinorPerHour + result.MajorPerHour + result.CriticalPerHour, result.TotalPerHour, precision: 12);
        }
    }

    [Fact]
    public void ComputePerProfileAsync_WearScalesRatesByTheProfileWearMax() {
        var service = new RealisticExpectationService(Options.Create(new FailureEngineSettings()));

        var fresh = service.ComputePerProfileAsync(averageWearPct: 0).GetAwaiter().GetResult();
        var worn = service.ComputePerProfileAsync(averageWearPct: 100).GetAwaiter().GetResult();

        foreach (var (f, w) in fresh.Zip(worn, (f, w) => (f, w))) {
            var profile = Defaults.ProfileFor(f.Risk);
            Assert.Equal(profile.WearMaxMultiplier, w.MinorPerHour / f.MinorPerHour, precision: 6);
        }
    }

    [Fact]
    public void ComputePerProfileAsync_WithoutWearOrHours_UsesTheReferenceDefaults() {
        var service = new RealisticExpectationService(Options.Create(new FailureEngineSettings()));

        var withNulls = service.ComputePerProfileAsync(null).GetAwaiter().GetResult();
        var withReferences = service.ComputePerProfileAsync(RealisticExpectationService.DefaultAverageWearPct).GetAwaiter().GetResult();

        // Sin datos, el servicio debe comportarse exactamente igual que con su constante de referencia.
        Assert.All(withNulls.Zip(withReferences, (a, b) => (a, b)),
            pair => Assert.Equal(pair.a.TotalPerHour, pair.b.TotalPerHour, precision: 12));
    }
}
