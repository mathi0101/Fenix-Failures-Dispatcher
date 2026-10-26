// RealFenixFailures.TestUnits/Services/RealisticExpectationServiceTests.cs
using Microsoft.Extensions.Options;
using RealFenixFailures.Application.Services;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Services;
using Xunit;

namespace RealFenixFailures.TestUnits.Services;

/// <summary>
/// Tests deterministas de la estimación de presentación (spec modelo-probabilístico §8.7).
/// Con FailureEngineSettings por defecto (sin perfiles configurados) el catálogo resuelve
/// los defaults, así que los valores esperados son estables.
/// </summary>
public class RealisticExpectationServiceTests {
    private static RealisticExpectationService CreateService() =>
        new(Options.Create(new FailureEngineSettings()));

    [Fact]
    public void Compute_ZeroWearTwoHoursModerate_MatchesReferenceMath() {
        var dto = CreateService().Compute(averageWearPct: 0, flightHours: 2, profileRisk: RiskLevel.Moderate);

        Assert.Equal(0.22, dto.RatePerHour, precision: 5);
        Assert.Equal(0.44, dto.ExpectedFailures, precision: 5);
        Assert.Equal(2, dto.FlightHours);
        Assert.Equal((1 - Math.Exp(-0.22 * 2)) * 100, dto.ProbabilityAtLeastOnePct, precision: 5);
    }

    [Fact]
    public void Compute_WithoutWearOrHours_FallsBackToReferenceRate() {
        var dto = CreateService().Compute(averageWearPct: null, flightHours: null, profileRisk: null);

        Assert.Equal(RealisticExpectationService.ReferenceRatePerHour, dto.RatePerHour, precision: 5);
        Assert.Null(dto.FlightHours);
        Assert.Equal(0, dto.ExpectedFailures);
        Assert.Equal(0, dto.ProbabilityAtLeastOnePct);
    }

    [Fact]
    public void Compute_FullWearModerate_TriplesTheRate() {
        var dto = CreateService().Compute(averageWearPct: 100, flightHours: 1, profileRisk: RiskLevel.Moderate);

        Assert.Equal(0.66, dto.RatePerHour, precision: 5);
        Assert.Equal(0.66, dto.ExpectedFailures, precision: 5);
    }

    [Theory]
    [InlineData(RiskLevel.Low, 0.132)]
    [InlineData(RiskLevel.Moderate, 0.22)]
    [InlineData(RiskLevel.High, 0.33)]
    [InlineData(RiskLevel.Squawk7700, 0.55)]
    public void Compute_ZeroWear_AppliesProfileProbabilityMultiplier(RiskLevel risk, double expectedRate) {
        var dto = CreateService().Compute(averageWearPct: 0, flightHours: 1, profileRisk: risk);

        Assert.Equal(expectedRate, dto.RatePerHour, precision: 5);
    }

    [Fact]
    public void Compute_NonPositiveHours_TreatedAsMissingDuration() {
        var dto = CreateService().Compute(averageWearPct: 0, flightHours: 0, profileRisk: null);

        Assert.Null(dto.FlightHours);
        Assert.Equal(0, dto.ExpectedFailures);
        Assert.Equal(0, dto.ProbabilityAtLeastOnePct);
    }
}
