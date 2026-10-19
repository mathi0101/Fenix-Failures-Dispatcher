using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Interfaces;
using RealFenixFailures.Domain.Services;

namespace RealFenixFailures.Application.Session;

public sealed class RealisticFailurePolicy {
    private readonly IFailureRuleEvaluator _ruleEvaluator;
    private readonly Func<DateTime> _utcNow;
    private readonly Random _random;

    public RealisticFailurePolicy(IFailureRuleEvaluator ruleEvaluator, Func<DateTime>? utcNow = null, Random? random = null) {
        _ruleEvaluator = ruleEvaluator ?? throw new ArgumentNullException(nameof(ruleEvaluator));
        _utcNow = utcNow ?? (static () => DateTime.UtcNow);
        _random = random ?? Random.Shared;
    }

    public DateTime UtcNow => _utcNow();

    public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);

    public static double PerHour(RealisticProfileSettings profile, FailureSeverity severity) => severity switch {
        FailureSeverity.Minor => profile.MinorPerHour,
        FailureSeverity.Major => profile.MajorPerHour,
        FailureSeverity.Critical => profile.CriticalPerHour,
        _ => 0
    };

    public TimeSpan EffectiveRespite(RealisticProfileSettings profile, int clampSeconds) {
        var seconds = Math.Clamp(profile.CriticalRespiteSeconds, 0, Math.Max(0, clampSeconds));
        return TimeSpan.FromSeconds(seconds);
    }

    public IReadOnlyList<PresetFailureDefinition> EvaluateEligible(
        IEnumerable<PresetFailureDefinition> candidates,
        ComplexFlightPhaseEnum currentPhase,
        RiskLevel riskLevel,
        IReadOnlyCollection<string> excludedFenixFailureIds) {
        return _ruleEvaluator.EvaluateEligibleFailures(candidates, currentPhase, riskLevel, excludedFenixFailureIds);
    }

    public bool RollTick(
        RealisticProfileSettings profile,
        double deltaSeconds,
        IReadOnlyDictionary<FailureSeverity, IReadOnlyList<PresetFailureDefinition>> eligibleBySeverity,
        out FailureSeverity chosenSeverity) {
        var available = eligibleBySeverity.Where(kv => kv.Value.Count > 0).ToArray();
        if (available.Length == 0) {
            chosenSeverity = FailureSeverity.Minor;
            return false;
        }

        var hours = Math.Max(0, deltaSeconds / 3600.0);
        var totalLambda = available.Sum(kv => Math.Max(0, PerHour(profile, kv.Key)));
        if (totalLambda <= 0) {
            chosenSeverity = FailureSeverity.Minor;
            return false;
        }

        var tickProbability = 1 - Math.Exp(-totalLambda * hours);
        if (_random.NextDouble() >= tickProbability) {
            chosenSeverity = FailureSeverity.Minor;
            return false;
        }

        var roll = _random.NextDouble() * totalLambda;
        var cumulative = 0.0;
        foreach (var kv in available) {
            cumulative += Math.Max(0, PerHour(profile, kv.Key));
            if (roll <= cumulative) {
                chosenSeverity = kv.Key;
                return true;
            }
        }
        chosenSeverity = available[^1].Key;
        return true;
    }

    public PresetFailureDefinition? ChooseCandidate(IReadOnlyList<PresetFailureDefinition> candidates) {
        if (candidates.Count == 0) return null;
        var totalWeight = candidates.Sum(x => Math.Max(0, x.Probability));
        if (totalWeight <= 0) return null;
        var roll = _random.NextDouble() * totalWeight;
        var cumulative = 0.0;
        foreach (var candidate in candidates) {
            cumulative += Math.Max(0, candidate.Probability);
            if (roll <= cumulative) return candidate;
        }
        return candidates[^1];
    }
}
