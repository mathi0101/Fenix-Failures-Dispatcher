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

    /// <summary>
    /// Horas de vuelo esperadas de la aeronave para que un sistema con la tasa dada alcance el porcentaje de
    /// desgaste pedido, promediando el sorteo de sistemas por sesión: en una sesión de <paramref name="sessionHours"/>
    /// horas, cada sistema acumula solo si sale sorteado (probabilidad <c>CountForHours / elegibles</c>), así que su
    /// tasa efectiva por hora volada es <c>WearPerFlightHour × multiplicador × selecciones/elegibles</c>. Con sorteo
    /// garantizado (vuelos largos o sin alternativa) la cota inferior es <c>percentage / (tasa × multiplicador)</c>.
    /// Devuelve <c>PositiveInfinity</c> si la tasa o la sesión no son positivas.
    /// </summary>
    public static double ExpectedFlightHoursToWear(double percentage, double wearPerFlightHour, double sessionHours, int eligibleCount, int sessionSystemsCap, double wearRateMultiplier = 1.0) {
        if (wearPerFlightHour <= 0 || sessionHours <= 0 || eligibleCount <= 0) return double.PositiveInfinity;
        var selected = CountForHours(sessionHours, eligibleCount, sessionSystemsCap);
        if (selected <= 0) return double.PositiveInfinity;
        var multiplier = Math.Max(0, wearRateMultiplier);
        if (multiplier <= 0) return double.PositiveInfinity;
        return percentage / (wearPerFlightHour * multiplier * selected / eligibleCount);
    }
}
