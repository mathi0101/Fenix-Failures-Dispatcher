using RealFenixFailures.Domain.Enums;

namespace RealFenixFailures.Domain.Services;

public class FailureEngineSettings {
    public const string SectionName = "FailureEngine";

    /// <summary>
    /// Flag de debug del motor: activa herramientas de desarrollo desde appsettings en instalaciones de
    /// testing sin recompilar. Hoy gobierna: la visibilidad del bloque «Reloj de simulación» del panel
    /// Realistic y el log de diagnóstico de SimConnect (ex flag <c>SimConnectDiagnostics:Enabled</c>).
    /// Código = false; en producción el appsettings puede omitirlo o dejarlo en false.
    /// </summary>
    public bool Debug { get; set; } = false;

    /// <summary>Límite de entradas del log de eventos en vivo de la UI (recorte por la cola).</summary>
    public int MaxLogEntries { get; set; } = 50;

    public int CheckConnectionIntervalSeconds { get; set; } = 30;
    public int CheckIntervalSeconds { get; set; } = 10;

    public RealisticModeSettings RealisticMode { get; set; } = new();
}

public class RealisticModeSettings {
    public int EvaluationIntervalSeconds { get; set; } = 60;
    public int PreparedQueueLimit { get; set; } = 5;
    public bool GroundFailureEnabled { get; set; } = true;
    public int ThinkingLinesCap { get; set; } = 50;
    public double EvaluationDeltaCapSeconds { get; set; } = 300;
    public int RespiteClampSeconds { get; set; } = 900;
    public RealisticPhysicsSettings Physics { get; set; } = new();
    public List<RealisticProfileSettings> Profiles { get; set; } = [];
    public OfpWindowSettings Ofp { get; set; } = new();
    public WearSettings Wear { get; set; } = new();
}

/// <summary>
/// Física global del motor (spec modelo-probabilístico §1.1): parámetros de la fórmula unificada y del
/// score de agravamiento, compartidos por tierra y vuelo. La política por perfil vive en
/// <see cref="RealisticProfileSettings"/>.
/// </summary>
public class RealisticPhysicsSettings {
    /// <summary>λ base por falla Minor elegible en vuelo, en eventos/hora.</summary>
    public double FailureRatePerHourBase { get; set; } = 0.01;
    /// <summary>Probabilidad base (0–1) de cada falla Minor individual en el seed de tierra, antes de multiplicadores.</summary>
    public double GroundSeedBaseProbability { get; set; } = 0.01;
    public PhysicsSeverityRateMultipliers SeverityRateMultiplier { get; set; } = new();
    public PhysicsAggravationWeights AggravationWeights { get; set; } = new();
    /// <summary>Puntos extra de agravamiento por cada falla previa simultánea del mismo sistema (ATA).</summary>
    public int SameAtaAggravationExtra { get; set; } = 1;
}

/// <summary>Cuánto más rara es cada severidad a igualdad de todo lo demás (multiplicador de λ / p).</summary>
public class PhysicsSeverityRateMultipliers {
    public double Minor { get; set; } = 1.0;
    public double Major { get; set; } = 0.4;
    public double Critical { get; set; } = 0.15;

    public double For(FailureSeverity severity) => severity switch {
        FailureSeverity.Major => Major,
        FailureSeverity.Critical => Critical,
        _ => Minor
    };
}

/// <summary>Pesos con que cada severidad suma al score de agravamiento de la sesión.</summary>
public class PhysicsAggravationWeights {
    public int Minor { get; set; } = 1;
    public int Major { get; set; } = 2;
    public int Critical { get; set; } = 4;

    public int For(FailureSeverity severity) => severity switch {
        FailureSeverity.Major => Major,
        FailureSeverity.Critical => Critical,
        _ => Minor
    };
}

public class OfpWindowSettings {
    public double PreStartWindowHours { get; set; } = 2;
    public double PostArrivalWindowHours { get; set; } = 0.5;
}

public class WearSettings {
    public double AmplificationFactor { get; set; } = 0.5;
    public int ThresholdNoticePct { get; set; } = 80;
    /// <summary>
    /// Tope de sistemas que acumulan desgaste al cerrar la sesión: se elige una cantidad aleatoria
    /// entre 1 (hardcodeado) y este valor, y solo esos sistemas suman. Valores menores a 1 se clampean a 1.
    /// Limita el realismo forzado de "todos los sistemas" y el tamaño del desgaste en el resumen.
    /// </summary>
    public int SessionSystemsCap { get; set; } = 5;
}
