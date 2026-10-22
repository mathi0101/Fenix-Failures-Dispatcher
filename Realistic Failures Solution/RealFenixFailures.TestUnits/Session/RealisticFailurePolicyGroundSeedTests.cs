// RealFenixFailures.TestUnits/Session/RealisticFailurePolicyGroundSeedTests.cs
using RealFenixFailures.Application.Session;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Interfaces;
using RealFenixFailures.Domain.Services;
using RealFenixFailures.TestUnits.Mocks;
using Xunit;

namespace RealFenixFailures.TestUnits.Session;

/// <summary>
/// Modelo de seed de fallas de tierra: probabilidad individual por falla (Bernoulli) con p_base del perfil
/// amplificada por el desgaste del sistema padre, y tope GroundFailuresCap priorizando desgaste → p_base → azar.
/// </summary>
public class RealisticFailurePolicyGroundSeedTests {
    private readonly MockRandom _random = new();

    private RealisticFailurePolicy CreatePolicy() =>
        new(new FailureRuleEvaluator(), () => new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc), _random);

    // -------------------------------------------------------------------------
    // GroundFailureProbability
    // -------------------------------------------------------------------------

    [Fact]
    public void Probability_WithoutWear_EqualsBase() {
        var profile = new RealisticProfileSettings { GroundFailureBaseProbability = 0.01, GroundWearProbabilityGain = 2 };

        Assert.Equal(0.01, CreatePolicy().GroundFailureProbability(profile, wearPercentage: 0), precision: 6);
    }

    [Theory]
    [InlineData(50, 0.02)]   // k=2 → 50% de desgaste duplica la base
    [InlineData(100, 0.03)]  // k=2 → 100% de desgaste triplica la base
    public void Probability_WithWear_AmplifiesBase(double wear, double expected) {
        var profile = new RealisticProfileSettings { GroundFailureBaseProbability = 0.01, GroundWearProbabilityGain = 2 };

        Assert.Equal(expected, CreatePolicy().GroundFailureProbability(profile, wear), precision: 6);
    }

    [Fact]
    public void Probability_ClampsToUnit() {
        var profile = new RealisticProfileSettings { GroundFailureBaseProbability = 0.9, GroundWearProbabilityGain = 2 };

        Assert.Equal(1.0, CreatePolicy().GroundFailureProbability(profile, wearPercentage: 100), precision: 6);
    }

    [Fact]
    public void Probability_NegativeValues_TreatedAsZero() {
        var profile = new RealisticProfileSettings { GroundFailureBaseProbability = -1, GroundWearProbabilityGain = 2 };

        Assert.Equal(0.0, CreatePolicy().GroundFailureProbability(profile, wearPercentage: 50), precision: 6);
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
        var payload = (Def: new RealisticProfileSettings { RiskLevel = (int)RiskLevel.High }, Name: "ELEC");
        var candidates = new[] { new GroundFailureCandidate<(RealisticProfileSettings Def, string Name)>(payload, 1.0, 42) };
        _random.SetupNextDouble(0.0);

        var selected = CreatePolicy().SelectGroundFailures(candidates, cap: 3);

        var chosen = Assert.Single(selected);
        Assert.Equal(42, chosen.WearPercentage);
        Assert.Equal(1.0, chosen.Probability);
        Assert.Equal("ELEC", chosen.Item.Name);
        Assert.Equal((int)RiskLevel.High, chosen.Item.Def.RiskLevel);
    }
}
