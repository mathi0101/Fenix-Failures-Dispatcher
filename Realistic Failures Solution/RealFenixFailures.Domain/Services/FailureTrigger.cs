using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Interfaces;

namespace RealFenixFailures.Domain.Services;

public class FailureTrigger : IFailureTrigger {
    private readonly IFailureRuleEvaluator _ruleEvaluator;
    private readonly Random _random;

    public FailureTrigger(IFailureRuleEvaluator ruleEvaluator, Random? random = null) {
        _ruleEvaluator = ruleEvaluator;
        _random = random ?? Random.Shared;
    }

    public PresetFailureDefinition? TryTriggerFailure(
        FailurePreset preset,
        ComplexFlightPhaseEnum currentPhase,
        RiskLevel riskLevel,
        double globalProbability,
        IReadOnlyCollection<string> activeFenixFailureIds) {
        if (preset.PresetFailureDefinitions.Count == 0) return null;
        if (_random.NextDouble() > globalProbability) return null;

        var eligible = _ruleEvaluator.EvaluateEligibleFailures(preset.PresetFailureDefinitions, currentPhase, riskLevel, activeFenixFailureIds);
        if (eligible.Count == 0) return null;

        var totalWeight = eligible.Sum(x => x.Probability);
        if (totalWeight <= 0) return null;
        var roll = _random.NextDouble() * totalWeight;
        var cumulative = 0.0;

        foreach (var candidate in eligible) {
            cumulative += candidate.Probability;
            if (roll <= cumulative) {
                return candidate;
            }
        }
        return null;
    }

    public IReadOnlyList<PresetFailureDefinition> GetTriggeredPresetFailures(FailurePreset preset) {
        var triggeredFailures = new List<PresetFailureDefinition>();

        var sameFailureGroups = preset.PresetFailureDefinitions.Where(x => x.Probability > 0 && x.ProbabilityGroup is not null).GroupBy(x => x.ProbabilityGroup);
        var independantFailures = preset.PresetFailureDefinitions.Where(x => x.Probability > 0 && x.ProbabilityGroup is null);

        foreach (var group in sameFailureGroups) {
            var candidates = group.ToArray();
            _random.Shuffle(candidates);               // Hacemos Shuffle a los candidatos para evitar ventaja de eleccion debido a orden de ejecución

            var totalWeight = candidates.Sum(x => x.Probability);
            if (!ProbabilidadEsExitosa(totalWeight, out double roll))    //Chequeamos si hay probabilidad de que no se active nada en el grupo
                continue;
            var cumulative = 0.0;

            foreach (var candidate in candidates) {
                cumulative += candidate.Probability;
                if (roll <= cumulative) {
                    triggeredFailures.Add(candidate);
                    break;
                }
            }
        }

        foreach (var candidate in independantFailures) {
            if (ProbabilidadEsExitosa(candidate.Probability, out double roll))
                triggeredFailures.Add(candidate);
        }

        return triggeredFailures;
    }

    private bool ProbabilidadEsExitosa(double probability, out double roll) {
        roll = _random.NextDouble();
        if (probability <= 0.0) return false;
        if (probability >= 1.0) return true;
        return roll <= probability;
    }
}
