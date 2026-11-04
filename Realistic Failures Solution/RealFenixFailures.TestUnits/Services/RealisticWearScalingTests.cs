using RealFenixFailures.Application.Session;
using RealFenixFailures.Domain.Enums;

namespace RealFenixFailures.TestUnits.Services;

public class RealisticWearScalingTests {
    [Fact]
    public void CountForHours_ScalesWithHours_ClampedBetweenThreeAndEligible() {
        Assert.Equal(3, RealisticWearScaling.CountForHours(0.5, eligibleCount: 19, sessionSystemsCap: 0));
        Assert.Equal(5, RealisticWearScaling.CountForHours(3.0, eligibleCount: 19, sessionSystemsCap: 0));
        Assert.Equal(19, RealisticWearScaling.CountForHours(15.0, eligibleCount: 19, sessionSystemsCap: 0));
        Assert.Equal(19, RealisticWearScaling.CountForHours(60.0, eligibleCount: 19, sessionSystemsCap: 0));
        Assert.Equal(4, RealisticWearScaling.CountForHours(15.0, eligibleCount: 19, sessionSystemsCap: 4));
        Assert.Equal(0, RealisticWearScaling.CountForHours(3.0, eligibleCount: 0, sessionSystemsCap: 0));
    }

    [Fact]
    public void ExpectedFlightHoursToWear_AveragesTheSelectionLottery() {
        // 3 h de sesión → 5 de 19 sistemas: tasa efectiva 0.75 × 5/19 %/h.
        var hours = RealisticWearScaling.ExpectedFlightHoursToWear(25, wearPerFlightHour: 0.75, sessionHours: 3.0, eligibleCount: 19, sessionSystemsCap: 0);

        Assert.Equal(25 / (0.75 * 5.0 / 19), hours, precision: 9);
    }

    [Fact]
    public void ExpectedFlightHoursToWear_WithAllSystemsSelected_EqualsRateInverse() {
        // Vuelos de 15 h sortean a los 19 sistemas: el sistema acumula siempre.
        var hours = RealisticWearScaling.ExpectedFlightHoursToWear(100, wearPerFlightHour: 0.5, sessionHours: 15.0, eligibleCount: 19, sessionSystemsCap: 0);

        Assert.Equal(200, hours, precision: 9);
    }

    [Fact]
    public void ExpectedFlightHoursToWear_GlobalMultiplier_ScalesLinearly() {
        var baseHours = RealisticWearScaling.ExpectedFlightHoursToWear(50, wearPerFlightHour: 0.3, sessionHours: 3.0, eligibleCount: 19, sessionSystemsCap: 0, wearRateMultiplier: 1.0);
        var doubled = RealisticWearScaling.ExpectedFlightHoursToWear(50, wearPerFlightHour: 0.3, sessionHours: 3.0, eligibleCount: 19, sessionSystemsCap: 0, wearRateMultiplier: 2.0);

        Assert.Equal(baseHours / 2, doubled, precision: 9);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.3)]
    public void ExpectedFlightHoursToWear_InvalidRate_IsInfinite(double wearPerFlightHour) {
        Assert.True(double.IsPositiveInfinity(RealisticWearScaling.ExpectedFlightHoursToWear(
            50, wearPerFlightHour, sessionHours: 3.0, eligibleCount: 19, sessionSystemsCap: 0)));
    }
}
