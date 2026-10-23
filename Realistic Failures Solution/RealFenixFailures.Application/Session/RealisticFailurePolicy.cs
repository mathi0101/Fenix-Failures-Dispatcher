using RealFenixFailures.Domain.Entities;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Interfaces;
using RealFenixFailures.Domain.Services;

namespace RealFenixFailures.Application.Session;

/// <summary>Candidato a falla de tierra con su probabilidad efectiva (ya con desgaste aplicado) y el desgaste de su sistema padre.</summary>
public sealed record GroundFailureCandidate<T>(T Item, double Probability, double WearPercentage);

/// <summary>Candidato en vuelo con su λ efectiva (eventos/hora, ya con perfil, severidad, desgaste y peso del preset aplicados).</summary>
public sealed record FlightFailureCandidate<T>(T Item, double Lambda);

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
    public double Next() => _random.NextDouble();

    /// <summary>
    /// Factor de desgaste del sistema padre (espec §3.1): lineal entre 1 (wear 0%) y
    /// <c>WearMaxMultiplier</c> (wear 100%). Sin registro de desgaste → 1 (sin efecto).
    /// </summary>
    public static double WearFactor(RealisticProfileSettings profile, double? wearPercentage) {
        if (wearPercentage is null) return 1.0;
        var wear = Math.Clamp(wearPercentage.Value, 0, 100);
        return 1.0 + (Math.Max(1.0, profile.WearMaxMultiplier) - 1.0) * wear / 100.0;
    }

    /// <summary>
    /// λ de una falla en vuelo, en eventos/hora (fórmula unificada, espec §1):
    /// p_base × mult severidad × mult perfil × factor desgaste × peso del preset.
    /// </summary>
    public static double FlightFailureLambda(FailureSeverity severity, double presetProbability, RealisticProfileSettings profile, RealisticPhysicsSettings physics, double wearFactor) {
        return Math.Max(0, physics.FailureRatePerHourBase)
            * physics.SeverityRateMultiplier.For(severity)
            * Math.Max(0, profile.ProbabilityMultiplier)
            * Math.Max(0, wearFactor)
            * Math.Max(0, presetProbability);
    }

    /// <summary>Probabilidad one-shot (0–1) de una falla individual en el seed de tierra: misma fórmula con la base de tierra.</summary>
    public static double GroundSeedProbability(FailureSeverity severity, double presetProbability, RealisticProfileSettings profile, RealisticPhysicsSettings physics, double wearFactor) {
        var p = Math.Max(0, physics.GroundSeedBaseProbability)
            * physics.SeverityRateMultiplier.For(severity)
            * Math.Max(0, profile.ProbabilityMultiplier)
            * Math.Max(0, wearFactor)
            * Math.Max(0, presetProbability);
        return Math.Clamp(p, 0, 1);
    }

    /// <summary>Peso con que una severidad suma al score de agravamiento de la sesión (espec §2.1).</summary>
    public static int AggravationWeight(RealisticPhysicsSettings physics, FailureSeverity severity) => physics.AggravationWeights.For(severity);

    /// <summary>Extra de agravamiento por cada falla previa simultánea del mismo ATA (espec §2.3): 2º hidráulico +1, 3º +2, …</summary>
    public static int SameAtaExtra(RealisticPhysicsSettings physics, int sameAtaCount) => Math.Max(0, physics.SameAtaAggravationExtra) * Math.Max(0, sameAtaCount);

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

    /// <summary>
    /// Sorteo unificado del tick en vuelo (espec §1): integra el tiempo real transcurrido sobre la Σλ de los
    /// candidatos elegibles. p_tick = 1 − e^(−Σλ·Δt); si el tick dispara, el candidato se elige con un sorteo
    /// ponderado por λ. Invariante al intervalo de evaluación: solo Δt (tiempo real) y las λ determinan la cadencia.
    /// </summary>
    public bool RollTick<T>(IReadOnlyList<FlightFailureCandidate<T>> candidates, double deltaSeconds, out T? chosen) where T : class {
        chosen = null;
        if (candidates.Count == 0) return false;

        var hours = Math.Max(0, deltaSeconds / 3600.0);
        var totalLambda = candidates.Sum(c => Math.Max(0, c.Lambda));
        if (totalLambda <= 0) return false;

        var tickProbability = 1 - Math.Exp(-totalLambda * hours);
        if (_random.NextDouble() >= tickProbability) return false;

        var roll = _random.NextDouble() * totalLambda;
        var cumulative = 0.0;
        foreach (var candidate in candidates) {
            cumulative += Math.Max(0, candidate.Lambda);
            if (roll <= cumulative) {
                chosen = candidate.Item;
                return true;
            }
        }
        chosen = candidates[^1].Item;
        return true;
    }

    /// <summary>
    /// Seed de tierra: sorteo Bernoulli independiente por candidato; los elegidos que superen el cap se truncan
    /// priorizando mayor desgaste → mayor probabilidad → azar. Con cap &lt;= 0 no se selecciona nada.
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
