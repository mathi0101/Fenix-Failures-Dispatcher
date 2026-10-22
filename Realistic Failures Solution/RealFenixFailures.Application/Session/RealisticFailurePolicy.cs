using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Interfaces;
using RealFenixFailures.Domain.Services;

namespace RealFenixFailures.Application.Session;

/// <summary>Candidato a falla de tierra con su probabilidad efectiva (ya con desgaste aplicado) y el desgaste de su sistema padre.</summary>
public sealed record GroundFailureCandidate<T>(T Item, double Probability, double WearPercentage);

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

    /// <summary>
    /// Probabilidad efectiva de una falla Minor individual en el seed de tierra:
    /// p_base del perfil amplificada por el desgaste del sistema padre (wear 0 → p_base).
    /// </summary>
    public double GroundFailureProbability(RealisticProfileSettings profile, double wearPercentage) {
        var multiplier = 1.0 + Math.Max(0, profile.GroundWearProbabilityGain) * Math.Max(0, wearPercentage) / 100.0;
        return Math.Clamp(Math.Max(0, profile.GroundFailureBaseProbability) * multiplier, 0, 1);
    }

    /// <summary>
    /// Sorteo Bernoulli independiente por candidato; los elegidos que superen el cap se truncarán priorizando
    /// mayor desgaste → mayor probabilidad → azar. Con cap &lt;= 0 no se selecciona nada.
    /// </summary>
    public IReadOnlyList<GroundFailureCandidate<T>> SelectGroundFailures<T>(IReadOnlyList<GroundFailureCandidate<T>> candidates, int cap) {
        if (cap <= 0 || candidates.Count == 0) return [];
        var rolled = new List<(GroundFailureCandidate<T> Candidate, double OrderKey)>();
        foreach (var candidate in candidates) {
            if (candidate.Probability <= 0) continue;
            if (_random.NextDouble() < candidate.Probability)
                rolled.Add((candidate, _random.NextDouble()));
        }
        if (rolled.Count <= cap) return rolled.Select(x => x.Candidate).ToArray();
        return rolled
            .OrderByDescending(x => x.Candidate.WearPercentage)
            .ThenByDescending(x => x.Candidate.Probability)
            .ThenBy(x => x.OrderKey)
            .Take(cap)
            .Select(x => x.Candidate)
            .ToArray();
    }
}
