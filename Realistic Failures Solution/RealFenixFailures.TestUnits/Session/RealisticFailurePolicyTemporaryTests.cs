using Moq;
using RealFenixFailures.Application.Session;
using Xunit;

namespace RealFenixFailures.TestUnits.Session;

/// <summary>
/// Matemática de la temporalidad de fallas aleatorias (tipos-de-falla §4.3) con reloj y RNG deterministas:
/// probabilidad (0 nunca, 1 siempre), vida uniforme dentro de la ventana configurada (incluida la
/// normalización de bordes invertidos y el corte con valores no positivos). La exclusión de críticas y el
/// armado con expiración viven en el engine (no aquí).
/// </summary>
public class RealisticFailurePolicyTemporaryTests {
    private static readonly DateTime BaseUtc = new(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);

    private static RealisticFailurePolicy Policy(Random random) =>
        new(Mock.Of<RealFenixFailures.Domain.Interfaces.IFailureRuleEvaluator>(), () => BaseUtc, random);

    [Fact]
    public void RollTemporary_ZeroProbability_NeverTriggers() {
        var policy = Policy(new Random(7));
        for (var i = 0; i < 100; i++) Assert.False(policy.RollTemporaryFailure(0));
    }

    [Fact]
    public void RollTemporary_CertainProbability_AlwaysTriggers() {
        var policy = Policy(new Random(7));
        for (var i = 0; i < 100; i++) Assert.True(policy.RollTemporaryFailure(1));
    }

    [Fact]
    public void TemporaryExpiry_FallsWithinConfiguredWindow() {
        var policy = Policy(new Random(42));
        for (var i = 0; i < 200; i++) {
            var expiry = policy.TemporaryExpiryUtc(10, 30);
            Assert.InRange(expiry, BaseUtc.AddMinutes(10), BaseUtc.AddMinutes(30));
        }
    }

    [Fact]
    public void TemporaryExpiry_InvertedBounds_NormalizesWindow() {
        var policy = Policy(new Random(42));
        var expiry = policy.TemporaryExpiryUtc(30, 10);
        Assert.InRange(expiry, BaseUtc.AddMinutes(10), BaseUtc.AddMinutes(30));
    }

    [Fact]
    public void TemporaryExpiry_NonPositiveWindow_ReturnsNow() {
        var policy = Policy(new Random(42));
        Assert.Equal(BaseUtc, policy.TemporaryExpiryUtc(0, 0));
        Assert.Equal(BaseUtc, policy.TemporaryExpiryUtc(-5, 0));
    }
}
