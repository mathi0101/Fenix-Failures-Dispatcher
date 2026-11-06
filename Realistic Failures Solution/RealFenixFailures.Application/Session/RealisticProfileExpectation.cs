using RealFenixFailures.Domain.Services;

namespace RealFenixFailures.Application.Session;

/// <summary>
/// Distribución de la semilla de tierra para las estimaciones de presentación (spec §8.7): no cambia el
/// motor, solo traduce la configuración vigente (física global + perfil) a cómo arranca cada vuelo —
/// desde 0 fallas (arranque limpio) hasta el tope del perfil. Independiente del catálogo: el universo
/// elegible solo reparte la probabilidad entre candidatas (espec §4.1), no escala el total.
/// </summary>
public static class RealisticProfileExpectation {
    /// <summary>λ del sorteo de tierra: λ₀ (<c>GroundSeedLambdaBase</c>) × multiplicador del perfil × factor de desgaste.</summary>
    public static double GroundSeedLambda(RealisticPhysicsSettings physics, RealisticProfileSettings profile, double? wearPct) =>
        Math.Max(0, physics.GroundSeedLambdaBase)
        * Math.Max(0, profile.ProbabilityMultiplier)
        * RealisticFailurePolicy.WearFactor(profile, wearPct);

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
    /// Distribución de semillas de tierra por vuelo: P(k fallas al iniciar) para k = 0..<paramref name="cap"/>,
    /// con X ~ Poisson(λ) truncada por el tope del perfil (la masa que excede el cap se acumula en el último
    /// elemento). El resultado suma 1; <c>probabilities[0]</c> es la probabilidad de arrancar limpio.
    /// </summary>
    public static double[] GroundSeedDistribution(RealisticPhysicsSettings physics, RealisticProfileSettings profile, double? wearPct) {
        var cap = Math.Max(0, profile.GroundFailuresCap);
        var lambda = GroundSeedLambda(physics, profile, wearPct);
        var probabilities = new double[cap + 1];
        if (cap <= 0 || lambda <= 0) {
            probabilities[0] = 1;
            return probabilities;
        }
        var pmf = Math.Exp(-lambda);
        var cumulative = 0.0;
        for (var k = 0; k < cap; k++) {
            probabilities[k] = pmf;
            cumulative += pmf;
            pmf *= lambda / (k + 1);
        }
        probabilities[cap] = Math.Max(0, 1 - cumulative);
        return probabilities;
    }

    /// <summary>
    /// Fallas esperadas al iniciar la sesión (media de la distribución): λ₀ × multiplicador de perfil ×
    /// factor de desgaste, truncada por el cap de tierra del perfil. La media no es lo que ocurre en cada
    /// vuelo: cada vuelo sortea entre 0 y el cap del perfil.
    /// </summary>
    public static double ExpectedGroundSeedsPerFlight(
        RealisticPhysicsSettings physics,
        RealisticProfileSettings profile,
        double? wearPct) =>
        MeanTruncatedPoisson(GroundSeedLambda(physics, profile, wearPct), profile.GroundFailuresCap);
}
