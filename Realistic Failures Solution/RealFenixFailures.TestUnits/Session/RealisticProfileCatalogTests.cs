// RealFenixFailures.TestUnits/Session/RealisticProfileCatalogTests.cs
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Services;
using Xunit;

namespace RealFenixFailures.TestUnits.Session;

public class RealisticProfileCatalogTests {
    [Theory]
    [InlineData(RiskLevel.Low, 0.75, 1.5, 3)]
    [InlineData(RiskLevel.Moderate, 1.0, 2.0, 5)]
    [InlineData(RiskLevel.High, 1.5, 2.5, 7)]
    public void DefaultsFor_ConfiguredKnobsPerRisk(RiskLevel risk, double probabilityMultiplier, double wearMaxMultiplier, int aggravationCap) {
        var profile = RealisticProfileCatalog.DefaultsFor(risk);
        Assert.Equal(probabilityMultiplier, profile.ProbabilityMultiplier, precision: 5);
        Assert.Equal(wearMaxMultiplier, profile.WearMaxMultiplier, precision: 5);
        Assert.Equal(aggravationCap, profile.AggravationCap);
    }

    [Fact]
    public void DefaultsFor_Squawk7700_MatchesCurrentConfiguration() {
        var profile = RealisticProfileCatalog.DefaultsFor(RiskLevel.Squawk7700);

        Assert.Equal(10, profile.AggravationCap);
        Assert.Equal(600, profile.CriticalRespiteSeconds);
        Assert.Equal(2.5, profile.ProbabilityMultiplier, precision: 5);
        Assert.Equal(4.0, profile.WearMaxMultiplier, precision: 5);
    }

    [Fact]
    public void Resolve_PrefersConfiguredProfileOverDefaults() {
        var settings = new RealisticModeSettings {
            Profiles = {
                new RealisticProfileSettings { RiskLevel = 1, AggravationCap = 99, ProbabilityMultiplier = 3.0 },
                new RealisticProfileSettings { RiskLevel = 2, AggravationCap = 4, ProbabilityMultiplier = 0.5 }
            }
        };

        var low = RealisticProfileCatalog.Resolve(settings, RiskLevel.Low);
        Assert.Equal(99, low.AggravationCap);
        Assert.Equal(3.0, low.ProbabilityMultiplier, precision: 5);

        var moderate = RealisticProfileCatalog.Resolve(settings, RiskLevel.Moderate);
        Assert.Equal(4, moderate.AggravationCap);
        Assert.Equal(0.5, moderate.ProbabilityMultiplier, precision: 5);
    }

    [Fact]
    public void Resolve_FallsBackToDefaultsWhenNoConfiguredProfile() {
        var settings = new RealisticModeSettings();

        var high = RealisticProfileCatalog.Resolve(settings, RiskLevel.High);

        Assert.Equal(7, high.AggravationCap);
        Assert.Equal(1200, high.CriticalRespiteSeconds);
    }

    [Theory]
    [InlineData(RiskLevel.Low, 1)]
    [InlineData(RiskLevel.Moderate, 2)]
    [InlineData(RiskLevel.High, 3)]
    [InlineData(RiskLevel.Squawk7700, 5)]
    public void Resolve_ConfiguredGroundFailuresCapPerRisk(RiskLevel risk, int cap) {
        var settings = new RealisticModeSettings {
            Profiles = {
                new RealisticProfileSettings { RiskLevel = 1, GroundFailuresCap = 1 },
                new RealisticProfileSettings { RiskLevel = 2, GroundFailuresCap = 2 },
                new RealisticProfileSettings { RiskLevel = 3, GroundFailuresCap = 3 },
                new RealisticProfileSettings { RiskLevel = 4, GroundFailuresCap = 5 }
            }
        };

        var profile = RealisticProfileCatalog.Resolve(settings, risk);

        Assert.Equal(cap, profile.GroundFailuresCap);
    }

    [Fact]
    public void Resolve_WithoutConfiguredProfile_DefaultsGroundFailuresCapPerRisk() {
        var profile = RealisticProfileCatalog.Resolve(null, RiskLevel.High);

        Assert.Equal(3, profile.GroundFailuresCap);
    }

    [Fact]
    public void RealisticModeSettings_DefaultPhysics_MatchSpec() {
        var physics = new RealisticModeSettings().Physics;

        Assert.Equal(0.01, physics.FailureRatePerHourBase, precision: 6);
        Assert.Equal(0.01, physics.GroundSeedBaseProbability, precision: 6);
        Assert.Equal(1.0, physics.SeverityRateMultiplier.Minor, precision: 5);
        Assert.Equal(0.4, physics.SeverityRateMultiplier.Major, precision: 5);
        Assert.Equal(0.15, physics.SeverityRateMultiplier.Critical, precision: 5);
        Assert.Equal(1, physics.AggravationWeights.Minor);
        Assert.Equal(2, physics.AggravationWeights.Major);
        Assert.Equal(4, physics.AggravationWeights.Critical);
        Assert.Equal(1, physics.SameAtaAggravationExtra);
    }
}
