using RealFenixFailures.Domain.Enums;

namespace RealFenixFailures.Domain.Services;

public static class RealisticProfileCatalog {
    public static RealisticProfileSettings DefaultsFor(RiskLevel risk) => risk switch {
        RiskLevel.Low => new RealisticProfileSettings {
            RiskLevel = 1, MinorCap = 5, MajorCap = 2, CriticalCap = 0,
            MinorPerHour = 0.5, MajorPerHour = 0.05, CriticalPerHour = 0, CriticalRespiteSeconds = 0
        },
        RiskLevel.Moderate => new RealisticProfileSettings {
            RiskLevel = 2, MinorCap = 7, MajorCap = 4, CriticalCap = 1,
            MinorPerHour = 0.2, MajorPerHour = 0.1, CriticalPerHour = 0.01, CriticalRespiteSeconds = 0
        },
        RiskLevel.High => new RealisticProfileSettings {
            RiskLevel = 3, MinorCap = 10, MajorCap = 5, CriticalCap = 2,
            MinorPerHour = 0.3, MajorPerHour = 0.25, CriticalPerHour = 0.1, CriticalRespiteSeconds = 600
        },
        RiskLevel.Squawk7700 => new RealisticProfileSettings {
            RiskLevel = 4, MinorCap = null, MajorCap = null, CriticalCap = null,
            MinorPerHour = 0.6, MajorPerHour = 0.5, CriticalPerHour = 0.3, CriticalRespiteSeconds = 300
        },
        _ => DefaultsFor(RiskLevel.Moderate)
    };

    public static RealisticProfileSettings Resolve(RealisticModeSettings? settings, RiskLevel risk) {
        var configured = settings?.Profiles?.FirstOrDefault(p => p.RiskLevel == (int)risk);
        return configured ?? DefaultsFor(risk);
    }

    public static (int Min, int Max) GroundRange(RealisticModeSettings? settings, RiskLevel risk) {
        var configured = settings?.GroundFailureRanges?.FirstOrDefault(r => r.RiskLevel == (int)risk);
        (int Min, int Max) defaults = risk switch {
            RiskLevel.Low => (1, 2),
            RiskLevel.Moderate => (1, 3),
            RiskLevel.High => (2, 4),
            RiskLevel.Squawk7700 => (3, 5),
            _ => (1, 2)
        };
        if (configured is null) return defaults;
        var min = configured.Min > 0 ? configured.Min : defaults.Min;
        var max = configured.Max >= min ? configured.Max : Math.Max(min, defaults.Max);
        return (min, max);
    }
}
