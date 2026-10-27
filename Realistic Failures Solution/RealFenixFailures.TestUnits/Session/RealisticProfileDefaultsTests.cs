// RealFenixFailures.TestUnits/Session/RealisticProfileDefaultsTests.cs
using Microsoft.Extensions.Configuration;
using RealFenixFailures.Domain.Enums;
using RealFenixFailures.Domain.Services;
using Xunit;

namespace RealFenixFailures.TestUnits.Session;

/// <summary>
/// Contratos de los perfiles de riesgo sin valores de calibración fijados en el test: la intensidad crece
/// con el nivel de riesgo y la configuración pisa solo los campos presentes, conservando el resto del
/// default. Los valores numéricos se leen siempre de la misma fuente que usa el motor (defaults de
/// <see cref="RealisticModeSettings"/>), así que cambiar la calibración no rompe la suite salvo que viole
/// un invariante de diseño.
/// </summary>
public class RealisticProfileDefaultsTests {
    public static readonly RiskLevel[] AllRisks = [RiskLevel.Low, RiskLevel.Moderate, RiskLevel.High, RiskLevel.Squawk7700];

    private static readonly RealisticModeSettings Defaults = new();

    public static System.Collections.Generic.IEnumerable<object[]> AllRisksAsMemberData() =>
        AllRisks.Select(r => new object[] { r });

    [Theory]
    [MemberData(nameof(AllRisksAsMemberData))]
    public void Defaults_AllKnobsAreUsable(RiskLevel risk) {
        var profile = Defaults.ProfileFor(risk);

        Assert.True(profile.ProbabilityMultiplier > 0, "El multiplicador de probabilidad debe ser positivo.");
        Assert.True(profile.WearMaxMultiplier >= 1, "El desgaste máximo no puede atenuar fallas (>= 1).");
        Assert.True(profile.AggravationCap > 0, "El tope de agravación debe ser positivo.");
        Assert.True(profile.GroundFailuresCap >= 0, "El tope de tierra no puede ser negativo.");
        Assert.True(profile.CriticalRespiteSeconds >= 0, "La tregua crítica no puede ser negativa.");
    }

    [Fact]
    public void Defaults_IntensityGrowsWithRiskLevel() {
        var profiles = AllRisks.Select(Defaults.ProfileFor).ToList();

        for (var i = 1; i < profiles.Count; i++) {
            var weaker = profiles[i - 1];
            var stronger = profiles[i];
            Assert.True(stronger.ProbabilityMultiplier > weaker.ProbabilityMultiplier,
                $"{AllRisks[i]} debe multiplicar más que {AllRisks[i - 1]}.");
            Assert.True(stronger.WearMaxMultiplier >= weaker.WearMaxMultiplier,
                $"{AllRisks[i]} debe amplificar igual o más el desgaste que {AllRisks[i - 1]}.");
            Assert.True(stronger.AggravationCap >= weaker.AggravationCap,
                $"{AllRisks[i]} debe tolerar igual o más agravación que {AllRisks[i - 1]}.");
            Assert.True(stronger.GroundFailuresCap >= weaker.GroundFailuresCap,
                $"{AllRisks[i]} permite igual o más fallas de tierra que {AllRisks[i - 1]}.");
        }
    }

    [Fact]
    public void Defaults_Squawk7700_IsTheMostIntenseProfile() {
        var squawk = Defaults.ProfileFor(RiskLevel.Squawk7700);
        var all = AllRisks.Select(Defaults.ProfileFor).ToList();

        Assert.Equal(all.Max(p => p.ProbabilityMultiplier), squawk.ProbabilityMultiplier);
        Assert.Equal(all.Max(p => p.WearMaxMultiplier), squawk.WearMaxMultiplier);
        Assert.Equal(all.Max(p => p.AggravationCap), squawk.AggravationCap);
        Assert.Equal(all.Max(p => p.GroundFailuresCap), squawk.GroundFailuresCap);
    }

    [Theory]
    [InlineData(RiskLevel.Low, 1)]
    [InlineData(RiskLevel.Moderate, 2)]
    [InlineData(RiskLevel.High, 3)]
    [InlineData(RiskLevel.Squawk7700, 5)]
    public void ProfileFor_ConfiguredProfileOverridesDefaults(RiskLevel risk, int cap) {
        var settings = new RealisticModeSettings {
            Low = new RealisticProfileSettings { RiskLevel = 1, GroundFailuresCap = 1 },
            Moderate = new RealisticProfileSettings { RiskLevel = 2, GroundFailuresCap = 2 },
            High = new RealisticProfileSettings { RiskLevel = 3, GroundFailuresCap = 3 },
            Squawk7700 = new RealisticProfileSettings { RiskLevel = 4, GroundFailuresCap = 5 }
        };

        var profile = settings.ProfileFor(risk);

        Assert.Equal(cap, profile.GroundFailuresCap);
    }

    [Fact]
    public void Bind_PartialProfileConfig_KeepsCodeDefaultsForAbsentKeys() {
        // La clave del binding de .NET: un perfil pisado parcialmente conserva los defaults de código de
        // los campos ausentes (la lista vieja los zeraba); los perfiles no tocados quedan intactos.
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["FailureEngine:RealisticMode:Low:ProbabilityMultiplier"] = "0.9"
        }).Build().GetSection("FailureEngine").Get<FailureEngineSettings>()!;

        var low = settings.RealisticMode.ProfileFor(RiskLevel.Low);
        Assert.Equal(0.9, low.ProbabilityMultiplier, precision: 5);
        Assert.Equal(Defaults.ProfileFor(RiskLevel.Low).AggravationCap, low.AggravationCap);
        Assert.Equal(Defaults.ProfileFor(RiskLevel.Low).GroundFailuresCap, low.GroundFailuresCap);

        var high = settings.RealisticMode.ProfileFor(RiskLevel.High);
        Assert.Equal(Defaults.ProfileFor(RiskLevel.High).ProbabilityMultiplier, high.ProbabilityMultiplier, precision: 9);
        Assert.Equal(Defaults.ProfileFor(RiskLevel.High).AggravationCap, high.AggravationCap);
    }
}
