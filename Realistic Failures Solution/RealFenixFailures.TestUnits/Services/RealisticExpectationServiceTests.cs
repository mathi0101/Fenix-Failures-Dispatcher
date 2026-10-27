// RealFenixFailures.TestUnits/Services/RealisticExpectationServiceTests.cs
using Microsoft.Extensions.Options;
using RealFenixFailures.Application.Services;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Services;
using Xunit;

namespace RealFenixFailures.TestUnits.Services;

/// <summary>
/// Tests deterministas de la estimación de presentación (spec modelo-probabilístico §8.7).
/// Los casos de tasa esperada se derivan de FailureEngineSettings (no hay valores hardcodeados):
/// ajustar la calibración no rompe la suite; lo que se fija es la fórmula y las relaciones.
/// </summary>
public class RealisticExpectationServiceTests {
    private static RealisticExpectationService CreateService(FailureEngineSettings? settings = null) =>
        new(Options.Create(settings ?? new FailureEngineSettings()));

    [Fact]
    public void Compute_ZeroWearTwoHoursModerate_MatchesReferenceMath() {
        var settings = new FailureEngineSettings();
        var dto = CreateService(settings).Compute(averageWearPct: 0, flightHours: 2, profileRisk: RiskLevel.Moderate);

        // ExpectedFailures/P(≥1) usan la fórmula global por severidad (base × mult × perfil), no la constante de referencia.
        var physics = settings.RealisticMode.Physics;
        var mult = settings.RealisticMode.Moderate.ProbabilityMultiplier;
        var rate = physics.FailureRatePerHourBase
            * (physics.SeverityRateMultiplier.Minor + physics.SeverityRateMultiplier.Major + physics.SeverityRateMultiplier.Critical)
            * mult;
        Assert.Equal(rate, dto.ExpectedFailures / 2, precision: 5);
        Assert.Equal(2, dto.FlightHours);
        Assert.Equal((1 - Math.Exp(-rate * 2)) * 100, dto.ProbabilityAtLeastOnePct, precision: 5);
    }

    [Fact]
    public void Compute_WithoutWearOrHours_FallsBackToReferenceRate() {
        var settings = new FailureEngineSettings();
        var dto = CreateService(settings).Compute(averageWearPct: null, flightHours: null, profileRisk: null);

        // Sin perfil explícito la referencia es Moderado: la tasa base escala por su multiplicador vigente.
        var expected = RealisticExpectationService.ReferenceRatePerHour * settings.RealisticMode.Moderate.ProbabilityMultiplier;
        Assert.Equal(expected, dto.RatePerHour, precision: 5);
        Assert.Null(dto.FlightHours);
        Assert.Equal(0, dto.ExpectedFailures);
        Assert.Equal(0, dto.ProbabilityAtLeastOnePct);
    }

    [Fact]
    public void Compute_FullWearModerate_ScalesRateByProfileWearMaxMultiplier() {
        var settings = new FailureEngineSettings();
        var dto = CreateService(settings).Compute(averageWearPct: 100, flightHours: 1, profileRisk: RiskLevel.Moderate);
        var zeroWear = CreateService(settings).Compute(averageWearPct: 0, flightHours: 1, profileRisk: RiskLevel.Moderate);

        var expected = RealisticExpectationService.ReferenceRatePerHour
            * settings.RealisticMode.Moderate.ProbabilityMultiplier * settings.RealisticMode.Moderate.WearMaxMultiplier;
        Assert.Equal(expected, dto.RatePerHour, precision: 5);
        Assert.Equal(settings.RealisticMode.Moderate.WearMaxMultiplier, dto.RatePerHour / zeroWear.RatePerHour, precision: 5);
    }

    [Theory]
    [InlineData(RiskLevel.Low)]
    [InlineData(RiskLevel.Moderate)]
    [InlineData(RiskLevel.High)]
    [InlineData(RiskLevel.Squawk7700)]
    public void Compute_ZeroWear_AppliesProfileProbabilityMultiplier(RiskLevel risk) {
        var settings = new FailureEngineSettings();
        var dto = CreateService(settings).Compute(averageWearPct: 0, flightHours: 1, profileRisk: risk);

        var expected = RealisticExpectationService.ReferenceRatePerHour
            * settings.RealisticMode.ProfileFor(risk).ProbabilityMultiplier;
        Assert.Equal(expected, dto.RatePerHour, precision: 5);
    }

    [Fact]
    public void Compute_NonPositiveHours_TreatedAsMissingDuration() {
        var dto = CreateService().Compute(averageWearPct: 0, flightHours: 0, profileRisk: null);

        Assert.Null(dto.FlightHours);
        Assert.Equal(0, dto.ExpectedFailures);
        Assert.Equal(0, dto.ProbabilityAtLeastOnePct);
    }
}
