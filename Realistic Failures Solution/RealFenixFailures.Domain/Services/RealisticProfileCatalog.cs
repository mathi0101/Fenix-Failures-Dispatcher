using RealFenixFailures.Domain.Enums;

namespace RealFenixFailures.Domain.Services;

public static class RealisticProfileCatalog {
    public static RealisticProfileSettings DefaultsFor(RiskLevel risk) => risk switch {
        RiskLevel.Low => new RealisticProfileSettings {
            RiskLevel = 1, ProbabilityMultiplier = 0.75, WearMaxMultiplier = 1.5,
            AggravationCap = 3, GroundFailuresCap = 1, CriticalRespiteSeconds = 0
        },
        RiskLevel.Moderate => new RealisticProfileSettings {
            RiskLevel = 2, ProbabilityMultiplier = 1.0, WearMaxMultiplier = 2.0,
            AggravationCap = 5, GroundFailuresCap = 2, CriticalRespiteSeconds = 0
        },
        RiskLevel.High => new RealisticProfileSettings {
            RiskLevel = 3, ProbabilityMultiplier = 1.5, WearMaxMultiplier = 2.5,
            AggravationCap = 7, GroundFailuresCap = 3, CriticalRespiteSeconds = 600
        },
        RiskLevel.Squawk7700 => new RealisticProfileSettings {
            RiskLevel = 4, ProbabilityMultiplier = 2.5, WearMaxMultiplier = 4.0,
            AggravationCap = 10, GroundFailuresCap = 5, CriticalRespiteSeconds = 600
        },
        _ => DefaultsFor(RiskLevel.Moderate)
    };

    public static RealisticProfileSettings Resolve(RealisticModeSettings? settings, RiskLevel risk) {
        var configured = settings?.Profiles?.FirstOrDefault(p => p.RiskLevel == (int)risk);
        return configured ?? DefaultsFor(risk);
    }
}
