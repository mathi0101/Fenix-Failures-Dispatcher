using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Interfaces;

namespace RealFenixFailures.Domain.Services;

public class FailureRuleEvaluator : IFailureRuleEvaluator {
    private readonly Random _random;

    public FailureRuleEvaluator() {
        _random = Random.Shared;
    }

    public IReadOnlyList<PresetFailureDefinition> EvaluateEligibleFailures(
        IEnumerable<PresetFailureDefinition> candidates,
        ComplexFlightPhaseEnum currentPhase,
        RiskLevel riskLevel,
        IReadOnlyCollection<string> activeFenixFailureIds) {
        return candidates
            .Where(c => c.Preset == null || c.Preset.FlightPhase == currentPhase)
            .Where(c => c.FenixFailure == null || IsSeverityAllowed(c.FenixFailure.Severity, riskLevel))
            .Where(c => !activeFenixFailureIds.Contains(c.FenixFailureId))
            .ToList();
    }

    public IReadOnlyList<PresetFailureDefinition> EvaluateEligibleFailures(
        FailurePreset preset,
        RiskLevel riskLevel,
        IReadOnlyCollection<string> activeFenixFailureIds) {
        return EvaluateEligibleFailures(preset.PresetFailureDefinitions, preset.FlightPhase, riskLevel, activeFenixFailureIds);
    }

    private static bool IsSeverityAllowed(FailureSeverity severity, RiskLevel riskLevel) => riskLevel switch {
        RiskLevel.Low => severity is FailureSeverity.Minor or FailureSeverity.Major,
        RiskLevel.Squawk7700 => true,
        _ => true
    };
}
