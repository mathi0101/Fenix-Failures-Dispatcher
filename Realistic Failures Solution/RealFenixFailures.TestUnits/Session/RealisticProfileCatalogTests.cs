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
    [InlineData(RiskLevel.Low, 1, 2)]
    [InlineData(RiskLevel.Moderate, 1, 3)]
    [InlineData(RiskLevel.High, 2, 4)]
    [InlineData(RiskLevel.Squawk7700, 3, 5)]
    public void GroundRange_DefaultsPerRisk(RiskLevel risk, int min, int max) {
        var (actualMin, actualMax) = RealisticProfileCatalog.GroundRange(null, risk);

        Assert.Equal(min, actualMin);
        Assert.Equal(max, actualMax);
    }

    [Fact]
    public void GroundRange_UsesConfiguredRangeForRisk() {
        var settings = new RealisticModeSettings {
            GroundFailureRanges = {
                new GroundFailureRangeSettings { RiskLevel = 3, Min = 1, Max = 1 }
            }
        };

        var (min, max) = RealisticProfileCatalog.GroundRange(settings, RiskLevel.High);

        Assert.Equal(1, min);
        Assert.Equal(1, max);
    }
}
