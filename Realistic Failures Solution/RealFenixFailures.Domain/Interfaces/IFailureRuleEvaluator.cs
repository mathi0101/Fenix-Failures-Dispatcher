using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;

namespace RealFenixFailures.Domain.Interfaces;

public interface IFailureRuleEvaluator {
    /// <summary>
    /// Filters candidates down to the ones eligible to be armed right now: their preset matches
    /// <paramref name="currentPhase"/>, their severity is allowed by <paramref name="riskLevel"/>,
    /// and they are not already armed/failed in Fenix (per <paramref name="activeFenixFailureIds"/>).
    /// Altitude/IAS/time/after-event conditions are NOT evaluated here: they are delegated to Fenix,
    /// which waits for them itself once a failure is armed via <c>ISimulatorConnectionService.ExecuteFailureAsync</c>.
    /// </summary>
    IReadOnlyList<PresetFailureDefinition> EvaluateEligibleFailures(
        IEnumerable<PresetFailureDefinition> candidates,
        ComplexFlightPhaseEnum currentPhase,
        RiskLevel riskLevel,
        IReadOnlyCollection<string> activeFenixFailureIds);

    IReadOnlyList<PresetFailureDefinition> EvaluateEligibleFailures(
        FailurePreset preset,
        RiskLevel riskLevel,
        IReadOnlyCollection<string> activeFenixFailureIds);
}
