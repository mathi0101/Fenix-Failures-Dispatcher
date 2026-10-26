using RealFenixFailures.Domain.Services;

namespace RealFenixFailures.Application.Session;

/// <summary>
/// Esperanza matemática de la semilla de tierra para las estimaciones de presentación (spec §8.7):
/// no cambia el motor, solo traduce la configuración vigente (física global + perfil) a un número
/// esperado de fallas por vuelo usando el universo real del catálogo curado por severidad.
/// </summary>
public static class RealisticProfileExpectation {
    /// <summary>
    /// E[min(X, cap)] con X ~ Poisson(lambda): media de la cantidad de semillas de tierra truncada por
    /// el tope de simultáneas del perfil. Con lambda no positivo o cap no positivo devuelve 0.
    /// </summary>
    public static double MeanTruncatedPoisson(double lambda, int cap) {
        if (lambda <= 0 || cap <= 0) return 0;
        var pmf = Math.Exp(-lambda);
        var cumulative = pmf;
        var expected = 0.0;
        for (var k = 1; k <= cap; k++) {
            pmf *= lambda / k;
            expected += k * pmf;
            cumulative += pmf;
        }
        return expected + cap * (1 - cumulative);
    }

    /// <summary>
    /// Fallas esperadas al iniciar la sesión: p_base de tierra × Σ(pesos de severidad Minor/Major del
    /// catálogo) × multiplicador de perfil × factor de desgaste, truncada por el cap de tierra del perfil.
    /// Critical nunca participa de la semilla de tierra (las descarta <c>FailureRuleEvaluator</c>).
    /// </summary>
    public static double ExpectedGroundSeedsPerFlight(
        RealisticPhysicsSettings physics,
        RealisticProfileSettings profile,
        int minorCount,
        int majorCount,
        double? wearPct) {
        var lambda = Math.Max(0, physics.GroundSeedBaseProbability)
            * (Math.Max(0, minorCount) * physics.SeverityRateMultiplier.Minor
               + Math.Max(0, majorCount) * physics.SeverityRateMultiplier.Major)
            * Math.Max(0, profile.ProbabilityMultiplier)
            * RealisticFailurePolicy.WearFactor(profile, wearPct);
        return MeanTruncatedPoisson(lambda, profile.GroundFailuresCap);
    }
}
