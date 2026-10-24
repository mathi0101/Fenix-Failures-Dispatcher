// RealFenixFailures.TestUnits/Session/RealisticFailurePolicyGroundSeedTests.cs
using RealFenixFailures.Application.Session;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Interfaces;
using RealFenixFailures.Domain.Services;
using RealFenixFailures.TestUnits.Mocks;
using Xunit;

namespace RealFenixFailures.TestUnits.Session;

/// <summary>
/// Fórmula unificada y score de agravamiento (espec modelo-probabilístico §1–§2):
/// seed de tierra Bernoulli individual con la física global, λ de vuelo integrada por Δt en RollTick,
/// factor de desgaste lineal hasta WearMaxMultiplier, y costos de score con extra por mismo ATA.
/// </summary>
public class RealisticFailurePolicyGroundSeedTests {
    private readonly MockRandom _random = new();

    private RealisticFailurePolicy CreatePolicy() =>
        new(new FailureRuleEvaluator(), () => new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc), _random);

    private static RealisticPhysicsSettings Physics() => new();

    private static RealisticProfileSettings Profile(double probabilityMultiplier = 1.0, double wearMaxMultiplier = 3.0, int? aggravationCap = 5) => new() {
        RiskLevel = 2, ProbabilityMultiplier = probabilityMultiplier, WearMaxMultiplier = wearMaxMultiplier,
        AggravationCap = aggravationCap, GroundFailuresCap = 2, CriticalRespiteSeconds = 0
    };

    // -------------------------------------------------------------------------
    // GroundSeedProbability (fórmula unificada, base de tierra)
    // -------------------------------------------------------------------------

    [Fact]
    public void GroundSeed_WithoutWear_EqualsBase() {
        var p = RealisticFailurePolicy.GroundSeedProbability(FailureSeverity.Minor, 1.0, Profile(), Physics(), wearFactor: 1.0);

        Assert.Equal(0.01, p, precision: 6);
    }

    [Theory]
    [InlineData(50, 0.02)]   // max ×3 → 50% de desgaste duplica la base
    [InlineData(100, 0.03)]  // max ×3 → 100% de desgaste triplica la base
    public void GroundSeed_WithWear_AmplifiesBase(double wear, double expected) {
        var wearFactor = RealisticFailurePolicy.WearFactor(Profile(), wear);

        var p = RealisticFailurePolicy.GroundSeedProbability(FailureSeverity.Minor, 1.0, Profile(), Physics(), wearFactor);

        Assert.Equal(expected, p, precision: 6);
    }

    [Fact]
    public void GroundSeed_WithoutWearRecord_FactorIsOne() {
        Assert.Equal(1.0, RealisticFailurePolicy.WearFactor(Profile(), wearPercentage: null), precision: 6);

        var p = RealisticFailurePolicy.GroundSeedProbability(FailureSeverity.Minor, 1.0, Profile(), Physics(), wearFactor: 1.0);
        Assert.Equal(0.01, p, precision: 6);
    }

    [Fact]
    public void GroundSeed_ClampsToUnit() {
        var physics = new RealisticPhysicsSettings { GroundSeedBaseProbability = 0.9 };

        var p = RealisticFailurePolicy.GroundSeedProbability(FailureSeverity.Minor, 1.0, Profile(), physics, wearFactor: 3.0);

        Assert.Equal(1.0, p, precision: 6);
    }

    [Fact]
    public void GroundSeed_NegativeBase_TreatedAsZero() {
        var physics = new RealisticPhysicsSettings { GroundSeedBaseProbability = -1 };

        var p = RealisticFailurePolicy.GroundSeedProbability(FailureSeverity.Minor, 1.0, Profile(), physics, wearFactor: 2.0);

        Assert.Equal(0.0, p, precision: 6);
    }

    // -------------------------------------------------------------------------
    // WearFactor (espec §3.1)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(null, 1.0)]
    [InlineData(0.0, 1.0)]
    [InlineData(50.0, 2.0)]
    [InlineData(100.0, 3.0)]
    [InlineData(150.0, 3.0)] // clamp superior
    [InlineData(-10.0, 1.0)] // clamp inferior
    public void WearFactor_IsLinearUpToMax(double? wear, double expected) {
        Assert.Equal(expected, RealisticFailurePolicy.WearFactor(Profile(wearMaxMultiplier: 3.0), wear), precision: 6);
    }

    // -------------------------------------------------------------------------
    // FlightFailureLambda (fórmula unificada, base de vuelo)
    // -------------------------------------------------------------------------

    [Fact]
    public void Lambda_MinorWithoutWear_EqualsBaseTimesPreset() {
        var lambda = RealisticFailurePolicy.FlightFailureLambda(FailureSeverity.Minor, 0.5, Profile(), Physics(), wearFactor: 1.0);

        Assert.Equal(0.005, lambda, precision: 8);
    }

    [Fact]
    public void Lambda_SeverityMultiplier_Applies() {
        var minor = RealisticFailurePolicy.FlightFailureLambda(FailureSeverity.Minor, 1.0, Profile(), Physics(), 1.0);
        var major = RealisticFailurePolicy.FlightFailureLambda(FailureSeverity.Major, 1.0, Profile(), Physics(), 1.0);
        var critical = RealisticFailurePolicy.FlightFailureLambda(FailureSeverity.Critical, 1.0, Profile(), Physics(), 1.0);

        Assert.Equal(0.01, minor, precision: 8);
        Assert.Equal(0.004, major, precision: 8);
        Assert.Equal(0.0015, critical, precision: 8);
    }

    [Fact]
    public void Lambda_ProfileMultiplier_Applies() {
        var lambda = RealisticFailurePolicy.FlightFailureLambda(FailureSeverity.Minor, 1.0, Profile(probabilityMultiplier: 2.5), Physics(), 1.0);

        Assert.Equal(0.025, lambda, precision: 8);
    }

    // -------------------------------------------------------------------------
    // Score de agravamiento (espec §2)
    // -------------------------------------------------------------------------

    [Fact]
    public void AggravationWeight_MatchesSpec() {
        var physics = Physics();
        Assert.Equal(1, RealisticFailurePolicy.AggravationWeight(physics, FailureSeverity.Minor));
        Assert.Equal(2, RealisticFailurePolicy.AggravationWeight(physics, FailureSeverity.Major));
        Assert.Equal(4, RealisticFailurePolicy.AggravationWeight(physics, FailureSeverity.Critical));
    }

    [Fact]
    public void SameAtaExtra_AccumulatesPerPreviousFailureOfSameAta() {
        var physics = Physics();
        Assert.Equal(0, RealisticFailurePolicy.SameAtaExtra(physics, 0));
        Assert.Equal(1, RealisticFailurePolicy.SameAtaExtra(physics, 1)); // 2º hidráulico: 2 + 1 = 3 de costo
        Assert.Equal(2, RealisticFailurePolicy.SameAtaExtra(physics, 2)); // 3º hidráulico: 2 + 2 = 4
    }

    // -------------------------------------------------------------------------
    // RollTick: invariancia al intervalo (espec §7.2)
    // -------------------------------------------------------------------------

    [Fact]
    public void RollTick_FireRate_IsInvariantToEvaluationInterval() {
        const double lambda = 0.5; // eventos/hora
        const int trials = 4000;
        const double hours = 1.0;
        var expected = 1 - Math.Exp(-lambda * hours); // ≈ 0.393

        var rate5 = SimulateTrails(lambda, trials, hours, intervalSeconds: 5);
        var rate30 = SimulateTrails(lambda, trials, hours, intervalSeconds: 30);

        Assert.InRange(rate5, expected - 0.05, expected + 0.05);
        Assert.InRange(rate30, expected - 0.05, expected + 0.05);
    }

    /// <summary>Cubre <paramref name="hours"/> horas con ticks de <paramref name="intervalSeconds"/> y cuenta en cuántas simulaciones disparó al menos una falla.</summary>
    private double SimulateTrails(double lambda, int trials, double hours, int intervalSeconds) {
        var fired = 0;
        var ticks = (int)Math.Ceiling(hours * 3600 / intervalSeconds);
        for (var trial = 0; trial < trials; trial++) {
            var random = new Random(1000 + trial);
            var policy = new RealisticFailurePolicy(new FailureRuleEvaluator(), random: random);
            var candidates = new[] { new FlightFailureCandidate<string>("x", lambda) };
            var firedThisTrial = false;
            for (var tick = 0; tick < ticks && !firedThisTrial; tick++) {
                firedThisTrial = policy.RollTick(candidates, intervalSeconds, out _);
            }
            if (firedThisTrial) fired++;
        }
        return (double)fired / trials;
    }

    // -------------------------------------------------------------------------
    // SelectGroundFailures
    // -------------------------------------------------------------------------

    private static GroundFailureCandidate<string> Candidate(string item, double probability, double wear) =>
        new(item, probability, wear);

    [Fact]
    public void Select_NonPositiveCap_SelectsNothing() {
        var candidates = new[] { Candidate("a", 1.0, 0) };

        Assert.Empty(CreatePolicy().SelectGroundFailures(candidates, cap: 0));
        Assert.Empty(CreatePolicy().SelectGroundFailures(candidates, cap: -1));
    }

    [Fact]
    public void Select_ZeroProbability_NeverSelected() {
        var candidates = new[] { Candidate("a", 0.0, 0) };

        Assert.Empty(CreatePolicy().SelectGroundFailures(candidates, cap: 5));
    }

    [Fact]
    public void Select_AllRolledBelowCap_KeepsAll() {
        var candidates = new[] { Candidate("a", 0.5, 0), Candidate("b", 0.5, 0) };
        _random.SetupNextDouble(0.1); // Bernoulli a
        _random.SetupNextDouble(0.9); // order key a
        _random.SetupNextDouble(0.9); // Bernoulli b (0.9 >= 0.5 → no entra)

        var selected = CreatePolicy().SelectGroundFailures(candidates, cap: 5);

        var selectedItems = selected.Select(x => x.Item).ToArray();
        Assert.Equal(new[] { "a" }, selectedItems);
    }

    [Fact]
    public void Select_OverCap_PrioritizesHigherWearThenProbability() {
        var candidates = new[] {
            Candidate("low-wear", 1.0, 10),
            Candidate("high-wear", 1.0, 90),
            Candidate("mid-wear", 1.0, 50)
        };
        foreach (var _ in candidates) {
            _random.SetupNextDouble(0.0); // Bernoulli: 0 < 1 → entra
            _random.SetupNextDouble(0.5); // order key (desempate final)
        }

        var selected = CreatePolicy().SelectGroundFailures(candidates, cap: 2);

        Assert.Equal(new[] { "high-wear", "mid-wear" }, selected.Select(x => x.Item).ToArray());
    }

    [Fact]
    public void Select_OverCap_SameWear_PrioritizesHigherProbability() {
        var candidates = new[] {
            Candidate("low-p", 0.5, 80),
            Candidate("high-p", 0.9, 80)
        };
        foreach (var _ in candidates) {
            _random.SetupNextDouble(0.0); // Bernoulli: 0 < p → entran ambos
            _random.SetupNextDouble(0.5); // order key (desempate final)
        }

        var selected = CreatePolicy().SelectGroundFailures(candidates, cap: 1);

        var selectedItems = selected.Select(x => x.Item).ToArray();
        Assert.Equal(new[] { "high-p" }, selectedItems);
    }

    [Fact]
    public void Select_OverCap_SameWearAndProbability_BreaksTieByRandomKey() {
        var candidates = new[] {
            Candidate("first", 1.0, 80),
            Candidate("second", 1.0, 80)
        };
        // first: bernoulli + order key 0.8; second: bernoulli + order key 0.2
        _random.SetupNextDouble(0.0);
        _random.SetupNextDouble(0.8);
        _random.SetupNextDouble(0.0);
        _random.SetupNextDouble(0.2);

        var selected = CreatePolicy().SelectGroundFailures(candidates, cap: 1);

        var selectedItems = selected.Select(x => x.Item).ToArray();
        Assert.Equal(new[] { "second" }, selectedItems);
    }

    [Fact]
    public void Select_CarriesCandidatePayloadIntact() {
        var payload = (Def: new RealisticProfileSettings { RiskLevel = 3 }, Name: "ELEC");
        var candidates = new[] { new GroundFailureCandidate<(RealisticProfileSettings Def, string Name)>(payload, 1.0, 42) };
        _random.SetupNextDouble(0.0);

        var selected = CreatePolicy().SelectGroundFailures(candidates, cap: 3);

        var chosen = Assert.Single(selected);
        Assert.Equal(42, chosen.WearPercentage);
        Assert.Equal(1.0, chosen.Probability);
        Assert.Equal("ELEC", chosen.Item.Name);
        Assert.Equal(3, chosen.Item.Def.RiskLevel);
    }
}
