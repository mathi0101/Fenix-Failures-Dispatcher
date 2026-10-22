// RealFenixFailures.TestUnits/Session/RealisticProfileCatalogTests.cs
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Services;
using Xunit;

namespace RealFenixFailures.TestUnits.Session;

public class RealisticProfileCatalogTests {
    [Theory]
    [InlineData(RiskLevel.Low, 5, 2, 0)]
    [InlineData(RiskLevel.Moderate, 7, 4, 1)]
    [InlineData(RiskLevel.High, 10, 5, 2)]
    public void DefaultsFor_ConfiguredCapsPerRisk(RiskLevel risk, int minor, int major, int critical) {
        var profile = RealisticProfileCatalog.DefaultsFor(risk);
        Assert.Equal(minor, profile.MinorCap);
        Assert.Equal(major, profile.MajorCap);
        Assert.Equal(critical, profile.CriticalCap);
    }

    [Fact]
    public void DefaultsFor_Squawk7700_HasNoCaps() {
        var profile = RealisticProfileCatalog.DefaultsFor(RiskLevel.Squawk7700);

        Assert.Null(profile.MinorCap);
        Assert.Null(profile.MajorCap);
        Assert.Null(profile.CriticalCap);
    }

    [Fact]
    public void Resolve_PrefersConfiguredProfileOverDefaults() {
        var settings = new RealisticModeSettings {
            Profiles = {
                new RealisticProfileSettings { RiskLevel = 1, MinorCap = 99, MajorCap = 0, CriticalCap = 0 },
                new RealisticProfileSettings { RiskLevel = 2, MinorCap = 3, MajorCap = 2, CriticalCap = 2 }
            }
        };

        var low = RealisticProfileCatalog.Resolve(settings, RiskLevel.Low);
        Assert.Equal(99, low.MinorCap);

        var moderate = RealisticProfileCatalog.Resolve(settings, RiskLevel.Moderate);
        Assert.Equal(3, moderate.MinorCap);
    }

    [Fact]
    public void Resolve_FallsBackToDefaultsWhenNoConfiguredProfile() {
        var settings = new RealisticModeSettings();

        var high = RealisticProfileCatalog.Resolve(settings, RiskLevel.High);

        Assert.Equal(10, high.MinorCap);
        Assert.Equal(600, high.CriticalRespiteSeconds);
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
    public void Resolve_WithoutConfiguredProfile_DefaultsGroundFailuresCapToZero() {
        var profile = RealisticProfileCatalog.Resolve(null, RiskLevel.High);

        Assert.Equal(0, profile.GroundFailuresCap);
    }

    [Theory]
    [InlineData(RiskLevel.Low, 0.004, 1.5)]
    [InlineData(RiskLevel.Moderate, 0.01, 2)]
    [InlineData(RiskLevel.High, 0.015, 2.5)]
    [InlineData(RiskLevel.Squawk7700, 0.025, 3)]
    public void DefaultsFor_ConfiguredGroundProbabilityPerRisk(RiskLevel risk, double baseProbability, double wearGain) {
        var profile = RealisticProfileCatalog.DefaultsFor(risk);

        Assert.Equal(baseProbability, profile.GroundFailureBaseProbability, precision: 5);
        Assert.Equal(wearGain, profile.GroundWearProbabilityGain, precision: 5);
    }
}
