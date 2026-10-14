using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;

namespace RealFenixFailures.Domain.Interfaces;

public interface IFailureTrigger {
    IReadOnlyList<PresetFailureDefinition> GetTriggeredPresetFailures(FailurePreset preset);

    /// <summary>
    /// Rolls the global probability and, if successful, picks one eligible candidate from
    /// <paramref name="preset"/> weighted by <see cref="PresetFailureDefinition.Probability"/>.
    /// Returns the chosen candidate (not yet armed in Fenix) or null if nothing was triggered.
    /// </summary>
    PresetFailureDefinition? TryTriggerFailure(
        FailurePreset preset,
        ComplexFlightPhaseEnum currentPhase,
        RiskLevel riskLevel,
        double globalProbability,
        IReadOnlyCollection<string> activeFenixFailureIds);
}
