namespace RealFenixFailures.Application.Session;

/// <summary>
/// Escala de la cantidad de sistemas que acumulan desgaste al cerrar una sesión (normal o huérfana):
/// 1,5 sistemas por hora de vuelo, mínimo 3 (vuelo corto) y máximo todos los elegibles (vuelo largo).
/// <c>SessionSystemsCap</c> > 0 recota el máximo; 0 o negativo = sin tope.
/// </summary>
public static class RealisticWearScaling {
    public const double SystemsPerFlightHour = 1.5;
    public const int MinSystems = 3;

    public static int CountForHours(double flightHours, int eligibleCount, int sessionSystemsCap) {
        if (eligibleCount <= 0) return 0;
        var target = (int)Math.Ceiling(SystemsPerFlightHour * flightHours);
        var upper = eligibleCount;
        if (sessionSystemsCap > 0)
            upper = Math.Min(upper, sessionSystemsCap);
        var lower = Math.Min(MinSystems, upper);
        return Math.Clamp(target, lower, upper);
    }
}
